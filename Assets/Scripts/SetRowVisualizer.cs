using UnityEngine;
using Mirror;

[AddComponentMenu("Cards/Set Row Visualizer")]
public class SetRowVisualizer : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerState;     // usually on same GameObject
    public Transform cardSpawnPoint;    // auto-resolved from TableSeatAnchors (SetAnchor_SeatX)
    public GameObject cardPrefab3D;
    public CardDatabase database;

    [Header("Line Layout")]
    public float gapX = 0.20f;
    public float raiseY = 0.0f;

    [Header("Orientation")]
    public bool layFlatOnTable = true;
    public float tiltXDegrees = 90f;
    public float frontFacingYawOffset = 0f;
    public float rowYawNudge = 0f;
    public bool faceDown = false;

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

    void OnEnable()
    {
        SubscribeToLists();
    }

    void Start()
    {
        // try immediately
        TryResolveSpawnPoint(forceLog: true);
        ScheduleRebuild("[Start]");
    }

    void OnDisable()
    {
        UnsubscribeFromLists();
    }

    void Update()
    {
        // Keep trying to resolve until we have an anchor
        if (cardSpawnPoint == null)
            TryResolveSpawnPoint(forceLog: false);

        if (rebuildPending && Time.time >= rebuildAtTime)
        {
            if (playerState == null || cardSpawnPoint == null || cardPrefab3D == null || database == null)
            {
                // If we still don't have the anchor, try again next frame
                rebuildAtTime = Time.time + debounceSeconds;
                return;
            }

            int ids = playerState.setIds.Count;
            int lvls = playerState.setLvls.Count;

            if (ids == lvls)
            {
                RebuildRow();
                rebuildPending = false;
            }
            else
            {
                // wait a tick until both SyncLists are aligned
                rebuildAtTime = Time.time + debounceSeconds;
            }
        }
    }

    // ---------- SyncList subscription ----------
    private void SubscribeToLists()
    {
        if (subscribed || playerState == null) return;
        playerState.setIds.Callback += OnSetChanged_Int;
        playerState.setLvls.Callback += OnSetChanged_Byte;
        subscribed = true;
    }

    private void UnsubscribeFromLists()
    {
        if (!subscribed || playerState == null) return;
        playerState.setIds.Callback -= OnSetChanged_Int;
        playerState.setLvls.Callback -= OnSetChanged_Byte;
        subscribed = false;
    }

    private void OnSetChanged_Int(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        ScheduleRebuild("[setIds]");
    }

    private void OnSetChanged_Byte(SyncList<byte>.Operation op, int index, byte oldItem, byte newItem)
    {
        ScheduleRebuild("[setLvls]");
    }

    private void ScheduleRebuild(string reason)
    {
        rebuildPending = true;
        rebuildAtTime = Time.time + debounceSeconds;
        if (verboseLogs) Debug.Log($"[SetRowVisualizer] Rebuild scheduled {reason}");
    }

    // ---------- Anchor resolution ----------
    private void TryResolveSpawnPoint(bool forceLog)
    {
        if (cardSpawnPoint != null) return;

        // recover playerState lazily
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
            if (forceLog || verboseLogs) Debug.LogWarning("[SetRowVisualizer] PlayerState not found yet.");
            return;
        }

        if (TableSeatAnchors.Instance == null)
        {
            if (forceLog || verboseLogs) Debug.LogWarning("[SetRowVisualizer] TableSeatAnchors.Instance is null (is it in the active scene and enabled?).");
            return;
        }

        if (playerState.seatIndex < 0)
        {
            if (forceLog || verboseLogs) Debug.LogWarning("[SetRowVisualizer] seatIndex is -1; set it on PlayerState or assign Card Spawn Point manually.");
            return;
        }

        var anchor = TableSeatAnchors.Instance.GetSetAnchor(playerState.seatIndex);
        if (anchor != null)
        {
            cardSpawnPoint = anchor;
            if (forceLog || verboseLogs)
                Debug.Log($"[SetRowVisualizer] Seat {playerState.seatIndex} -> Using SetAnchor '{anchor.name}'");
        }
        else
        {
            if (forceLog || verboseLogs)
                Debug.LogWarning($"[SetRowVisualizer] No SetAnchor for seat {playerState.seatIndex}. Fill TableSeatAnchors.setAnchors.");
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
        // Bail hard if anchor missing (prevents 0,0,0 spawns)
        if (cardSpawnPoint == null)
        {
            Debug.LogError("[SetRowVisualizer] No Card Spawn Point. Fill in Inspector or ensure TableSeatAnchors + seatIndex are set.");
            return;
        }

        int count = Mathf.Min(playerState.setIds.Count, playerState.setLvls.Count);
        ClearChildren();
        if (count == 0) return;

        float mid = (count - 1) * 0.5f;

        // World rotation for flat-on-table facing seat yaw
        float anchorYaw = cardSpawnPoint.eulerAngles.y + rowYawNudge + frontFacingYawOffset;
        float pitch = layFlatOnTable ? 90f : tiltXDegrees;
        if (faceDown) pitch += 180f;
        Quaternion worldRot = Quaternion.Euler(pitch, anchorYaw, 0f);

        for (int i = 0; i < count; i++)
        {
            int id = playerState.setIds[i];
            int lvl = playerState.setLvls[i];

            Vector3 localPos = new Vector3((i - mid) * gapX, raiseY, 0f);

            var go = Instantiate(cardPrefab3D, cardSpawnPoint, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = worldRot;
            go.transform.localScale = cardLocalScale;

            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null) adapter.Bind(id, lvl, database);

            var view = go.GetComponent<CardView>();
            if (view == null) view = go.AddComponent<CardView>();
            view.Init(playerState, i, id, lvl, false);  // false = in set row
        }
    }
}
