using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;

// Client-only picker that spawns 3 FULL-FRONT cards at the same place/layout as DraftDrawPicker.
// - Copies layout from any DraftDrawPicker found in the scene (offsets, spread, scale, pickLayer)
// - Uses the same hand anchor: TableSeatAnchors.Instance.GetHandAnchor(seatIndex)
// - Strips PinToAnchor / FloatingShowcase / CardView / HoverLift to prevent transform fights
// - Click sends chosen HAND INDEX via PlayerChestUpgrades and closes the picker
public class ChestUpgradePicker : MonoBehaviour
{
    private static ChestUpgradePicker _instance;

    // Default layout (will be overridden from DraftDrawPicker if present)
    public float centerOffsetX = -0.06f;
    public float centerOffsetY = 0.30f;
    public float centerOffsetZ = 0.00f;

    public float spreadX = 0.12f;
    public float spreadZ = 0.00f;

    public Vector3 cardLocalScale = new Vector3(0.05f, 0.05f, 0.05f);
    public Vector3 colliderWorldSize = new Vector3(0.18f, 0.26f, 0.05f);

    public bool overrideLayerForPicks = true;
    public int pickLayer = 0; // Default

    public LayerMask raycastMask = ~0;

    // Runtime
    private Transform anchor;
    private Camera cam;

    private GameObject groupRootGO;
    private GameObject[] spawned = null;
    private CardRaycasterOnRoot[] disabledRaycasters;

    private int[] choiceHandIndices = null;
    private int[] choiceCardIds = null;
    private int[] choiceLevels = null;

    private PlayerState ownerPs = null;
    private PlayerChestUpgrades ownerUpgrades = null;
    private bool active;

    // ---------- Public entry points ----------
    public static void ShowChoices(int[] cardIds, byte[] targetLevels, int[] handIndices)
    {
        if (_instance == null)
        {
            var go = new GameObject("~ChestUpgradePicker");
            _instance = go.AddComponent<ChestUpgradePicker>();
        }

        int n = Mathf.Min(3, targetLevels != null ? targetLevels.Length : 0);
        var lvls = new int[n];
        for (int i = 0; i < n; i++) lvls[i] = targetLevels[i];

        _instance.Open(cardIds, lvls, handIndices);
    }

    public static void HideChoices()
    {
        if (_instance != null) _instance.Close();
    }

