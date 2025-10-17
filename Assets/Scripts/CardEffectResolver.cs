using Mirror;
using UnityEngine;

public static class CardEffectResolver
{
    // ===== INSTANTS =====
    [Server]
    public static void PlayInstant(CardDefinition def, int level, PlayerState caster, PlayerState target)
    {
        if (def == null || caster == null) return;

        // Pull level-scaled numbers from the card's tier.
        // Tier.attack is the per-level "amount" you set in the Inspector.
        var tier = def.GetTier(Mathf.Max(1, level));
        int amt = tier.attack > 0 ? tier.attack : def.amount;   // fallback to base amount if tier not set
        int dur = def.durationTurns;                            // duration is currently on the base def
        int arcs = def.arcs;                                     // arcs from base def (change if you want it tiered)

        switch (def.effect)
        {
            case CardDefinition.EffectType.DealDamage:
                if (target != null) target.Server_ApplyDamage(caster, amt);
                break;

            case CardDefinition.EffectType.HealSelf:
                caster.Server_Heal(amt);
                break;

            case CardDefinition.EffectType.HealAll:
                TurnManager.Instance?.Server_HealAll(amt);
                break;

            case CardDefinition.EffectType.Poison:
                if (target != null) target.Server_AddPoison(amt, dur);
                break;

            case CardDefinition.EffectType.ChainArc:
                if (target != null) TurnManager.Instance?.Server_ChainArc(caster, target, amt, arcs);
                break;

            // Reactions are handled in TryReactOnIncomingHit; leave them as-is.
            default:
                // Fallback: if you later add new instant types, they can default to amount-based damage
                if (target != null) target.Server_ApplyDamage(caster, amt);
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
