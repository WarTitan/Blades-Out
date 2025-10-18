using UnityEngine;
using Mirror;

[AddComponentMenu("Cards/Set Row Visualizer")]
public class SetRowVisualizer : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerState;     // usually on same GameObject
    public Transform cardSpawnPoint;    // auto: TableSeatAnchors.Instance.GetSetAnchor(seatIndex)
    public GameObject cardPrefab3D;
    public CardDatabase database;

    [Header("Line Layout")]
    public float gapX = 0.20f;
    public float raiseY = 0.0f;

    [Header("Debug")]
    public bool verboseLogs = false;

    Transform _lastAnchor;

    // --- Unity 2023+ compat helper ---
    static T FindFirst<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    void Awake()
    {
        if (!playerState) playerState = GetComponent<PlayerState>();
        if (!database) database = FindFirst<CardDatabase>();
    }

    void OnEnable()
    {
        Hook();
        RebuildRow();
    }

    void OnDisable()
    {
        Unhook();
    }

    void Hook()
    {
        if (playerState == null) return;

        playerState.setIds.Callback += OnSetChanged;
        playerState.setLvls.Callback += OnSetChanged;

        // Initial anchor resolve
        ResolveAnchor();
    }

    void Unhook()
    {
        if (playerState == null) return;

        playerState.setIds.Callback -= OnSetChanged;
        playerState.setLvls.Callback -= OnSetChanged;
    }

    void OnSetChanged(Mirror.SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        RebuildRow();
    }
    void OnSetChanged(Mirror.SyncList<byte>.Operation op, int index, byte oldItem, byte newItem)
    {
        RebuildRow();
    }

    void ResolveAnchor()
    {
        if (playerState == null) return;
        var anchors = TableSeatAnchors.Instance;
        if (anchors != null)
        {
            var t = anchors.GetSetAnchor(playerState.seatIndex);
            if (t != null) cardSpawnPoint = t;
            _lastAnchor = cardSpawnPoint;
        }
    }

    void ClearChildren()
    {
        if (!cardSpawnPoint) return;
        for (int i = cardSpawnPoint.childCount - 1; i >= 0; i--)
        {
            var c = cardSpawnPoint.GetChild(i);
            Destroy(c.gameObject);
        }
    }

    public void RebuildRow()
    {
        if (playerState == null) return;
        if (database == null) return;
        ResolveAnchor();
        if (!cardSpawnPoint) return;

        ClearChildren();

        int count = Mathf.Min(playerState.setIds.Count, playerState.setLvls.Count);
        if (count <= 0) return;

        float mid = (count - 1) * 0.5f;
        var localRot = Quaternion.identity;
        var cardLocalScale = Vector3.one;

        for (int i = 0; i < count; i++)
        {
            int id = playerState.setIds[i];
            int lvl = playerState.setLvls[i];

            var def = database != null ? database.Get(id) : null;

            Vector3 localPos = new Vector3((i - mid) * gapX, raiseY, 0f);

            GameObject go;
            if (def != null && def.setShowcasePrefab != null)
            {
                go = Instantiate(def.setShowcasePrefab, cardSpawnPoint, false);

                // apply per-card offsets so you can fine-tune each model
                go.transform.localPosition = localPos + def.setShowcaseLocalOffset;
                go.transform.localRotation = Quaternion.Euler(def.setShowcaseLocalEuler);
                go.transform.localScale = Vector3.Scale(cardLocalScale, def.setShowcaseLocalScale);
            }
            else
            {
                // fallback to card 3D prefab
                go = Instantiate(cardPrefab3D, cardSpawnPoint, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = localRot;
                go.transform.localScale = cardLocalScale;
            }

            if (verboseLogs)
                Debug.Log($"[SetRowVisualizer] Spawn seat {playerState.seatIndex} -> anchor '{cardSpawnPoint.name}' at {cardSpawnPoint.position}, " +
                          $"card local {localPos}, world {go.transform.position}");

            // OPTIONAL: still bind for level/name display if your showcase has a small card overlay
            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null) adapter.Bind(id, lvl, database);

            var view = go.GetComponent<CardView>();
            if (view == null) view = go.AddComponent<CardView>();
            view.Init(playerState, i, id, lvl, false);  // false = in set row

            // Pin (so if the anchor moves, the model follows)
            var pin = go.GetComponent<PinToAnchor>();
            if (!pin) pin = go.AddComponent<PinToAnchor>();
            pin.anchor = cardSpawnPoint;
            pin.localPosition = go.transform.localPosition;
            pin.localRotation = go.transform.localRotation;
            pin.localScale = go.transform.localScale;
        }
    }
}
