using UnityEngine;


[CreateAssetMenu(menuName = "BladesOut/Card Definition", fileName = "CardDefinition")]
public class CardDefinition : ScriptableObject
{
    [Header("Identity")] public int id; // unique across all cards
    public string cardName;
    [TextArea] public string description;
    public Sprite image;
    [Header("3D Showcase (optional)")]
    public GameObject showcasePrefab;      // drag a model prefab here (knife, bomb, etc.)
    public Vector3 showcaseLocalOffset = new Vector3(0f, 0.15f, 0f);
    public float showcaseLocalScale = 1f;



    [System.Serializable]
    public struct UpgradeTier
    {
        public int attack; // optional stat
        public int defense; // optional stat
        public int costGold; // gold to upgrade FROM previous tier TO this tier
        [TextArea] public string effectText; // optional: per-tier effect text
    }


    [Header("Tiers (index 0 = level 1)")]
    public UpgradeTier[] tiers;


    public int MaxLevel
    {
        get { return (tiers != null && tiers.Length > 0) ? tiers.Length : 1; }
    }


    public UpgradeTier GetTier(int level)
    {
        if (tiers == null || tiers.Length == 0) return default;
        int i = Mathf.Clamp(level - 1, 0, tiers.Length - 1);
        return tiers[i];
    }
}