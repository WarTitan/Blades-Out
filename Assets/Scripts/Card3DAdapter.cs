using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class Card3DAdapter : MonoBehaviour
{
    [Header("Bind")]
    public CardDatabase database;
    public int cardId = -1;
    public int level = 1;

    [Header("Renderers")]
    [Tooltip("Renders the CARD ART sprite (the picture).")]
    public MeshRenderer frontRenderer;

    [Tooltip("Renders the FULL CARD (frame/skin). We will swap its MATERIAL by turn/max level.")]
    public MeshRenderer fullCardRenderer;

    [Header("Full Card Materials (by turn)")]
    [Tooltip("Material used when it's the owner's turn (e.g., 'Chip' frame).")]
    public Material fullCardMatMyTurn;

    [Tooltip("Material used when it's NOT the owner's turn (e.g., 'Gold' frame).")]
    public Material fullCardMatNotMyTurn;

    [Header("Full Card Materials (MAX level)")]
    [Tooltip("Material used when the card is MAX level AND it's the owner's turn.")]
    public Material fullCardMatMaxLevelMyTurn;

    [Tooltip("Material used when the card is MAX level AND it's NOT the owner's turn.")]
    public Material fullCardMatMaxLevelNotMyTurn;

    [Header("Texts")]
    public TMP_Text cardNameText;
    public TMP_Text cardDescriptionText;

    [Header("Upgrade (Gold) Cost")]
    public TMP_Text upgradeCostText;
    public string upgradeCostFormat = "Upgrade: {0}g";
    public string maxLevelText = "MAX";

    [Header("Chip (Cast) Cost")]
    public TMP_Text chipCostText;
    public string chipCostFormat = "Cost: {0}c";

    [Header("Level Badge")]
    public TMP_Text levelText;
    public string levelFormat = "Lv {0}";
    public bool hideLevelWhenOne = false;
    public bool useRomanNumerals = false;

    [Header("Visibility Rules")]
    [Tooltip("When true: on owner's turn show CHIP only, off-turn show GOLD only.")]
    public bool toggleCostsByTurn = true;

    [Tooltip("If no owner is found yet, show both costs (useful while spawning).")]
    public bool showBothWhenNoOwner = true;

    // shader props
    static readonly int PROP_MAIN_TEX = Shader.PropertyToID("_MainTex");
    static readonly int PROP_BASE_MAP = Shader.PropertyToID("_BaseMap");
    MaterialPropertyBlock _mpb;

    // owner detection (via CardView or parent PlayerState)
    PlayerState owner;
    CardView cv;

    // state cache to avoid redundant work
    bool? lastMyTurn = null;
    bool lastWasMax = false;

    void Awake()
    {
        cv = GetComponentInParent<CardView>(true);
        TryResolveOwner();
    }

    void OnEnable()
    {
        TryResolveOwner();
        lastMyTurn = null;
        lastWasMax = false;
        LateApplyTurnVisuals();
    }

    void Update()
    {
        if (owner == null) TryResolveOwner();
        if (!toggleCostsByTurn && fullCardRenderer == null) return;

        bool hasOwner = owner != null;
        bool myTurn = hasOwner && TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(owner);

        bool isMax = false;
        var def = (database != null) ? database.Get(cardId) : null;
        if (def != null) isMax = level >= def.MaxLevel;

        if (lastMyTurn == null || lastMyTurn.Value != myTurn || lastWasMax != isMax)
        {
            lastMyTurn = myTurn;
            lastWasMax = isMax;
            ApplyCostVisibility(myTurn, hasOwner, isMax);
            ApplyFullCardMaterial(myTurn, isMax);
        }
    }

    void TryResolveOwner()
    {
        if (owner != null) return;

        if (cv == null) cv = GetComponentInParent<CardView>(true);
        if (cv != null && cv.owner != null)
        {
            owner = cv.owner;
            LateApplyTurnVisuals();
        }
        else
        {
            var ps = GetComponentInParent<PlayerState>(true);
            if (ps != null)
            {
                owner = ps;
                LateApplyTurnVisuals();
            }
        }
    }

    void LateApplyTurnVisuals()
    {
        bool hasOwner = owner != null;
        bool myTurn = hasOwner && TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(owner);

        var def = (database != null) ? database.Get(cardId) : null;
        bool isMax = def != null && level >= def.MaxLevel;

        ApplyCostVisibility(myTurn, hasOwner, isMax);
        ApplyFullCardMaterial(myTurn, isMax);
        lastMyTurn = myTurn;
        lastWasMax = isMax;
    }

    // ───────── Public API ─────────
    public void SetOwner(PlayerState ps)
    {
        owner = ps;
        LateApplyTurnVisuals();
    }

    public void SetLevel(int lvl)
    {
        level = Mathf.Max(1, lvl);
        Apply();
    }

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
        if (database == null) { Debug.LogWarning("[Card3DAdapter] No database."); return; }
        var def = database.Get(cardId);
        if (def == null) { Debug.LogWarning("[Card3DAdapter] No definition for cardId " + cardId); return; }
        var tier = def.GetTier(level);

        // Name / Description (prefer per-tier effectText)
        if (cardNameText) cardNameText.text = def.cardName;
        if (cardDescriptionText) cardDescriptionText.text =
            string.IsNullOrEmpty(tier.effectText) ? def.description : tier.effectText;

        // ART (front image)
        if (def.image != null && frontRenderer != null)
        {
            var tex = def.image.texture;
            if (!SetTextureOn(frontRenderer, tex))
            {
                var mat = frontRenderer.material;
                if (mat != null) mat.mainTexture = tex;
            }
            FitFrontToSprite(def.image, frontRenderer.transform, 1f);
            frontRenderer.enabled = true;
        }
        else if (frontRenderer != null)
        {
            Debug.LogWarning("[Card3DAdapter] No sprite image or frontRenderer not assigned.");
        }

        // FULL CARD MATERIAL initial apply
        bool hasOwner = owner != null;
        bool myTurn = hasOwner && TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(owner);
        bool isMax = level >= def.MaxLevel;
        ApplyFullCardMaterial(myTurn, isMax);

        // COST TEXTS (values)
        if (upgradeCostText)
        {
            int nextCost = GetUpgradeCost(def, level);
            upgradeCostText.text = (nextCost >= 0) ? string.Format(upgradeCostFormat, nextCost) : maxLevelText;
        }

        if (chipCostText)
        {
            int chip = def.GetCastChipCost(level); // current level only
            chipCostText.text = string.Format(chipCostFormat, chip);
        }

        // LEVEL BADGE
        if (levelText)
        {
            if (hideLevelWhenOne && level <= 1)
            {
                levelText.text = string.Empty;
            }
            else
            {
                string lvStr = useRomanNumerals ? ToRoman(level) : level.ToString();
                levelText.text = string.Format(levelFormat, lvStr);
            }
        }

        // COST VISIBILITY
        if (toggleCostsByTurn)
        {
            ApplyCostVisibility(myTurn, hasOwner, isMax);
            lastMyTurn = myTurn;
            lastWasMax = isMax;
        }
        else
        {
            if (upgradeCostText) upgradeCostText.gameObject.SetActive(true);
            if (chipCostText) chipCostText.gameObject.SetActive(true);
        }
    }

    // ───────── helpers ─────────

    void ApplyCostVisibility(bool myTurn, bool hasOwner, bool isMax)
    {
        bool showGold = !myTurn;   // still show gold off-turn, even at max (label prints "MAX")
        bool showChip = myTurn;

        if (!hasOwner && showBothWhenNoOwner)
        {
            showChip = true; showGold = true;
        }

        if (chipCostText) chipCostText.gameObject.SetActive(showChip);
        if (upgradeCostText) upgradeCostText.gameObject.SetActive(showGold);
    }

    void ApplyFullCardMaterial(bool myTurn, bool isMax)
    {
        if (!fullCardRenderer) return;

        Material target = null;

        if (isMax)
        {
            // Prefer dedicated max-level materials; fall back to normal per-turn mats.
            target = myTurn
                ? (fullCardMatMaxLevelMyTurn != null ? fullCardMatMaxLevelMyTurn : fullCardMatMyTurn)
                : (fullCardMatMaxLevelNotMyTurn != null ? fullCardMatMaxLevelNotMyTurn : fullCardMatNotMyTurn);
        }
        else
        {
            target = myTurn ? fullCardMatMyTurn : fullCardMatNotMyTurn;
        }

        if (target != null)
        {
            fullCardRenderer.sharedMaterial = target;
            fullCardRenderer.enabled = true;
        }
    }

    bool SetTextureOn(MeshRenderer r, Texture tex)
    {
        if (!r || tex == null) return false;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(_mpb, 0);

        var mat = r.sharedMaterial;
        if (mat != null && mat.HasProperty(PROP_BASE_MAP))
            _mpb.SetTexture(PROP_BASE_MAP, tex);
        else
            _mpb.SetTexture(PROP_MAIN_TEX, tex);

        r.SetPropertyBlock(_mpb, 0);
        return true;
    }

    int GetUpgradeCost(CardDefinition def, int currentLevel)
    {
        if (currentLevel >= def.MaxLevel) return -1;
        return def.GetTier(currentLevel + 1).costGold;
    }

    void FitFrontToSprite(Sprite s, Transform quad, float desiredHeight)
    {
        if (s == null || quad == null || desiredHeight <= 0f) return;
        float w = s.rect.width, h = s.rect.height;
        if (h <= 0f) return;
        float aspect = w / h;
        var sc = quad.localScale;
        quad.localScale = new Vector3(desiredHeight * aspect, desiredHeight, sc.z);
    }

    string ToRoman(int n)
    {
        if (n <= 0) return "0";
        if (n > 3999) n = 3999;
        (int val, string sym)[] map = new (int, string)[]
        {
            (1000,"M"),(900,"CM"),(500,"D"),(400,"CD"),
            (100,"C"),(90,"XC"),(50,"L"),(40,"XL"),
            (10,"X"),(9,"IX"),(5,"V"),(4,"IV"),(1,"I")
        };
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var (v, s) in map)
            while (n >= v) { sb.Append(s); n -= v; }
        return sb.ToString();
    }
}
