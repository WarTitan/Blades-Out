using UnityEngine;
using Mirror;

[AddComponentMenu("Cards/Hand Visualizer")]
public class HandVisualizer : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerState;     // usually on same GameObject
    public Transform cardSpawnPoint;    // auto: TableSeatAnchors.GetHandAnchor(seatIndex)
    public GameObject cardPrefab3D;
    public CardDatabase database;

    [Header("Line Layout")]
    [Tooltip("Gap between cards along the anchor's local X (meters).")]
    public float gapX = 0.20f;
    [Tooltip("Raise cards above the table plane (meters). 0 to rest on table.")]
    public float raiseY = 0.0f;

    [Header("Orientation")]
    [Tooltip("Lay cards flat on the table (90° pitch).")]
    public bool layFlatOnTable = true;
    [Tooltip("Pitch if not laying flat. 90 = face up, 0 = vertical.")]
    public float tiltXDegrees = 90f;
    [Tooltip("If your prefab's front/back is flipped, add 180.")]
    public float frontFacingYawOffset = 0f;
    [Tooltip("Tiny extra yaw for the whole row (+/- few degrees).")]
    public float rowYawNudge = 0f;

    [Header("Scale")]
    public Vector3 cardLocalScale = Vector3.one;

    [Header("Debug")]
    public bool verboseLogs = true;

    // internals
    private bool subscribed = false;
    private bool rebuildPending = false;
    private float rebuildAtTime = 0f;
    private const float debounceSeconds = 0.02f;

    void Awake()
    {
        if (!playerState) playerState = GetComponent<PlayerState>();
    }

    void OnEnable() { SubscribeToLists(); }
    void OnDisable() { UnsubscribeFromLists(); }

    void Start()
    {
        TryResolveSpawnPoint(true);
        ScheduleRebuild("[Start]");
    }

    void Update()
    {
        if (cardSpawnPoint == null) TryResolveSpawnPoint(false);

        if (rebuildPending && Time.time >= rebuildAtTime)
        {
            if (playerState == null || cardSpawnPoint == null || cardPrefab3D == null || database == null)
            {
                rebuildAtTime = Time.time + debounceSeconds;
                return;
            }

            int ids = playerState.handIds.Count;
            int lvls = playerState.handLvls.Count;
            if (ids == lvls)
            {
                RebuildRow();
                rebuildPending = false;
            }
            else
            {
                rebuildAtTime = Time.time + debounceSeconds;
            }
        }
    }

    // ---------- SyncList subscription ----------
    private void SubscribeToLists()
    {
        if (subscribed || playerState == null) return;
        playerState.handIds.Callback += OnHandChanged_Int;
        playerState.handLvls.Callback += OnHandChanged_Byte;
        subscribed = true;
    }

    private void UnsubscribeFromLists()
    {
        if (!subscribed || playerState == null) return;
        playerState.handIds.Callback -= OnHandChanged_Int;
        playerState.handLvls.Callback -= OnHandChanged_Byte;
        subscribed = false;
    }

    private void OnHandChanged_Int(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        ScheduleRebuild("[handIds]");
    }

    private void OnHandChanged_Byte(SyncList<byte>.Operation op, int index, byte oldItem, byte newItem)
    {
        ScheduleRebuild("[handLvls]");
    }

    private void ScheduleRebuild(string reason)
    {
        rebuildPending = true;
        rebuildAtTime = Time.time + debounceSeconds;
        if (verboseLogs) Debug.Log($"[HandVisualizer] Rebuild scheduled {reason}");
    }

    // ---------- Anchor resolution ----------
    private void TryResolveSpawnPoint(bool forceLog)
    {
        if (cardSpawnPoint != null) return;

        if (playerState == null)
        {
#if UNITY_2023_1_OR_NEWER
            var arr = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
            var arr = Object.FindObjectsOfType<PlayerState>();
#endif
            foreach (var ps in arr)
            {
                if (ps.isLocalPlayer || playerState == null)
                {
                    playerState = ps;
                    break;
                }
            }
        }

        if (playerState == null)
        {
            if (forceLog || verboseLogs) Debug.LogWarning("[HandVisualizer] PlayerState not found yet.");
            return;
        }

        if (TableSeatAnchors.Instance == null)
        {
            if (forceLog || verboseLogs) Debug.LogWarning("[HandVisualizer] TableSeatAnchors.Instance is null.");
            return;
        }

        if (playerState.seatIndex < 0)
        {
            if (forceLog || verboseLogs) Debug.LogWarning("[HandVisualizer] seatIndex is -1; set it or assign Card Spawn Point manually.");
            return;
        }

        var anchor = TableSeatAnchors.Instance.GetHandAnchor(playerState.seatIndex);
        if (anchor != null)
        {
            cardSpawnPoint = anchor;
            if (forceLog || verboseLogs)
                Debug.Log($"[HandVisualizer] Seat {playerState.seatIndex} -> Using HandAnchor '{anchor.name}' @ {anchor.position}");
        }
        else
        {
            if (forceLog || verboseLogs)
                Debug.LogWarning($"[HandVisualizer] No HandAnchor for seat {playerState.seatIndex}. Fill TableSeatAnchors.handAnchors.");
        }
    }

    // ---------- Build / clear ----------
    private void ClearChildren()
    {
        if (!cardSpawnPoint) return;
        for (int i = cardSpawnPoint.childCount - 1; i >= 0; i--)
        {
            var c = cardSpawnPoint.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(c.gameObject);
            else Destroy(c.gameObject);
#else
            Destroy(c.gameObject);
#endif
        }
    }

    private void RebuildRow()
    {
        if (cardSpawnPoint == null)
        {
            Debug.LogError("[HandVisualizer] No Card Spawn Point. Fill in Inspector or ensure TableSeatAnchors + seatIndex are set.");
            return;
        }

        int count = Mathf.Min(playerState.handIds.Count, playerState.handLvls.Count);
        ClearChildren();
        if (count == 0) return;

        float mid = (count - 1) * 0.5f;

        // World rotation to keep cards flat and aligned to seat yaw
        float anchorYaw = cardSpawnPoint.eulerAngles.y + rowYawNudge + frontFacingYawOffset;
        float pitch = layFlatOnTable ? 90f : tiltXDegrees;
        Quaternion worldRot = Quaternion.Euler(pitch, anchorYaw, 0f);

        for (int i = 0; i < count; i++)
        {
            int id = playerState.handIds[i];
            int lvl = playerState.handLvls[i];

            Vector3 localPos = new Vector3((i - mid) * gapX, raiseY, 0f);
            Quaternion localRot = Quaternion.Inverse(cardSpawnPoint.rotation) * worldRot;

            var go = Instantiate(cardPrefab3D, cardSpawnPoint, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = cardLocalScale;

            // Debug: confirm world placement
            if (verboseLogs)
                Debug.Log($"[HandVisualizer] Spawn seat {playerState.seatIndex} -> anchor '{cardSpawnPoint.name}' at {cardSpawnPoint.position}, " +
                          $"card local {localPos}, world {go.transform.position}");

            // Bind visuals
            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null) adapter.Bind(id, lvl, database);

            // Selection metadata
            var view = go.GetComponent<CardView>();
            if (view == null) view = go.AddComponent<CardView>();
            view.Init(playerState, i, id, lvl, true); // true = in hand

            // Pin so nothing can move it away
            var pin = go.GetComponent<PinToAnchor>();
            if (!pin) pin = go.AddComponent<PinToAnchor>();
            pin.anchor = cardSpawnPoint;
            pin.localPosition = localPos;
            pin.localRotation = localRot;
            pin.localScale = cardLocalScale;
        }
    }
}
