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
    public MeshRenderer frontRenderer;      // art quad (card image)
    public MeshRenderer fullCardRenderer;   // whole card frame

    [Header("Full Card Materials (by turn)")]
    public Material fullCardMatMyTurn;
    public Material fullCardMatNotMyTurn;              // ← assign your "NOT MY Turn.mat" here

    [Header("Full Card Materials (MAX level, by turn)")]
    public Material fullCardMatMaxLevelMyTurn;
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
    [Tooltip("On owner's turn show CHIP only, off-turn show GOLD only.")]
    public bool toggleCostsByTurn = true;
    public bool showBothWhenNoOwner = true;

    static readonly int PROP_MAIN_TEX = Shader.PropertyToID("_MainTex");
    static readonly int PROP_BASE_MAP = Shader.PropertyToID("_BaseMap");
    MaterialPropertyBlock _mpb;

    PlayerState owner;
    CardView cv;
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

        // Always enforce capacity/turn overrides for SetReaction cards in hand
        ApplySetCardCapacityVisual(myTurn);
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

        // Also ensure correct capacity look on first bind
        ApplySetCardCapacityVisual(myTurn);
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

        if (cardNameText) cardNameText.text = def.cardName;
        if (cardDescriptionText) cardDescriptionText.text =
            string.IsNullOrEmpty(tier.effectText) ? def.description : tier.effectText;

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

        bool hasOwner = owner != null;
        bool myTurn = hasOwner && TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(owner);
        bool isMax = level >= def.MaxLevel;
        ApplyFullCardMaterial(myTurn, isMax);

        if (upgradeCostText)
        {
            int nextCost = GetUpgradeCost(def, level);
            upgradeCostText.text = (nextCost >= 0) ? string.Format(upgradeCostFormat, nextCost) : maxLevelText;
        }

        if (chipCostText)
        {
            int chip = def.GetCastChipCost(level);
            chipCostText.text = string.Format(chipCostFormat, chip);
        }

        if (levelText)
        {
            if (hideLevelWhenOne && level <= 1) levelText.text = string.Empty;
            else
            {
                string lvStr = useRomanNumerals ? ToRoman(level) : level.ToString();
                levelText.text = string.Format(levelFormat, lvStr);
            }
        }

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

        // ensure visual capacity rule applies on first bind too
        ApplySetCardCapacityVisual(myTurn);
    }

    // ───────── helpers ─────────

    void ApplyCostVisibility(bool myTurn, bool hasOwner, bool isMax)
    {
        // Your rule: show chips on owner's turn, gold off-turn.
        bool showGold = !myTurn;
        bool showChip = myTurn;

        if (!hasOwner && showBothWhenNoOwner)
        {
            showChip = true;
            showGold = true;
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

    /// Enforce special visuals for SetReaction cards in HAND:
    /// - NOT my turn: force NotMyTurn material (max variant if max level).
    /// - MY turn but I already have a set card: use disabled look (MaxLevelNotMyTurn) and hide chip cost.
    void ApplySetCardCapacityVisual(bool myTurn)
    {
        if (!fullCardRenderer) return;
        if (owner == null || database == null) return;

        var def = database.Get(cardId);
        if (def == null) return;

        var cvLocal = cv != null ? cv : GetComponent<CardView>();
        bool isInHand = cvLocal != null && cvLocal.isInHand;
        bool isSetCard = def.playStyle == CardDefinition.PlayStyle.SetReaction;
        if (!isInHand || !isSetCard) return;

        // compute isMax for proper material selection
        bool isMax = level >= def.MaxLevel;

        if (!myTurn)
        {
            // Your request: set cards (in hand) use the "NOT MY TURN" mat when it's not my turn
            Material m = isMax && fullCardMatMaxLevelNotMyTurn ? fullCardMatMaxLevelNotMyTurn : fullCardMatNotMyTurn;
            if (m) fullCardRenderer.sharedMaterial = m;

            // chip cost already hidden off-turn by ApplyCostVisibility
            return;
        }

        // My turn: if I already have a set card, show disabled look and hide chip cost
        bool alreadyHaveSet = owner.setIds.Count > 0;
        if (alreadyHaveSet)
        {
            if (fullCardMatMaxLevelNotMyTurn) fullCardRenderer.sharedMaterial = fullCardMatMaxLevelNotMyTurn;
            if (chipCostText) chipCostText.gameObject.SetActive(false);
        }
        // else: leave whatever material & cost visibility the normal turn-logic selected
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
