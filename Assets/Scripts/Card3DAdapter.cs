using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class Card3DAdapter : MonoBehaviour
{
    [Header("Database Binding")]
    [Tooltip("Card database that contains definitions for all cards.")]
    public CardDatabase database;
    [Tooltip("Definition ID for this card (assigned via Bind).")]
    public int cardId = -1;
    [Tooltip("Current card level (1-based).")]
    public int level = 1;

    [Header("Front Art (Quad)")]
    [Tooltip("Renderer on your QuadFront (optional but recommended).")]
    public MeshRenderer frontRenderer;

    [Header("Texts (UI or 3D TMP allowed)")]
    public TMP_Text cardNameText;
    public TMP_Text cardDescriptionText;
    public TMP_Text upgradeCostText;

    [Header("Upgrade Cost Display")]
    public bool showUpgradeCost = true;
    [Tooltip("Text format for showing cost, e.g. \"Upgrade: {0}g\".")]
    public string upgradeCostFormat = "Upgrade: {0}g";
    [Tooltip("Text to show when already at max level (NextLevel mode).")]
    public string maxLevelText = "MAX";

    public enum CostMode { NextLevel, SpecificTier }
    [Tooltip("NextLevel = cost to upgrade from current level to next. SpecificTier = show a fixed tier's cost.")]
    public CostMode costMode = CostMode.SpecificTier;
    [Tooltip("When CostMode = SpecificTier, which tier level (1-based) to show.")]
    public int specificTierLevel = 2;

    [Header("Front Fitting (optional)")]
    [Tooltip("Auto-scale QuadFront's local X/Y to match sprite aspect.")]
    public bool autoFitAspect = true;
    [Tooltip("Target local height for QuadFront when auto-fitting.")]
    public float targetHeight = 1f;

    [Header("Showcase 3D Model")]
    [Tooltip("Optional: where to place the 3D model. If null, one is created as a child and left alone.")]
    public Transform showcaseAnchor;
    [Tooltip("Fallback model if the definition/tier has no showcasePrefab.")]
    public GameObject fallbackShowcasePrefab;
    [Tooltip("Extra local offset added on top of definition's offset (X/Z along anchor plane, Y = up).")]
    public Vector3 extraShowcaseOffset = Vector3.zero;
    [Tooltip("Extra scale multiplier for the showcase model.")]
    public float extraShowcaseScale = 1f;
    [Tooltip("Starting local rotation (Euler) for the showcase model.")]
    public Vector3 showcaseInitialEuler = Vector3.zero;

    [Header("Showcase Anchor Control")]
    [Tooltip("If true and we CREATE an anchor, set it upright (local up). Assigned anchors are never touched.")]
    public bool orientCreatedAnchorUpright = true;
    [Tooltip("If true, we keep re-orienting a CREATED anchor each Apply. Leave OFF to tweak it at runtime.")]
    public bool keepCreatedAnchorUprightEachApply = false;

    [Header("Showcase Normalization (optional)")]
    [Tooltip("If assigned, used when the spawned model has no materials.")]
    public Material fallbackShowcaseMaterial;
    [Tooltip("Largest local dimension (meters) to normalize the model to. 0 = disable normalize.")]
    public float targetShowcaseMaxSize = 0.25f;

    private GameObject spawnedShowcase;
    private bool createdAnchor = false;

    /// <summary>Bind this instance to an id/level from the database.</summary>
    public void Bind(int id, int lvl, CardDatabase db)
    {
        cardId = id;
        level = Mathf.Max(1, lvl);
        database = db;
        Apply();
    }

    /// <summary>Re-apply data to visuals (safe to call after fields change).</summary>
    public void Apply()
    {
        if (database == null) return;
        var def = database.Get(cardId);
        if (def == null) return;

        // Name
        if (cardNameText != null) cardNameText.text = def.cardName;

        // Description (prefer tier.effectText if provided)
        string desc = def.description;
        var tier = def.GetTier(level); // struct; effectText may be null/empty
        if (!string.IsNullOrEmpty(tier.effectText)) desc = tier.effectText;
        if (cardDescriptionText != null) cardDescriptionText.text = desc;

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
            else if (costMode == CostMode.NextLevel)
            {
                int cost = GetUpgradeCost(def, level);
                upgradeCostText.text = (cost >= 0)
                    ? string.Format(upgradeCostFormat, cost)
                    : maxLevelText;
            }
            else
            {
                int lvl = Mathf.Max(1, specificTierLevel);
                if (lvl <= def.MaxLevel)
                {
                    var t2 = def.GetTier(lvl);
                    upgradeCostText.text = string.Format(upgradeCostFormat, t2.costGold);
                }
                else upgradeCostText.text = string.Empty;
            }
        }

        // Showcase 3D model
        SpawnShowcase(def);
    }

    /// <summary>Spawn/replace the floating 3D model. If you assigned an anchor, we won't move it.</summary>
    private void SpawnShowcase(CardDefinition def)
    {
        // Clean previous
        if (spawnedShowcase != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(spawnedShowcase);
            else Destroy(spawnedShowcase);
#else
            Destroy(spawnedShowcase);
#endif
            spawnedShowcase = null;
        }

        // Resolve prefab (definition wins; else fallback)
        GameObject prefab = def.showcasePrefab != null ? def.showcasePrefab : fallbackShowcasePrefab;
        if (prefab == null) return;

        // Ensure/prepare anchor:
        //  - If you assigned one in Inspector, we RESPECT its transform (no changes).
        //  - If none assigned, create it ONCE as a child so you can move it later.
        if (showcaseAnchor == null)
        {
            var go = new GameObject("ShowcaseAnchor");
            go.transform.SetParent(transform, false);             // local to the card
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            if (orientCreatedAnchorUpright)
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f); // upright in card local space
            showcaseAnchor = go.transform;
            createdAnchor = true;
        }
        else
        {
            createdAnchor = false; // user-provided anchor → don't touch it
        }

        // Optional: if we created it and you want it kept upright every Apply, re-orient it
        if (createdAnchor && keepCreatedAnchorUprightEachApply && orientCreatedAnchorUpright)
        {
            showcaseAnchor.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        // Spawn under the anchor
        spawnedShowcase = Instantiate(prefab, showcaseAnchor);

        // 1) Baseline
        spawnedShowcase.transform.localPosition = Vector3.zero;
        spawnedShowcase.transform.localRotation = Quaternion.identity;
        spawnedShowcase.transform.localScale = Vector3.one;

        // 2) Normalize first (so later scales/offsets stick)
        NormalizeShowcase(spawnedShowcase, targetShowcaseMaxSize, fallbackShowcaseMaterial);

        // 3) Apply your data-driven offset/scale/rotation (relative to the anchor)
        Vector3 defOffset = def.showcaseLocalOffset;
        float defScale = (def.showcaseLocalScale <= 0f ? 1f : def.showcaseLocalScale);
        float s = Mathf.Max(0.0001f, defScale * Mathf.Max(0.0001f, extraShowcaseScale));

        spawnedShowcase.transform.localPosition += defOffset + extraShowcaseOffset;
        spawnedShowcase.transform.localScale *= s;
        spawnedShowcase.transform.localRotation = Quaternion.Euler(showcaseInitialEuler);

        // 4) Spin + bob behaviour (auto-add if missing)
        var floater = spawnedShowcase.GetComponent<global::FloatingShowcase>();
        if (floater == null) floater = spawnedShowcase.AddComponent<global::FloatingShowcase>();
    }

    /// <summary>
    /// Assign fallback material if needed and auto-scale spawned model to a reasonable size.
    /// </summary>
    private void NormalizeShowcase(GameObject go, float targetMaxSize, Material fallbackMat)
    {
        if (!go) return;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[Card3DAdapter] Showcase '{go.name}' has NO Renderer in children.");
            return;
        }

        // Assign fallback material on renderers that have none
        if (fallbackMat != null)
        {
            foreach (var r in renderers)
            {
                if (r.sharedMaterials == null || r.sharedMaterials.Length == 0)
                    r.sharedMaterial = fallbackMat;
            }
        }

        if (targetMaxSize <= 0f) return; // skip autoscale if disabled

        // Build bounds in world space (enabled renderers only)
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var r in renderers)
        {
            if (!r.enabled) continue;
            if (!hasBounds) { b = r.bounds; hasBounds = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hasBounds) return;

        // Convert world bounds to local by dividing by parent lossyScale
        var parent = go.transform.parent;
        Vector3 parentScale = parent ? parent.lossyScale : Vector3.one;
        Vector3 localSize = new Vector3(
            b.size.x / Mathf.Max(0.0001f, parentScale.x),
            b.size.y / Mathf.Max(0.0001f, parentScale.y),
            b.size.z / Mathf.Max(0.0001f, parentScale.z)
        );

        float maxDim = Mathf.Max(localSize.x, Mathf.Abs(localSize.y), localSize.z);
        if (maxDim > 0.0001f)
        {
            float mul = targetMaxSize / maxDim;
            go.transform.localScale = go.transform.localScale * mul;
        }
    }

    /// <summary>Cost to upgrade from currentLevel -> next level. Returns -1 if already max.</summary>
    private int GetUpgradeCost(CardDefinition def, int currentLevel)
    {
        if (currentLevel >= def.MaxLevel) return -1;
        var next = def.GetTier(currentLevel + 1);
        return next.costGold;
    }

    /// <summary>Fit the QuadFront's local scale to the sprite's aspect (width/height). Keeps Z scale unchanged.</summary>
    private void FitFrontToSprite(Sprite s, Transform quad, float desiredHeight)
    {
        if (s == null || quad == null || desiredHeight <= 0f) return;
        float w = s.rect.width, h = s.rect.height;
        if (h <= 0f) return;
        float aspect = w / h;
        quad.localScale = new Vector3(desiredHeight * aspect, desiredHeight, quad.localScale.z);
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
