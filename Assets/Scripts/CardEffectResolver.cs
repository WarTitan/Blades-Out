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
            // ───────── Named instants (no target) ─────────
            case CardDefinition.EffectType.LovePotion_HealXSelf:
                caster.Server_Heal(X);
                break;

            case CardDefinition.EffectType.Bomb_AllPlayersTakeX:
                foreach (var p in FindAll<PlayerState>())
                    p.Server_ApplyDamage(caster, X);
                break;

            case CardDefinition.EffectType.PhoenixFeather_HealX_ReviveTo2IfDead:
                // Proactive cast: just heal now (auto-revive is handled in lethal damage path).
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

            // ───────── Named instants with target ─────────
            case CardDefinition.EffectType.Knife_DealX:
                if (target != null) target.Server_ApplyDamage(caster, X);
                break;

            case CardDefinition.EffectType.KnifePotion_DealX_HealXSelf:
                if (target != null) target.Server_ApplyDamage(caster, X);
                caster.Server_Heal(X);
                break;

            case CardDefinition.EffectType.C4_ExplodeOnTargetAfter3Turns:
                // Place a C4 "item" on TARGET's set row and arm a 3-turn fuse on them.
                if (target != null)
                {
                    target.Server_AddToSet(def.id, (byte)level);
                    target.Server_AddStatus_C4Fuse(level, /*turns*/3, X);
                }
                break;

            case CardDefinition.EffectType.GoblinHands_MoveOneSetItemToCaster:
                // Move the first set item from TARGET to a RANDOM OTHER PLAYER (not the target).
                if (target != null && target.setIds.Count > 0)
                {
                    int idx = FindFirstMovableSetIndex(target);
                    if (idx >= 0)
                    {
                        int movedId = target.setIds[idx];
                        byte movedLvl = target.setLvls[idx];
                        target.Server_ConsumeSetAt(idx);

                        // Pick a random player that's NOT the original target. If none, fall back to caster.
                        var allPlayers = FindAll<PlayerState>();
                        var candidates = allPlayers.Where(p => p != null && p != target).ToArray();
                        PlayerState destination = null;

                        if (candidates.Length > 0)
                        {
                            int r = Random.Range(0, candidates.Length);
                            destination = candidates[r];
                        }
                        else
                        {
                            destination = caster;
                        }

                        if (destination != null)
                            destination.Server_AddToSet(movedId, movedLvl);
                    }
                }
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

            case CardDefinition.EffectType.Turtle_TargetSkipsNextTurn:
                if (target != null)
                {
                    target.Server_AddToSet(def.id, (byte)level);
                    target.Server_AddStatus_TurtleSkipNext();
                }
                break;

            case CardDefinition.EffectType.Mirror_CopyLastPlayedByYou:
                // Mirror stays in hand; fire stored effect.
                caster.Server_PlayLastStoredCardCopy(target);
                break;

            // ───────── Set reactions (placed via CmdSetCard; triggered on attack) ─────────
            case CardDefinition.EffectType.Cactus_ReflectUpToX_For3Turns:
            case CardDefinition.EffectType.BearTrap_FirstAttackerTakesX:
            case CardDefinition.EffectType.MirrorShield_ReflectFirstAttackFull:
                // No direct instant effect here; handled as set/status.
                break;

            // ───────── Legacy (kept for compatibility) ─────────
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

    // Find a movable item (here: first set entry).
    static int FindFirstMovableSetIndex(PlayerState ps)
    {
        for (int i = 0; i < ps.setIds.Count; i++)
        {
            var d = ps.database?.Get(ps.setIds[i]);
            if (d == null) continue;
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
        int incomingBefore = incomingDamage;
        defender.Server_TryApplyCactusStatusReflect(attacker, ref incomingBefore);
        incomingDamage = incomingBefore;

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

                    // (Cactus is status-driven; the set card persists as a visual until duration ends.)
            }
        }
    }
}
