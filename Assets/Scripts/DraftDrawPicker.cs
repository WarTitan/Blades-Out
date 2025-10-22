using UnityEngine;
using UnityEngine.EventSystems;

[AddComponentMenu("Cards/Draft Draw Picker (Client)")]
public class DraftDrawPicker : MonoBehaviour
{
    private static DraftDrawPicker _instance;

    // Boot preferences
    public bool useSceneInstanceIfAvailable = true;
    public bool persistSettingsIfAutoCreated = true;

    // Config
    public float centerOffsetX = -0.06f;
    public float centerOffsetY = 0.30f;
    public float centerOffsetZ = 0.00f;

    public float spreadX = 0.12f;
    public float spreadZ = 0.00f;

    public Vector3 cardLocalScale = new Vector3(0.05f, 0.05f, 0.05f);
    public Vector3 colliderWorldSize = new Vector3(0.18f, 0.26f, 0.05f);

    public bool overrideLayerForPicks = true;
    public int pickLayer = 0; // Default

    public Camera cameraOverride = null;
    public LayerMask raycastMask = ~0;

    public bool stripPinToAnchorOnChildren = true;
    public bool stripFloatingShowcaseOnChildren = true;
    public bool stripCardViewOnChildren = true;

    // Runtime
    private DraftDrawNet ownerNet;
    private Transform anchor;
    private Camera cam;

    private GameObject groupRootGO;

    private GameObject[] spawned = null;
    private CardRaycasterOnRoot[] disabledRaycasters;
    private int[] choiceIds = null;
    private PlayerState ownerPs = null;

    private bool active;
    private bool warnedNoCamera = false;
    private bool autoCreatedThisRun = false;

    private const string PP_KEY = "DraftDrawPicker.Settings.v1";

    // Public entry points
    public static void ShowChoices(DraftDrawNet ownerNet, int[] ids)
    {
        if (_instance == null)
        {
            DraftDrawPicker existing = null;
#if UNITY_2023_1_OR_NEWER
            existing = UnityEngine.Object.FindFirstObjectByType<DraftDrawPicker>(FindObjectsInactive.Include);
#else
            existing = UnityEngine.Object.FindObjectOfType<DraftDrawPicker>(true);
#endif
            if (existing != null && existing.useSceneInstanceIfAvailable)
            {
                _instance = existing;
                if (!_instance.gameObject.activeSelf) _instance.gameObject.SetActive(true);
                _instance.autoCreatedThisRun = false;
            }
            else
            {
                var go = new GameObject("~DraftDrawPicker");
                _instance = go.AddComponent<DraftDrawPicker>();
                _instance.autoCreatedThisRun = true;
                if (_instance.persistSettingsIfAutoCreated)
                    _instance.LoadSettingsIfAvailable();
            }
        }

        _instance.Open(ownerNet, ids);
    }

    public static void HideChoices()
    {
        if (_instance != null) _instance.Close();
    }

