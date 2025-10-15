using Mirror;
using UnityEngine;

public static class CardEffectResolver
{
    // ===== INSTANTS =====
    [Server]
    public static void PlayInstant(CardDefinition def, int level, PlayerState caster, PlayerState target)
    {
        if (def == null || caster == null) return;

        switch (def.effect)
        {
            case CardDefinition.EffectType.DealDamage:
                if (target != null) target.Server_ApplyDamage(caster, def.amount);
                break;

            case CardDefinition.EffectType.HealSelf:
                caster.Server_Heal(def.amount);
                break;

            case CardDefinition.EffectType.HealAll:
                TurnManager.Instance?.Server_HealAll(def.amount);
                break;

            case CardDefinition.EffectType.Poison:
                if (target != null) target.Server_AddPoison(def.amount, def.durationTurns);
                break;

            case CardDefinition.EffectType.ChainArc:
                if (target != null) TurnManager.Instance?.Server_ChainArc(caster, target, def.amount, def.arcs);
                break;

            default:
                Debug.LogWarning($"[CardEffectResolver] Unhandled instant effect {def.effect}");
                break;
        }
    }

    // ===== REACTIONS / SET CARDS =====
    // Invoked by defender when damage comes in; can modify damage and counter-hit
    [Server]
    public static void TryReactOnIncomingHit(PlayerState defender, PlayerState attacker, ref int incomingDamage)
    {
        if (defender == null) return;
        // Scan defender's set row for first matching reaction
        for (int i = 0; i < defender.setIds.Count; i++)
        {
            var def = defender.database?.Get(defender.setIds[i]);
            if (def == null) continue;

            if (def.playStyle != CardDefinition.PlayStyle.SetReaction)
                continue;

            switch (def.effect)
            {
                case CardDefinition.EffectType.ReflectFirstAttack:
                    // reflect full damage, consume card
                    if (attacker != null && incomingDamage > 0)
                    {
                        attacker.Server_ApplyDamage(defender, incomingDamage);
                        incomingDamage = 0;
                        defender.Server_ConsumeSetAt(i);
                        return;
                    }
                    break;

                case CardDefinition.EffectType.Reflect1:
                    if (attacker != null && incomingDamage > 0)
                    {
                        attacker.Server_ApplyDamage(defender, 1);
                        incomingDamage = Mathf.Max(0, incomingDamage - 1);
                        defender.Server_ConsumeSetAt(i);
                        return;
                    }
                    break;

                case CardDefinition.EffectType.FirstAttackerTakes2:
                    if (attacker != null && incomingDamage > 0)
                    {
                        attacker.Server_ApplyDamage(defender, 2);
                        defender.Server_ConsumeSetAt(i);
                        return;
                    }
                    break;
            }
        }
    }
}
