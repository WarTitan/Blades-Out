using UnityEngine;

[CreateAssetMenu(menuName = "BladesOut/Card Definition", fileName = "CardDefinition")]
public class CardDefinition : ScriptableObject
{
    // ───────────────────────── Identity ─────────────────────────
    [Header("Identity")]
    [Tooltip("Unique across all cards.")]
    public int id;
    public string cardName;
    [TextArea] public string description;
    public Sprite image;

    // ───────────────────────── Types ─────────────────────────
    public enum PlayStyle { Instant, SetReaction, SetDelayed }

    // Keep legacy values first (compat), then new ones (alphabetical)
    public enum EffectType
    {
        // legacy
        DealDamage,
        HealSelf,
        HealAll,
        Poison,
        Reflect1,
        ReflectFirstAttack,
        FirstAttackerTakes2,
        ChainArc,

        // new (alphabetical)
        BearTrap_FirstAttackerTakesX,
        BlackHole_DiscardHandsRedrawSame,
        Bomb_AllPlayersTakeX,
        C4_ExplodeOnTargetAfter3Turns,
        Cactus_ReflectUpToX_For3Turns,
        GoblinHands_MoveOneSetItemToCaster,
        Knife_DealX,
        KnifePotion_DealX_HealXSelf,
        LovePotion_HealXSelf,
        Mirror_CopyLastPlayedByYou,
        MirrorShield_ReflectFirstAttackFull,
        PhoenixFeather_HealX_ReviveTo2IfDead,
        Pickpocket_StealOneRandomHandCard,
        Shield_GainXArmor,
        Turtle_TargetSkipsNextTurn
    }

    // ───────────────────────── Gameplay (fields) ─────────────────────────
    [Header("Gameplay")]
    public PlayStyle playStyle = PlayStyle.Instant;
    public EffectType effect = EffectType.DealDamage;

    [Tooltip("Generic base amount for effects (damage/heal/etc.). Tier.attack overrides this.")]
    public int amount = 1;

    [Tooltip("Base duration in turns for certain effects.")]
    public int durationTurns = 0;

    [Tooltip("For ChainArc-style effects (extra jumps after the first target).")]
    public int arcs = 0;

    // ───────────────────────── Costs ─────────────────────────
    [Header("Costs")]
    [Tooltip("Base poker chip cost to CAST this card (tiers can override).")]
    public int chipCost = 0;

    // ───────────────────────── Tiers ─────────────────────────
    [System.Serializable]
    public struct UpgradeTier
    {
        [Tooltip("Per-level primary amount (damage/heal/armor/etc.).")]
        public int attack;

        [Tooltip("Optional secondary stat slot.")]
        public int defense;

        [Tooltip("Gold to upgrade FROM the previous tier TO this tier.")]
        public int costGold;

        [TextArea, Tooltip("Optional per-tier rules text shown on the card.")]
        public string effectText;

        [Tooltip("Optional per-level chip cost to CAST. 0 = use CardDefinition.chipCost.")]
        public int castChipCost;
    }

    [Header("Tiers (index 0 = level 1)")]
    public UpgradeTier[] tiers;

    // ───────────────────────── Helpers ─────────────────────────
    public int MaxLevel => (tiers != null && tiers.Length > 0) ? tiers.Length : 1;

    public UpgradeTier GetTier(int level)
    {
        if (tiers == null || tiers.Length == 0) return default;
        int i = Mathf.Clamp(level - 1, 0, tiers.Length - 1);
        return tiers[i];
    }

    public int GetCastChipCost(int level)
    {
        var t = GetTier(level);
        return (t.castChipCost > 0) ? t.castChipCost : Mathf.Max(0, chipCost);
    }
}
