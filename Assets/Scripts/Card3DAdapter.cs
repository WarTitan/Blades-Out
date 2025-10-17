using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class Card3DAdapter : MonoBehaviour
{
    [Header("Database Binding")]
    public CardDatabase database;
    public int cardId = -1;
    public int level = 1;

    [Header("Front Art (Quad)")]
    public MeshRenderer frontRenderer;

    [Header("Texts (UI or 3D TMP allowed)")]
    public TMP_Text cardNameText;
    public TMP_Text cardDescriptionText;

    [Header("Upgrade (Gold) Cost Display")]
    public TMP_Text upgradeCostText;
    public bool showUpgradeCost = true;
    public string upgradeCostFormat = "Upgrade: {0}g";
    public string maxLevelText = "MAX";

    public enum CostMode { NextLevel, SpecificTier }
    public CostMode costMode = CostMode.SpecificTier;
    public int specificTierLevel = 2;

    [Header("Chip (Cast) Cost Display")]
    [Tooltip("Text for the chip cost to CAST/SET this card. Uses tier.castChipCost if >0, else CardDefinition.chipCost.")]
    public TMP_Text chipCostText;
    public bool showChipCost = true;
    public bool hideZeroChipCost = false;
    public string chipCostFormat = "Cost: {0}c";

    [Header("Front Fitting (optional)")]
    public bool autoFitAspect = true;
    public float targetHeight = 1f;

    [Header("Showcase 3D Model (optional)")]
    public Transform showcaseAnchor;
    public GameObject fallbackShowcasePrefab;
    public Vector3 extraShowcaseOffset = Vector3.zero;
    public float extraShowcaseScale = 1f;
    public Vector3 showcaseInitialEuler = Vector3.zero;

    [Header("Showcase Normalization (optional)")]
    public Material fallbackShowcaseMaterial;
    public float targetShowcaseMaxSize = 0.25f;

    private GameObject spawnedShowcase;
    private bool createdAnchor = false;

    static readonly int PROP_MAIN_TEX = Shader.PropertyToID("_MainTex");
    static readonly int PROP_BASE_MAP = Shader.PropertyToID("_BaseMap");
    MaterialPropertyBlock _mpb;

    public void Bind(int id, int lvl, CardDatabase db)
    {
        cardId = id;
        level = Mathf.Max(1, lvl);
        database = db;
        Apply();
    }

    public void Bind(int id, int lvl)
    {
        cardId = id;
        level = Mathf.Max(1, lvl);
        Apply();
    }

    public void Apply()
    {
        if (database == null) return;
        var def = database.Get(cardId);
        if (def == null) return;

        // Name
        if (cardNameText != null) cardNameText.text = def.cardName;

        // Description (prefer tier.effectText if present)
        string desc = def.description;
        var tier = def.GetTier(level);
        if (!string.IsNullOrEmpty(tier.effectText)) desc = tier.effectText;
        if (cardDescriptionText != null) cardDescriptionText.text = desc;

        // Art
        if (def.image != null && frontRenderer != null)
        {
            var tex = def.image.texture;
            if (tex != null) SetFrontTexture(tex);
            if (autoFitAspect) FitFrontToSprite(def.image, frontRenderer.transform, targetHeight);
        }

        // ----- GOLD UPGRADE COST -----
        if (upgradeCostText != null)
        {
            if (!showUpgradeCost)
            {
                upgradeCostText.text = string.Empty;
            }
            else if (costMode == CostMode.NextLevel)
            {
                int cost = GetUpgradeCost(def, level);
                upgradeCostText.text = (cost >= 0) ? string.Format(upgradeCostFormat, cost) : maxLevelText;
            }
            else
            {
                int lvl = Mathf.Max(1, specificTierLevel);
                if (lvl <= def.MaxLevel)
                {
                    var t2 = def.GetTier(lvl);
                    upgradeCostText.text = string.Format(upgradeCostFormat, t2.costGold);
                }
                else
                {
                    upgradeCostText.text = string.Empty;
                    Debug.LogWarning("[Card3DAdapter] No tier " + lvl + " on '" + def.cardName + "' (MaxLevel=" + def.MaxLevel + "). Cost hidden.");
                }
            }
        }

        // ----- CHIP (CAST) COST BADGE -----
        if (chipCostText != null)
        {
            if (!showChipCost)
            {
                chipCostText.text = string.Empty;
            }
            else
            {
                // Prefer tier override; if 0, fallback to base definition chipCost
                int chipCost = (tier.castChipCost > 0) ? tier.castChipCost : Mathf.Max(0, def.chipCost);
                if (hideZeroChipCost && chipCost == 0)
                    chipCostText.text = string.Empty;
                else
                    chipCostText.text = string.Format(chipCostFormat, chipCost);
            }
        }

        // 3D showcase (optional)
        SpawnShowcase(def);
    }

    private void SetFrontTexture(Texture tex)
    {
        if (frontRenderer == null || tex == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        frontRenderer.GetPropertyBlock(_mpb, 0);

        var mat = frontRenderer.sharedMaterial;
        if (mat != null && mat.HasProperty(PROP_BASE_MAP))
            _mpb.SetTexture(PROP_BASE_MAP, tex);
        else
            _mpb.SetTexture(PROP_MAIN_TEX, tex);

        frontRenderer.SetPropertyBlock(_mpb, 0);
    }

    private void SpawnShowcase(CardDefinition def)
    {
        if (spawnedShowcase != null)
        {
            Destroy(spawnedShowcase);
            spawnedShowcase = null;
        }

        GameObject prefab = def.showcasePrefab != null ? def.showcasePrefab : fallbackShowcasePrefab;
        if (prefab == null) return;

        if (showcaseAnchor == null)
        {
            var go = new GameObject("ShowcaseAnchor");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            showcaseAnchor = go.transform;
            createdAnchor = true;
        }
        else createdAnchor = false;

        spawnedShowcase = Instantiate(prefab, showcaseAnchor);
        spawnedShowcase.transform.localPosition = Vector3.zero;
        spawnedShowcase.transform.localRotation = Quaternion.identity;
        spawnedShowcase.transform.localScale = Vector3.one;

        NormalizeShowcase(spawnedShowcase, targetShowcaseMaxSize, fallbackShowcaseMaterial);

        Vector3 defOffset = def.showcaseLocalOffset;
        float defScale = (def.showcaseLocalScale <= 0f ? 1f : def.showcaseLocalScale);
        float s = Mathf.Max(0.0001f, defScale * Mathf.Max(0.0001f, extraShowcaseScale));

        spawnedShowcase.transform.localPosition += defOffset + extraShowcaseOffset;
        spawnedShowcase.transform.localScale *= s;
        spawnedShowcase.transform.localRotation = Quaternion.Euler(showcaseInitialEuler);
    }

    private void NormalizeShowcase(GameObject go, float targetMaxSize, Material fallbackMat)
    {
        if (!go) return;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("[Card3DAdapter] Showcase '" + go.name + "' has NO Renderer in children.");
            return;
        }

        if (fallbackMat != null)
        {
            foreach (var r in renderers)
            {
                if (r.sharedMaterials == null || r.sharedMaterials.Length == 0)
                    r.sharedMaterial = fallbackMat;
            }
        }

        if (targetMaxSize <= 0f) return;

        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var r in renderers)
        {
            if (!r.enabled) continue;
            if (!hasBounds) { b = r.bounds; hasBounds = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hasBounds) return;

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

    private int GetUpgradeCost(CardDefinition def, int currentLevel)
    {
        if (currentLevel >= def.MaxLevel) return -1;
        var next = def.GetTier(currentLevel + 1);
        return next.costGold;
    }

    private void FitFrontToSprite(Sprite s, Transform quad, float desiredHeight)
    {
        if (s == null || quad == null || desiredHeight <= 0f) return;
        float w = s.rect.width, h = s.rect.height;
        if (h <= 0f) return;
        float aspect = w / h;
        quad.localScale = new Vector3(desiredHeight * aspect, desiredHeight, quad.localScale.z);
    }

    public void ClearShowcase()
    {
        if (spawnedShowcase != null) { Destroy(spawnedShowcase); spawnedShowcase = null; }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (database != null && cardId >= 0) Apply();
        }
    }
#endif
}