    // ---------- Core flow ----------
    private void Open(int[] cardIds, int[] targetLevels, int[] handIndices)
    {
        Close();

        ownerPs = FindLocalPlayer();
        if (ownerPs == null)
        {
            Debug.LogWarning("[ChestUpgradePicker] Local PlayerState not found.");
            return;
        }
        ownerUpgrades = ownerPs.GetComponent<PlayerChestUpgrades>();
        if (ownerUpgrades == null)
        {
            Debug.LogWarning("[ChestUpgradePicker] PlayerChestUpgrades not found on local player.");
            return;
        }

        // Prefer the local player's camera; fallback to MainCamera
        cam = ownerPs.GetComponentInChildren<Camera>(true);
        if (cam == null) cam = Camera.main;

        // Use the SAME anchor as DraftDrawPicker (hand anchor)
        anchor = (TableSeatAnchors.Instance != null)
            ? TableSeatAnchors.Instance.GetHandAnchor(ownerPs.seatIndex)
            : GameObject.Find("HandAnchor_Seat0") != null ? GameObject.Find("HandAnchor_Seat0").transform : null;

        if (anchor == null)
        {
            Debug.LogWarning("[ChestUpgradePicker] No hand anchor found. Aborting.");
            return;
        }

        // Adopt layout from any scene DraftDrawPicker to match exact placement
        AdoptLayoutFromDraftPickerIfAvailable();

        GameObject cardPrefab = null;
        var hv = ownerPs.GetComponent<HandVisualizer>();
        if (hv != null) cardPrefab = hv.cardPrefab3D;
        if (cardPrefab == null)
        {
            Debug.LogWarning("[ChestUpgradePicker] No card prefab configured on HandVisualizer.");
            return;
        }

        if (overrideLayerForPicks && pickLayer >= 0 && pickLayer < 32)
        {
            int bit = 1 << pickLayer;
            if ((raycastMask.value & bit) == 0) raycastMask |= bit;
        }

        disabledRaycasters = FindAll<CardRaycasterOnRoot>();
        for (int i = 0; i < disabledRaycasters.Length; i++) disabledRaycasters[i].enabled = false;

        int n = Mathf.Min(3, cardIds != null ? cardIds.Length : 0);
        choiceHandIndices = new int[n];
        choiceCardIds = new int[n];
        choiceLevels = new int[n];
        for (int i = 0; i < n; i++)
        {
            choiceHandIndices[i] = handIndices[i];
            choiceCardIds[i] = cardIds[i];
            choiceLevels[i] = targetLevels[i];
        }

        groupRootGO = new GameObject("ChestUpgradePickRoot");
        groupRootGO.transform.SetParent(anchor, false);
        groupRootGO.transform.localPosition = new Vector3(centerOffsetX, centerOffsetY, centerOffsetZ);
        groupRootGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        groupRootGO.transform.localScale = Vector3.one;

        spawned = new GameObject[n];
        float mid = (n - 1) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var go = Object.Instantiate(cardPrefab, groupRootGO.transform, false);
            go.name = "ChestUpgradeChoice_hand" + handIndices[i] + "_card" + cardIds[i];

            if (overrideLayerForPicks && pickLayer >= 0 && pickLayer < 32)
                SetLayerRecursively(go, pickLayer);

            // IMPORTANT: remove any scripts that fight layout (same idea as DraftDrawPicker)
            StripIfPresent<PinToAnchor>(go);
            StripIfPresent<FloatingShowcase>(go);
            StripIfPresent<CardView>(go);
            StripIfPresent<HoverLift>(go);

            // Bind visuals at target level, set owner
            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null)
            {
                var db = (ownerPs.database != null) ? ownerPs.database : CardDatabase.Active;
                adapter.Bind(cardIds[i], Mathf.Max(1, choiceLevels[i]), db);
                adapter.SetOwner(ownerPs);
            }

            // Place like DraftDrawPicker
            Vector3 localPos = new Vector3((i - mid) * spreadX, 0f, (i - mid) * spreadZ);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = cardLocalScale;

            EnsureClickTarget(go);

            // Enforce preview visuals: "+1 lv", "+1", hide costs, use MaxLevelNotMyTurn frame
            var force = go.GetComponent<ForceCardUpgradePreview>();
            if (force == null) force = go.AddComponent<ForceCardUpgradePreview>();
            force.effectOverrideText = "+1 lv";
            force.levelBadgeOverrideText = "+1";
            force.hideCosts = true;
            force.useMaxLevelNotMyTurnMaterial = true;

            var tagger = go.GetComponent<ChestUpgradeChoice>();
            if (tagger == null) tagger = go.AddComponent<ChestUpgradeChoice>();
            tagger.handIndex = handIndices[i];

