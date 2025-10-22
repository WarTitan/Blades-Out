using UnityEngine;
using TMPro;

// Forces preview visuals for a spawned card using Card3DAdapter.
// - Shows "+1 lv" in effect/description area
// - Shows "+1" on level badge
// - Hides any chip/gold cost texts
// - Uses MaxLevelNotMyTurn full-card material if available
public class ForceCardUpgradePreview : MonoBehaviour
{
    public string effectOverrideText = "+1 lv";
    public string levelBadgeOverrideText = "+1";
    public bool hideCosts = true;
    public bool useMaxLevelNotMyTurnMaterial = true;

    private Card3DAdapter adapter;

    void Awake()
    {
        adapter = GetComponent<Card3DAdapter>();
    }

    void LateUpdate()
    {
        if (adapter == null) return;

        // Text overrides
        if (adapter.cardDescriptionText != null)
            adapter.cardDescriptionText.text = effectOverrideText;

        if (adapter.levelText != null)
            adapter.levelText.text = levelBadgeOverrideText;

        // Hide costs
        if (hideCosts)
        {
            if (adapter.upgradeCostText != null)
                adapter.upgradeCostText.gameObject.SetActive(false);
            if (adapter.chipCostText != null)
                adapter.chipCostText.gameObject.SetActive(false);
        }

        // Force full-card material to "MaxLevelNotMyTurn"
        if (useMaxLevelNotMyTurnMaterial && adapter.fullCardRenderer != null)
        {
            var target = adapter.fullCardMatMaxLevelNotMyTurn != null
                ? adapter.fullCardMatMaxLevelNotMyTurn
                : adapter.fullCardMatNotMyTurn;

            if (target != null && adapter.fullCardRenderer.sharedMaterial != target)
                adapter.fullCardRenderer.sharedMaterial = target;
        }
    }
}
