using UnityEngine;

[CreateAssetMenu(menuName = "BladesOut/Card Definition", fileName = "CardDefinition")]
public class CardDefinition : ScriptableObject
{
    // ───────────────────────────────────────────────── Identity ─────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Unique across all cards.")]
    public int id;
    public string cardName;
    [TextArea] public string description;
    public Sprite image;

    // ───────────────────────────────────────────────── 3D Showcase ───────────────────────────────────────────────
    [Header("3D Showcase (optional)")]
    public GameObject showcasePrefab;
    public Vector3 showcaseLocalOffset = new Vector3(0f, 0.15f, 0f);
    public float showcaseLocalScale = 1f;

    // ───────────────────────────────────────────────── Gameplay core ────────────────────────────────────────────
    public enum PlayStyle { Instant, SetReaction, SetDelayed }
    public enum EffectType
    {
        DealDamage,             // amount
        HealSelf,               // amount
        HealAll,                // amount to everyone
        Poison,                 // amount per tick, durationTurns
        Reflect1,               // reflect X dmg on next hit (consumed)
        ReflectFirstAttack,     // reflect full first attack (consumed)
        FirstAttackerTakes2,    // on next hit, attacker takes X (consumed)
        ChainArc                // deal amount to target, then arcs times to next players
    }

    [Header("Gameplay")]
    public PlayStyle playStyle = PlayStyle.Instant;
    public EffectType effect = EffectType.DealDamage;

    [Tooltip("Generic base amount for effects (damage/heal/etc.). Tier values override this when present.")]
    public int amount = 1;

    [Tooltip("Turns for Poison/Delayed etc.")]
    public int durationTurns = 0;

    [Tooltip("For ChainArc-style effects (extra jumps after the first target).")]
    public int arcs = 0;

    [Tooltip("If true, applies to all players (e.g., HealAll).")]
    public bool targetAll = false;

    // ───────────────────────────────────────────────── Costs ────────────────────────────────────────────────────
    [Header("Costs")]
    [Tooltip("Base poker chip cost to CAST/SET this card when the tier doesn't override.")]
    public int chipCost = 0;

    // ───────────────────────────────────────────────── Tiers ────────────────────────────────────────────────────
    [System.Serializable]
    public struct UpgradeTier
    {
        [Tooltip("Per-level 'attack' number (e.g., damage/heal).")]
        public int attack;

        [Tooltip("Per-level 'defense' number (optional stat for your designs).")]
        public int defense;

        [Tooltip("Gold to upgrade FROM the previous tier TO this tier.")]
        public int costGold;

        [TextArea]
        [Tooltip("Optional per-tier rules text shown on the card.")]
        public string effectText;

        [Tooltip("Optional per-level chip cost to CAST/SET. 0 = use CardDefinition.chipCost.")]
        public int castChipCost;
    }

    [Header("Tiers (index 0 = level 1)")]
    public UpgradeTier[] tiers;

    // ───────────────────────────────────────────────── Helpers ─────────────────────────────────────────────────
    public int MaxLevel => (tiers != null && tiers.Length > 0) ? tiers.Length : 1;

    public UpgradeTier GetTier(int level)
    {
        if (tiers == null || tiers.Length == 0) return default;
        int i = Mathf.Clamp(level - 1, 0, tiers.Length - 1);
        return tiers[i];
    }

    /// <summary>
    /// Convenience: chip cost for a given level (uses tier.castChipCost if >0, else base chipCost).
    /// </summary>
    public int GetCastChipCost(int level)
    {
        var t = GetTier(level);
        return (t.castChipCost > 0) ? t.castChipCost : Mathf.Max(0, chipCost);
    }
}