            spawned[i] = go;
        }

        active = true;
    }

    private void Close()
    {
        if (spawned != null)
        {
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null) DestroyImmediateOrRuntime(spawned[i]);
            }
            spawned = null;
        }

        if (groupRootGO != null)
        {
            DestroyImmediateOrRuntime(groupRootGO);
            groupRootGO = null;
        }

        if (disabledRaycasters != null)
        {
            for (int i = 0; i < disabledRaycasters.Length; i++)
                if (disabledRaycasters[i]) disabledRaycasters[i].enabled = true;
            disabledRaycasters = null;
        }

        active = false;
        anchor = null;
        ownerPs = null;
        ownerUpgrades = null;
        choiceHandIndices = null;
        choiceCardIds = null;
        choiceLevels = null;
        cam = null;
    }

    void Update()
    {
        if (!active) return;

        if (groupRootGO != null)
        {
            // Keep matching DraftDrawPicker behavior
            groupRootGO.transform.localPosition = new Vector3(centerOffsetX, centerOffsetY, centerOffsetZ);
            groupRootGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            groupRootGO.transform.localScale = Vector3.one;
            ApplyChildrenLayout();
        }

        var useCam = (cam != null ? cam : Camera.main);
        if (useCam == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = useCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, raycastMask, QueryTriggerInteraction.Collide);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    var choice = hits[h].transform.GetComponentInParent<ChestUpgradeChoice>();
                    if (choice != null)
                    {
                        if (ownerUpgrades != null)
                            ownerUpgrades.CmdChooseChestOption(choice.handIndex);

                        Close();
                        break;
                    }
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            int handIndex = ChooseRandomHandIndex();
            if (handIndex >= 0 && ownerUpgrades != null)
            {
                ownerUpgrades.CmdChooseChestOption(handIndex);
            }
            Close();
        }
    }

    private void ApplyChildrenLayout()
    {
        if (spawned == null || spawned.Length == 0 || groupRootGO == null) return;

        int n = spawned.Length;
        float mid = (n - 1) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var go = spawned[i];
            if (go == null) continue;

            Vector3 localPos = new Vector3((i - mid) * spreadX, 0f, (i - mid) * spreadZ);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = cardLocalScale;

            ResizeClickTarget(go);
        }
    }

    private void EnsureClickTarget(GameObject cardGO)
    {
        Transform t = cardGO.transform.Find("ClickTarget");
        GameObject clickGO;
        if (t == null)
        {
            clickGO = new GameObject("ClickTarget");
            clickGO.transform.SetParent(cardGO.transform, false);
            clickGO.transform.localPosition = Vector3.zero;
            clickGO.transform.localRotation = Quaternion.identity;
            clickGO.transform.localScale = Vector3.one;

            var box = clickGO.AddComponent<BoxCollider>();
            box.isTrigger = false;
        }
        else
        {
            clickGO = t.gameObject;
            if (clickGO.GetComponent<BoxCollider>() == null)
                clickGO.AddComponent<BoxCollider>();
        }

        ResizeClickTarget(cardGO);
    }

    private void ResizeClickTarget(GameObject cardGO)
    {
        var t = cardGO.transform.Find("ClickTarget");
        if (t == null) return;
        var box = t.GetComponent<BoxCollider>();
        if (box == null) return;

        box.center = Vector3.zero;
        box.size = colliderWorldSize;
    }

    // Read layout from any DraftDrawPicker instance in the scene
    private void AdoptLayoutFromDraftPickerIfAvailable()
    {
        var draft = FindFirst<DraftDrawPicker>();
        if (draft == null) return;

        this.centerOffsetX = draft.centerOffsetX;
        this.centerOffsetY = draft.centerOffsetY;
        this.centerOffsetZ = draft.centerOffsetZ;

        this.spreadX = draft.spreadX;
        this.spreadZ = draft.spreadZ;

        this.cardLocalScale = draft.cardLocalScale;
        this.colliderWorldSize = draft.colliderWorldSize;

        this.overrideLayerForPicks = draft.overrideLayerForPicks;
        this.pickLayer = draft.pickLayer;
    }

    // Helpers
    private static PlayerState FindLocalPlayer()
    {
        var arr = FindAll<PlayerState>();
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null && arr[i].isLocalPlayer) return arr[i];
        return null;
    }

    private int ChooseRandomHandIndex()
    {
        if (choiceHandIndices == null || choiceHandIndices.Length == 0) return -1;
        int k = Random.Range(0, choiceHandIndices.Length);
        return choiceHandIndices[k];
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }

    private static void DestroyImmediateOrRuntime(Object obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Object.DestroyImmediate(obj);
        else Object.Destroy(obj);
#else
        Object.Destroy(obj);
#endif
    }

    private static void StripIfPresent<T>(GameObject root) where T : Component
    {
        var arr = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < arr.Length; i++)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(arr[i]);
#else
            Object.Destroy(arr[i]);
#endif
        }
    }

    private static T[] FindAll<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>(true);
#endif
    }

    private static T FindFirst<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }
}
