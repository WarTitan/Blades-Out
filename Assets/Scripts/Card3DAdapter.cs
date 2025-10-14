using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class Card3DAdapter : MonoBehaviour
{
    [Header("Database Binding")]
    [Tooltip("Card database that contains definitions for all cards.")]
    public CardDatabase database;

    [Tooltip("Definition ID for this card (assigned by your hand logic via Bind).")]
    public int cardId = -1;

    [Tooltip("Current card level (1-based).")]
    public int level = 1;

    [Header("Front Art (Quad)")]
    [Tooltip("Renderer on your QuadFront (optional but recommended).")]
    public MeshRenderer frontRenderer;

    [Header("Texts (UI or 3D)")]
    [Tooltip("Card name text (TMP_Text or TextMeshProUGUI).")]
    public TMP_Text cardNameText;

    [Tooltip("Card description text (TMP_Text or TextMeshProUGUI).")]
    public TMP_Text cardDescriptionText;

    [Tooltip("Optional: shows upgrade cost (TMP_Text or TextMeshProUGUI).")]
    public TMP_Text upgradeCostText;

    [Header("Upgrade Cost Display")]
    public bool showUpgradeCost = true;

    [Tooltip("Text format for showing cost, e.g. \"Upgrade: {0}g\".")]
    public string upgradeCostFormat = "Upgrade: {0}g";

    [Tooltip("Text to show when already at max level (for NextLevel mode).")]
    public string maxLevelText = "MAX";

    public enum CostMode { NextLevel, SpecificTier }

    [Tooltip("NextLevel = cost to upgrade from current level to next. SpecificTier = show fixed tier's cost.")]
    public CostMode costMode = CostMode.SpecificTier;

    [Tooltip("When CostMode = SpecificTier, this is the level whose cost you want to display (1-based).")]
    public int specificTierLevel = 2;

    [Header("Front Fitting (optional)")]
    [Tooltip("Auto-scale QuadFront's local X/Y to match sprite aspect.")]
    public bool autoFitAspect = true;

    [Tooltip("Target local height for QuadFront when auto-fitting.")]
    public float targetHeight = 1f;

    /// <summary>
    /// Call this from your hand/visualizer after Instantiate.
    /// </summary>
    public void Bind(int id, int lvl, CardDatabase db)
    {
        cardId = id;
        level = Mathf.Max(1, lvl);
        database = db;
        Apply();
    }

    /// <summary>
    /// Re-apply data to visuals (safe to call anytime after fields change).
    /// </summary>
    public void Apply()
    {
        if (database == null) return;
        var def = database.Get(cardId);
        if (def == null) return; // unknown id

        // Name
        if (cardNameText != null)
            cardNameText.text = def.cardName;

        // Description (prefer tier.effectText if provided)
        string desc = def.description;
        // Safe even if tiers empty: GetTier returns default struct; effectText can be null
        var tier = def.GetTier(level);
        if (!string.IsNullOrEmpty(tier.effectText))
            desc = tier.effectText;

        if (cardDescriptionText != null)
            cardDescriptionText.text = desc;

        // Artwork
        if (def.image != null && frontRenderer != null)
        {
            var tex = def.image.texture;
            if (tex != null && frontRenderer.material != null)
                frontRenderer.material.mainTexture = tex;

            if (autoFitAspect)
                FitFrontToSprite(def.image, frontRenderer.transform, targetHeight);
        }

        // Cost text
        if (upgradeCostText != null)
        {
            if (!showUpgradeCost)
            {
                upgradeCostText.text = string.Empty;
            }
            else
            {
                if (costMode == CostMode.NextLevel)
                {
                    int cost = GetUpgradeCost(def, level);
                    upgradeCostText.text = (cost >= 0)
                        ? string.Format(upgradeCostFormat, cost)
                        : maxLevelText;
                }
                else // SpecificTier
                {
                    int lvl = Mathf.Max(1, specificTierLevel);
                    // Only show if that tier actually exists
                    if (lvl <= def.MaxLevel)
                    {
                        var targetTier = def.GetTier(lvl); // struct; safe
                        upgradeCostText.text = string.Format(upgradeCostFormat, targetTier.costGold);
                    }
                    else
                    {
                        upgradeCostText.text = string.Empty; // or "N/A"
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cost to upgrade from currentLevel -> next level. Returns -1 if already max.
    /// </summary>
    private int GetUpgradeCost(CardDefinition def, int currentLevel)
    {
        // Bounds via MaxLevel (tiers length). No null checks on struct.
        if (currentLevel >= def.MaxLevel)
            return -1;

        int nextLevel = currentLevel + 1;
        var nextTier = def.GetTier(nextLevel);
        return nextTier.costGold;
    }

    /// <summary>
    /// Fit the QuadFront's local scale to the sprite's aspect ratio (width/height).
    /// Keeps Z scale unchanged; uses targetHeight for local Y.
    /// </summary>
    private void FitFrontToSprite(Sprite s, Transform quad, float desiredHeight)
    {
        if (s == null || quad == null || desiredHeight <= 0f) return;

        float w = s.rect.width;
        float h = s.rect.height;
        if (h <= 0f) return;

        float aspect = w / h; // width / height
        float y = desiredHeight;
        quad.localScale = new Vector3(y * aspect, y, quad.localScale.z);
    }

#if UNITY_EDITOR
    [ContextMenu("Fit Front Quad To Current Sprite")]
    private void FitNowInEditor()
    {
        if (database == null) return;
        var def = database.Get(cardId);
        if (def == null || def.image == null || frontRenderer == null) return;

        FitFrontToSprite(def.image, frontRenderer.transform, targetHeight);
        UnityEditor.EditorUtility.SetDirty(frontRenderer.transform);
    }
#endif
}
