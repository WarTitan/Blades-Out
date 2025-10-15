using UnityEngine;
using Mirror;

public class HandVisualizer : NetworkBehaviour
{
    [Header("References")]
    public PlayerState playerState;       // usually on same GameObject
    public Transform cardSpawnPoint;      // resolved from TableSeatAnchors at runtime
    public GameObject cardPrefab3D;
    public CardDatabase database;

    [Header("Line Layout")]
    [Tooltip("Gap between cards along the seat's local X (meters).")]
    public float gapX = 0.18f;

    [Tooltip("Raise cards above the table plane (meters). 0 to rest on table.")]
    public float raiseY = 0.0f;

    [Header("Orientation")]
    [Tooltip("If ON, lay cards flat on the table. (local +Z faces Up)")]
    public bool layFlatOnTable = true;

    [Tooltip("Pitch in degrees if not laying flat. 90 = face up, 0 = vertical.")]
    public float tiltXDegrees = 90f;

    [Tooltip("Add 0 or 180 if your prefab front/back is flipped.")]
    public float frontFacingYawOffset = 0f;

    [Tooltip("Tiny extra yaw for the whole hand (+/- few degrees).")]
    public float handYawNudge = 0f;

    [Header("Scale")]
    public Vector3 cardLocalScale = Vector3.one;

    // internal
    private bool subscribed = false;
    private bool rebuildPending = false;
    private float rebuildAtTime = 0f;
    private const float debounceSeconds = 0.02f;

    void Awake()
    {
        if (playerState == null) playerState = GetComponent<PlayerState>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveCardSpawnPointFromSeat();
        Subscribe();
        ScheduleRebuild("[OnStartClient]");
    }

    void OnEnable() { Subscribe(); }
    void OnDisable() { Unsubscribe(); }

    void Update()
    {
        if (rebuildPending && Time.time >= rebuildAtTime)
        {
            int ids = playerState != null ? playerState.handIds.Count : 0;
            int lvls = playerState != null ? playerState.handLvls.Count : 0;
            if (ids == lvls) { RebuildHand(); rebuildPending = false; }
            else { rebuildAtTime = Time.time + debounceSeconds; }
        }
    }

    private void ResolveCardSpawnPointFromSeat()
    {
        if (TableSeatAnchors.Instance == null || playerState == null) return;
        var anchor = TableSeatAnchors.Instance.GetHandAnchor(playerState.seatIndex);
        if (anchor != null) cardSpawnPoint = anchor;
    }

    private void Subscribe()
    {
        if (subscribed || playerState == null) return;
        playerState.handIds.Callback += OnHandListChanged_Int;
        playerState.handLvls.Callback += OnHandListChanged_Byte;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || playerState == null) return;
        playerState.handIds.Callback -= OnHandListChanged_Int;
        playerState.handLvls.Callback -= OnHandListChanged_Byte;
        subscribed = false;
    }

    private void OnHandListChanged_Int(Mirror.SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        ScheduleRebuild("[handIds]");
    }

    private void OnHandListChanged_Byte(Mirror.SyncList<byte>.Operation op, int index, byte oldItem, byte newItem)
    {
        ScheduleRebuild("[handLvls]");
    }

    private void ScheduleRebuild(string reason)
    {
        if (playerState == null) return;
        rebuildPending = true;
        rebuildAtTime = Time.time + debounceSeconds;
    }

    private void ClearChildren()
    {
        if (cardSpawnPoint == null) return;
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

    private void RebuildHand()
    {
        if (playerState == null || cardSpawnPoint == null || cardPrefab3D == null || database == null)
            return;

        int count = Mathf.Min(playerState.handIds.Count, playerState.handLvls.Count);
        ClearChildren();
        if (count == 0) return;

        float mid = (count - 1) * 0.5f;

        // World yaw from seat; cards inherit parent orientation
        float anchorYaw = cardSpawnPoint.eulerAngles.y + handYawNudge + frontFacingYawOffset;

        // Flat or tilted
        float pitch = layFlatOnTable ? 90f : tiltXDegrees;
        Quaternion worldRot = Quaternion.Euler(pitch, anchorYaw, 0f);

        for (int i = 0; i < count; i++)
        {
            int id = playerState.handIds[i];
            int lvl = playerState.handLvls[i];

            // Position relative to seat's local X axis
            Vector3 localPos = new Vector3((i - mid) * gapX, raiseY, 0f);

            var go = Instantiate(cardPrefab3D);

            // Parent with local placement (so line is along seat's X)
            go.transform.SetParent(cardSpawnPoint, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = worldRot;     // use world rotation (stable across seats)
            go.transform.localScale = cardLocalScale;

            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null) adapter.Bind(id, lvl, database);
        }
    }
}
