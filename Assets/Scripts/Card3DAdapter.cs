using UnityEngine;
using TMPro;

public class Card3DAdapter : MonoBehaviour
{
    [Header("Front Art")]
    public MeshRenderer frontRenderer;               // your QuadFront renderer (optional)

    [Header("Texts (3D TMP or UI TMP allowed)")]
    public TMP_Text cardNameText;                    // drag TextMeshPro OR TextMeshProUGUI
    public TMP_Text cardDescriptionText;             // drag TextMeshPro OR TextMeshProUGUI
    public TMP_Text upgradeCostText;                 // OPTIONAL: shows next-level cost gold

    [Header("Upgrade Cost Display")]
    public bool showUpgradeCost = true;              // toggle cost text on/off
    public string upgradeCostFormat = "Upgrade: {0}g";
    public string maxLevelText = "MAX";              // shown when already at max level

    private CardDatabase database;
    private int cardId = -1;
    private int level = 1;

    /// <summary>
    /// Bind this card instance to a definition id/level from the database.
    /// </summary>
    public void Bind(int id, int lvl, CardDatabase db)
    {
        database = db;
        cardId = id;
        level = Mathf.Max(1, lvl);
        Apply();
    }

    /// <summary>
    /// Re-applies current data to the visuals.
    /// </summary>
    public void Apply()
    {
        if (database == null) return;
        var def = database.Get(cardId);
        if (def == null) return;

        // Name
        if (cardNameText != null)
            cardNameText.text = def.cardName;

        // Description: prefer per-tier effectText when present, else base description
        string desc = def.description;
        var tier = def.GetTier(level);
        if (!string.IsNullOrWhiteSpace(tier.effectText))
            desc = tier.effectText;

        if (cardDescriptionText != null)
            cardDescriptionText.text = desc;

        // Front artwork (Sprite -> Texture2D)
        if (def.image != null && frontRenderer != null)
        {
            Texture2D tex = def.image.texture;
            if (tex != null)
            {
                // Make sure we don't accidentally mutate a shared material
                if (frontRenderer.material != null)
                    frontRenderer.material.mainTexture = tex;
            }
        }

        // Upgrade cost (cost to go FROM current level TO next level)
        if (upgradeCostText != null)
        {
            if (showUpgradeCost)
            {
                int cost = GetUpgradeCost(def, level);
                if (cost >= 0)
                    upgradeCostText.text = string.Format(upgradeCostFormat, cost);
                else
                    upgradeCostText.text = maxLevelText; // already at max
            }
            else
            {
                upgradeCostText.text = string.Empty;
            }
        }
    }

    /// <summary>
    /// Returns the gold required to upgrade from currentLevel to next level.
    /// If already at max, returns -1.
    /// </summary>
    private int GetUpgradeCost(CardDefinition def, int currentLevel)
    {
        int max = def.MaxLevel; // tiers length (index 0 = level 1)
        if (currentLevel >= max) return -1;
        // costGold is defined on the target tier (upgrade FROM previous TO this tier)
        var nextTier = def.GetTier(currentLevel + 1);
        return nextTier.costGold;
    }
}
