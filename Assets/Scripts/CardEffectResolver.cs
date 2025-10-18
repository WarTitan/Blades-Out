using Mirror;
using UnityEngine;
using System.Linq;

public static class CardEffectResolver
{
    // ---------- compat helper for Unity 2023+ ----------
    // Use FindObjectsByType on 2023+, fall back to FindObjectsOfType on older versions.
    static T[] FindAll<T>(bool sorted = false) where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(
            sorted ? UnityEngine.FindObjectsSortMode.InstanceID
                   : UnityEngine.FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>();
#endif
    }

    // ===== INSTANTS & SETUP =====
    [Server]
    public static void PlayInstant(CardDefinition def, int level, PlayerState caster, PlayerState target)
    {
        if (def == null || caster == null) return;

        var tier = def.GetTier(Mathf.Max(1, level));
        int X = (tier.attack > 0 ? tier.attack : def.amount);  // level-scaled amount
        int dur = def.durationTurns;
        int arcs = def.arcs;

        switch (def.effect)
        {
            // ───────── Named instants ─────────
            case CardDefinition.EffectType.Knife_DealX:
                if (target != null) target.Server_ApplyDamage(caster, X);
                break;

            case CardDefinition.EffectType.KnifePotion_DealX_HealXSelf:
                if (target != null) target.Server_ApplyDamage(caster, X);
                caster.Server_Heal(X);
                break;

            case CardDefinition.EffectType.LovePotion_HealXSelf:
                caster.Server_Heal(X);
                break;

            case CardDefinition.EffectType.Bomb_AllPlayersTakeX:
                foreach (var p in FindAll<PlayerState>())
                    p.Server_ApplyDamage(caster, X);
                break;

            case CardDefinition.EffectType.PhoenixFeather_HealX_ReviveTo2IfDead:
                // proactive self-cast: just heal for X
                caster.Server_Heal(X);
                break;

            case CardDefinition.EffectType.BlackHole_DiscardHandsRedrawSame:
                foreach (var p in FindAll<PlayerState>())
                {
                    int count = p.handIds.Count;
                    p.handIds.Clear();
                    p.handLvls.Clear();
                    p.Server_Draw(count);
                }
                break;

            case CardDefinition.EffectType.Shield_GainXArmor:
                caster.Server_AddArmor(X);
                break;

            case CardDefinition.EffectType.Pickpocket_StealOneRandomHandCard:
                if (target != null && target.handIds.Count > 0)
                {
                    int take = Random.Range(0, target.handIds.Count);
                    int cardId = target.handIds[take];
                    byte lvl = target.handLvls[take];
                    target.Server_RemoveHandAt(take);
                    caster.Server_AddToHand(cardId, lvl);
                }
                break;

            case CardDefinition.EffectType.GoblinHands_MoveOneSetItemToCaster:
                // Moves the FIRST set item found from target to caster.
                if (target != null)
                {
                    int idx = FindFirstMovableSetIndex(target);
                    if (idx >= 0)
                    {
                        int id = target.setIds[idx];
                        byte lvl = target.setLvls[idx];
                        target.Server_ConsumeSetAt(idx);
                        caster.Server_AddToSet(id, lvl);
                    }
                }
                break;

            case CardDefinition.EffectType.C4_ExplodeOnTargetAfter3Turns:
                // Place a C4 "item" on TARGET's set row and arm a 3-turn fuse on them.
                if (target != null)
                {
                    target.Server_AddToSet(def.id, (byte)level);
                    target.Server_AddStatus_C4Fuse(level, /*turns*/3, X);
                }
                break;

            case CardDefinition.EffectType.Turtle_TargetSkipsNextTurn:
                if (target != null)
                {
                    target.Server_AddToSet(def.id, (byte)level);
                    target.Server_AddStatus_TurtleSkipNext();
                }
                break;

            // ───────── Named reactions put in set by player (off-turn) ─────────
            // NOTE: These are placed via CmdSetCard; their effects are in TryReactOnIncomingHit / status system.
            case CardDefinition.EffectType.Cactus_ReflectUpToX_For3Turns:
            case CardDefinition.EffectType.BearTrap_FirstAttackerTakesX:
            case CardDefinition.EffectType.MirrorShield_ReflectFirstAttackFull:
                // Nothing to do here on instant cast; these are handled as SET reactions elsewhere.
                break;

            case CardDefinition.EffectType.Mirror_CopyLastPlayedByYou:
                // Handled in PlayerState: Mirror stays in hand. Here we just fire the stored effect if valid.
                caster.Server_PlayLastStoredCardCopy(target);
                break;

            // ───────── Legacy effects (kept working) ─────────
            case CardDefinition.EffectType.DealDamage:
                if (target != null) target.Server_ApplyDamage(caster, X);
                break;

            case CardDefinition.EffectType.HealSelf:
                caster.Server_Heal(X);
                break;

            case CardDefinition.EffectType.HealAll:
                foreach (var p in FindAll<PlayerState>())
                    p.Server_Heal(X);
                break;

            case CardDefinition.EffectType.Poison:
                if (target != null) target.Server_AddPoison(X, dur);
                break;

            case CardDefinition.EffectType.ChainArc:
                if (target != null) TurnManager.Instance?.Server_ChainArc(caster, target, X, arcs);
                break;

            default:
                if (target != null) target.Server_ApplyDamage(caster, X);
                break;
        }

        // Store "last played" so Mirror can copy it later (ignore Mirror itself).
        if (def.effect != CardDefinition.EffectType.Mirror_CopyLastPlayedByYou)
            caster.Server_RecordLastPlayed(def.id, level);
    }