    // Core flow
    private void Open(DraftDrawNet owner, int[] ids)
    {
        Close();

        ownerNet = owner;
        if (ownerNet == null) return;

        ownerPs = ownerNet.GetComponent<PlayerState>();
        if (ownerPs == null) return;

        anchor = TableSeatAnchors.Instance
            ? TableSeatAnchors.Instance.GetHandAnchor(ownerPs.seatIndex)
            : GameObject.Find("HandAnchor_Seat0")?.transform;

        if (anchor == null)
        {
            Debug.LogWarning("[DraftDrawPicker] No hand anchor found. Aborting.");
            return;
        }

        GameObject cardPrefab = null;
        var hv = ownerNet.GetComponent<HandVisualizer>();
        if (hv != null) cardPrefab = hv.cardPrefab3D;
        if (cardPrefab == null)
        {
            Debug.LogWarning("[DraftDrawPicker] No card prefab configured on HandVisualizer.");
            return;
        }

        if (overrideLayerForPicks && pickLayer >= 0 && pickLayer < 32)
        {
            int bit = 1 << pickLayer;
            if ((raycastMask.value & bit) == 0) raycastMask |= bit;
        }

#if UNITY_2023_1_OR_NEWER
        disabledRaycasters = UnityEngine.Object.FindObjectsByType<CardRaycasterOnRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        disabledRaycasters = UnityEngine.Object.FindObjectsOfType<CardRaycasterOnRoot>(true);
#endif
        for (int i = 0; i < disabledRaycasters.Length; i++) disabledRaycasters[i].enabled = false;

        int n = Mathf.Min(3, (ids != null ? ids.Length : 0));
        choiceIds = new int[n];
        for (int i = 0; i < n; i++) choiceIds[i] = ids[i];

        groupRootGO = new GameObject("DraftPickRoot");
        groupRootGO.transform.SetParent(anchor, false);
        groupRootGO.transform.localPosition = new Vector3(centerOffsetX, centerOffsetY, centerOffsetZ);
        groupRootGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        groupRootGO.transform.localScale = Vector3.one;

        spawned = new GameObject[n];

        float mid = (n - 1) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var go = UnityEngine.Object.Instantiate(cardPrefab, groupRootGO.transform, false);
            go.name = "DraftChoice_" + ids[i];

            if (overrideLayerForPicks && pickLayer >= 0 && pickLayer < 32)
                SetLayerRecursively(go, pickLayer);

            if (stripPinToAnchorOnChildren)
            {
                var pins = go.GetComponentsInChildren<PinToAnchor>(true);
                for (int p = 0; p < pins.Length; p++)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(pins[p]);
#else
                    UnityEngine.Object.Destroy(pins[p]);
#endif
                }
            }
            if (stripFloatingShowcaseOnChildren)
            {
                var floats = go.GetComponentsInChildren<FloatingShowcase>(true);
                for (int f = 0; f < floats.Length; f++)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(floats[f]);
#else
                    UnityEngine.Object.Destroy(floats[f]);
#endif
                }
            }
            if (stripCardViewOnChildren)
            {
                var cvs = go.GetComponentsInChildren<CardView>(true);
                for (int c = 0; c < cvs.Length; c++)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(cvs[c]);
#else
                    UnityEngine.Object.Destroy(cvs[c]);
#endif
                }
            }

            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null)
            {
                var db = (ownerPs.database != null) ? ownerPs.database : CardDatabase.Active;
                adapter.Bind(ids[i], 1, db);
                adapter.SetOwner(ownerPs);
            }

            Vector3 localPos = new Vector3((i - mid) * spreadX, 0f, (i - mid) * spreadZ);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = cardLocalScale;

            EnsureClickTarget(go);

            var tagger = go.GetComponent<DraftPickChoice>();
            if (tagger == null) tagger = go.AddComponent<DraftPickChoice>();
            tagger.cardId = ids[i];

            spawned[i] = go;
        }

        active = true;
        warnedNoCamera = false;
    }

    private void Close()
    {
        if (spawned != null)
        {
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(spawned[i]);
#else
                    UnityEngine.Object.Destroy(spawned[i]);
#endif
                }
            }
            spawned = null;
        }

        if (groupRootGO != null)
        {
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(groupRootGO);
#else
            UnityEngine.Object.Destroy(groupRootGO);
#endif
            groupRootGO = null;
        }

        if (disabledRaycasters != null)
        {
            for (int i = 0; i < disabledRaycasters.Length; i++)
                if (disabledRaycasters[i]) disabledRaycasters[i].enabled = true;
            disabledRaycasters = null;
        }

        if (autoCreatedThisRun && persistSettingsIfAutoCreated)
            SaveSettings();

        active = false;
        ownerNet = null;
        anchor = null;
        choiceIds = null;
        ownerPs = null;
    }

    void Update()
    {
        if (!active) return;
        if (ownerNet == null) { Close(); return; }

        if (groupRootGO != null)
        {
            groupRootGO.transform.localPosition = new Vector3(centerOffsetX, centerOffsetY, centerOffsetZ);
            groupRootGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            groupRootGO.transform.localScale = Vector3.one;
            ApplyChildrenLayout();
        }

        cam = GetRaycastCamera();
        if (cam == null)
        {
            if (!warnedNoCamera)
            {
                Debug.LogWarning("[DraftDrawPicker] No active camera found for raycasts. Tag your gameplay camera as MainCamera or assign cameraOverride.");
                warnedNoCamera = true;
            }
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, raycastMask, QueryTriggerInteraction.Collide);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    var choice = hits[h].transform.GetComponentInParent<DraftPickChoice>();
                    if (choice != null)
                    {
                        if (ownerNet != null) ownerNet.Cmd_ChooseDraftCard(choice.cardId);
                        Close();
                        break;
                    }
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            int randomId = ChooseRandomCardId();
            if (randomId >= 0 && ownerNet != null)
            {
                ownerNet.Cmd_ChooseDraftCard(randomId);
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
            var existing = clickGO.GetComponent<BoxCollider>();
            if (existing == null) clickGO.AddComponent<BoxCollider>();
        }

        if (overrideLayerForPicks && pickLayer >= 0 && pickLayer < 32)
            SetLayerRecursively(clickGO, pickLayer);

        ResizeClickTarget(cardGO);
    }

    private void ResizeClickTarget(GameObject cardGO)
    {
        Transform t = cardGO.transform.Find("ClickTarget");
        if (t == null) return;

        var box = t.GetComponent<BoxCollider>();
        if (box == null) return;

        Vector3 lossy = t.lossyScale;
        float sx = Mathf.Max(0.0001f, lossy.x);
        float sy = Mathf.Max(0.0001f, lossy.y);
        float sz = Mathf.Max(0.0001f, lossy.z);
        box.size = new Vector3(
            colliderWorldSize.x / sx,
            colliderWorldSize.y / sy,
            colliderWorldSize.z / sz
        );
        box.center = Vector3.zero;
        box.isTrigger = false;
        box.enabled = true;
    }

    private Camera GetRaycastCamera()
    {
        if (cameraOverride != null && cameraOverride.isActiveAndEnabled) return cameraOverride;

        Camera m = Camera.main;
        if (m != null && m.isActiveAndEnabled) return m;

        Camera[] cams = Camera.allCameras;
        Camera best = null;
        float bestDepth = float.NegativeInfinity;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null) continue;
            if (!c.isActiveAndEnabled) continue;
            if (c.targetDisplay != 0) continue;
            if (c.depth >= bestDepth)
            {
                best = c;
                bestDepth = c.depth;
            }
        }
        return best;
    }

    private int ChooseRandomCardId()
    {
        if (choiceIds == null || choiceIds.Length == 0) return -1;
        int idx = Random.Range(0, choiceIds.Length);
        return choiceIds[idx];
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }

    // Persistence helpers
    [System.Serializable]
    private struct Saved
    {
        public float cx, cy, cz;
        public float sx, sz;
        public Vector3 scl;
        public bool ol;
        public int pl;
    }

    private void LoadSettingsIfAvailable()
    {
        if (!PlayerPrefs.HasKey(PP_KEY)) return;
        string json = PlayerPrefs.GetString(PP_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            Saved s = JsonUtility.FromJson<Saved>(json);
            centerOffsetX = s.cx;
            centerOffsetY = s.cy;
            centerOffsetZ = s.cz;
            spreadX = s.sx;
            spreadZ = s.sz;
            cardLocalScale = s.scl;
            overrideLayerForPicks = s.ol;
            pickLayer = s.pl;
        }
        catch { }
    }

    private void SaveSettings()
    {
        Saved s = new Saved
        {
            cx = centerOffsetX,
            cy = centerOffsetY,
            cz = centerOffsetZ,
            sx = spreadX,
            sz = spreadZ,
            scl = cardLocalScale,
            ol = overrideLayerForPicks,
            pl = pickLayer
        };
        string json = JsonUtility.ToJson(s);
        PlayerPrefs.SetString(PP_KEY, json);
        PlayerPrefs.Save();
    }
}
