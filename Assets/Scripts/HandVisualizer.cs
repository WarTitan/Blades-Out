using UnityEngine;
using Mirror;

public class HandVisualizer : NetworkBehaviour
{
    [Header("References")]
    public PlayerState playerState;       // usually on same GameObject
    public Transform cardSpawnPoint;      // resolved from TableSeatAnchors at runtime
    public GameObject cardPrefab3D;
    public CardDatabase database;

    [Header("Ellipse Settings")]
    [Tooltip("Minor-to-major axis ratio b/a for the ellipse (0..1). a will equal distance player->anchor.")]
    [Range(0.05f, 1f)] public float minorAxisRatio = 0.65f;

    [Tooltip("Angular step (in degrees) around ellipse parameter t for spacing between cards (small = tighter).")]
    public float angleStepDegrees = 8f;

    [Tooltip("Lift cards above table plane (world Y).")]
    public float raiseY = 0.0f;

    [Header("Facing & Tilt")]
    [Tooltip("Pitch tilt in degrees (X axis). 80 = laid on table, 0 = fully upright.")]
    public float tiltXDegrees = 80f;

    [Tooltip("Add 0 or 180 to flip the prefab so the FRONT faces the player.")]
    public float frontFacingYawOffset = 0f;

    [Tooltip("Tiny extra yaw for whole hand (+/- few degrees) if needed.")]
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

        // World-space center = player pos projected to the table plane (anchor Y)
        Vector3 centerWorld = playerState.transform.position;
        centerWorld.y = cardSpawnPoint.position.y;

        // World-space anchor on rim
        Vector3 anchorWorld = cardSpawnPoint.position;

        // Flat (XZ) radial direction from center to anchor
        Vector3 radial = anchorWorld - centerWorld;
        radial.y = 0f;
        float a = radial.magnitude; // major radius (ellipse a) passes through anchor
        if (a < 0.0001f)
        {
            // Fallback: put them right at anchor in a small lateral line
            float midF = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int idF = playerState.handIds[i];
                int lvlF = playerState.handLvls[i];

                float off = (i - midF) * 0.06f;
                Vector3 pos = anchorWorld + new Vector3(off, raiseY, 0f);
                float yaw = playerState.transform.eulerAngles.y + handYawNudge + frontFacingYawOffset;
                Quaternion rot = Quaternion.Euler(tiltXDegrees, yaw, 0f);

                var goF = Instantiate(cardPrefab3D);
                goF.transform.SetParent(cardSpawnPoint, true);
                goF.transform.position = pos;
                goF.transform.rotation = rot;
                goF.transform.localScale = cardLocalScale;

                var adF = goF.GetComponent<Card3DAdapter>();
                if (adF != null) adF.Bind(idF, lvlF, database);
            }
            return;
        }

        Vector3 u = radial / a;                 // major axis direction (toward seat)
        Vector3 v = new Vector3(-u.z, 0f, u.x); // minor axis (perpendicular in XZ)

        // Minor radius b from ratio
        float b = Mathf.Max(0.0001f, a * Mathf.Clamp01(minorAxisRatio));

        // We place cards around t = 0 (the anchor is at t = 0 -> (a,0) in this basis)
        float mid = (count - 1) * 0.5f;
        float stepRad = angleStepDegrees * Mathf.Deg2Rad;

        for (int i = 0; i < count; i++)
        {
            int id = playerState.handIds[i];
            int lvl = playerState.handLvls[i];

            float offsetIdx = i - mid;
            float t = offsetIdx * stepRad; // small symmetric sweep around anchor

            // Ellipse param in world: center + a*cos(t)*u + b*sin(t)*v
            Vector3 posWorld = centerWorld + (a * Mathf.Cos(t)) * u + (b * Mathf.Sin(t)) * v;
            posWorld.y = anchorWorld.y + raiseY;

            // Face the center (player): yaw from pos toward center, no Z roll, add optional flip and nudge
            Vector3 toCenter = centerWorld - posWorld;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude < 0.000001f) toCenter = u * -1f; // fallback

            float yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            yaw += handYawNudge + frontFacingYawOffset;

            Quaternion rotWorld = Quaternion.Euler(tiltXDegrees, yaw, 0f);

            // Spawn and parent (keep world space stable)
            var go = Instantiate(cardPrefab3D);
            go.transform.SetParent(cardSpawnPoint, true); // worldPositionStays = true
            go.transform.position = posWorld;
            go.transform.rotation = rotWorld;
            go.transform.localScale = cardLocalScale;

            var adapter = go.GetComponent<Card3DAdapter>();
            if (adapter != null) adapter.Bind(id, lvl, database);
        }
    }
}