    // Find a movable item (skip non-item reactions if you want). Here we just take first.
    static int FindFirstMovableSetIndex(PlayerState ps)
    {
        for (int i = 0; i < ps.setIds.Count; i++)
        {
            var d = ps.database?.Get(ps.setIds[i]);
            if (d == null) continue;
            // Treat anything in set as movable except MirrorShield (up to you)
            return i;
        }
        return -1;
    }

    // ===== REACTIONS / SET CARDS =====
    // Invoked by defender when damage comes in; can modify damage and counter-hit
    [Server]
    public static void TryReactOnIncomingHit(PlayerState defender, PlayerState attacker, ref int incomingDamage)
    {
        if (defender == null) return;

        // 1) First, check durable statuses (e.g., Cactus reflect across turns)
        defender.Server_TryApplyCactusStatusReflect(attacker, ref incomingDamage);

        // 2) Then scan defender's set row for one-shot reactions
        for (int i = 0; i < defender.setIds.Count; i++)
        {
            var def = defender.database?.Get(defender.setIds[i]);
            if (def == null) continue;

            switch (def.effect)
            {
                case CardDefinition.EffectType.MirrorShield_ReflectFirstAttackFull:
                case CardDefinition.EffectType.ReflectFirstAttack:
                    if (attacker != null && incomingDamage > 0)
                    {
                        attacker.Server_ApplyDamage(defender, incomingDamage);
                        incomingDamage = 0;
                        defender.Server_ConsumeSetAt(i);
                        return;
                    }
                    break;

                case CardDefinition.EffectType.BearTrap_FirstAttackerTakesX:
                case CardDefinition.EffectType.FirstAttackerTakes2:
                    if (attacker != null && incomingDamage > 0)
                    {
                        int trapDmg = def.GetTier(defender.setLvls[i]).attack;
                        if (trapDmg <= 0) trapDmg = 1;
                        attacker.Server_ApplyDamage(defender, trapDmg);
                        defender.Server_ConsumeSetAt(i);
                        return;
                    }
                    break;

                    // (Cactus is now status-driven; leave set card for visuals; status decrements on turn start)
            }
        }
    }
}
