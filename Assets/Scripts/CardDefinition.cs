using UnityEngine;

[CreateAssetMenu(menuName = "BladesOut/Card Definition", fileName = "CardDefinition")]
public class CardDefinition : ScriptableObject
{
    [Header("Identity")]
    public int id; // unique across all cards
    public string cardName;
    [TextArea] public string description;
    public Sprite image;

    [Header("3D Showcase (optional)")]
    public GameObject showcasePrefab;
    public Vector3 showcaseLocalOffset = new Vector3(0f, 0.15f, 0f);
    public float showcaseLocalScale = 1f;

    // === Gameplay ===
    public enum PlayStyle { Instant, SetReaction, SetDelayed }
    public enum EffectType
    {
        DealDamage,             // amount
        HealSelf,               // amount
        HealAll,                // amount to everyone
        Poison,                 // amount per tick, durationTurns
        Reflect1,               // reflect 1 dmg on next hit (consumed)
        ReflectFirstAttack,     // reflect full first attack (consumed)
        FirstAttackerTakes2,    // on next hit, attacker takes 2 (consumed)
        ChainArc                // deal amount to target, then arcs times to next players (future)
    }

    [Header("Gameplay")]
    public PlayStyle playStyle = PlayStyle.Instant;
    public EffectType effect = EffectType.DealDamage;
    [Tooltip("Generic amount: damage/heal/etc.")] public int amount = 1;
    [Tooltip("Turns for Poison/Delayed etc.")] public int durationTurns = 0;
    [Tooltip("For ChainArc style effects.")] public int arcs = 0;
    [Tooltip("If true, applies to all players (e.g., HealAll).")] public bool targetAll = false;

    [System.Serializable]
    public struct UpgradeTier
    {
        public int attack;      // optional stat
        public int defense;     // optional stat
        public int costGold;    // gold to upgrade FROM previous tier TO this tier
        [TextArea] public string effectText; // optional per-tier text
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
}
