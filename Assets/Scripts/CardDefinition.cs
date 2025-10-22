using UnityEngine;

[CreateAssetMenu(menuName = "BladesOut/Card Definition", fileName = "CardDefinition")]
public class CardDefinition : ScriptableObject
{
    // ───────── Identity ─────────
    [Header("Identity")]
    public int id; // unique across all cards
    public string cardName;
    [TextArea] public string description;
    public Sprite image;

    // ───────── Play meta ─────────
    public enum PlayStyle
    {
        Instant,            // plays immediately, no target
        InstantWithTarget,  // select enemy then plays
        SetReaction         // placed on set anchor; triggers on attack (Cactus applies immediately & lasts 3 turns)
    }

    // Keep legacy first for compat, then alphabetically your new effects
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

    [Header("Gameplay")]
    public PlayStyle playStyle = PlayStyle.Instant;
    public EffectType effect = EffectType.DealDamage;

    [Tooltip("Generic base amount for effects (damage/heal/etc.). Tier.attack overrides this.")]
    public int amount = 1;

    [Tooltip("Base duration in turns for certain effects (e.g., Poison); Cactus/C4 also use custom fields.")]
    public int durationTurns = 0;

    [Tooltip("For ChainArc-style effects (extra jumps after the first target).")]
    public int arcs = 0;

    // ───────── Costs ─────────
    [Header("Costs")]
    [Tooltip("Base poker chip cost to CAST/SET this card (tiers can override).")]
    public int chipCost = 0;

    // ───────── 3D Showcase for Set cards ─────────
    [Header("3D Showcase (for Set cards)")]
    [Tooltip("Optional: 3D model to spawn on the Set Anchor when this card is set.")]
    public GameObject setShowcasePrefab;
    public Vector3 setShowcaseLocalOffset = Vector3.zero;
    public Vector3 setShowcaseLocalEuler = Vector3.zero;
    public Vector3 setShowcaseLocalScale = Vector3.one;

    // ───────── Tiers ─────────
    [System.Serializable]
    public struct UpgradeTier
    {
        [Tooltip("Per-level amount (damage/heal/armor/etc.).")]
        public int attack;

        [Tooltip("Optional secondary stat slot.")]
        public int defense;

        [Tooltip("Gold to upgrade FROM the previous tier TO this tier.")]
        public int costGold;

        [TextArea, Tooltip("Optional per-tier rules text shown on the card.")]
        public string effectText;

        [Tooltip("Optional per-level chip cost to CAST/SET. 0 = use CardDefinition.chipCost.")]
        public int castChipCost;
    }

    [Header("Tiers (index 0 = level 1)")]
    public UpgradeTier[] tiers;

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
