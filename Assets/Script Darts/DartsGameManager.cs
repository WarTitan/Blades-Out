// FILE: DartsGameManager.cs
// Spawns a networked DartsProjectile so ALL players see the dart fly and stick.
// Still uses your mask scorer / hitboxes and exact-finish rule.

using System.Collections;
using Mirror;
using UnityEngine;

[AddComponentMenu("Minigames/Darts Game Manager")]
public class DartsGameManager : NetworkBehaviour
{
    public static DartsGameManager Instance { get; private set; }

    [Header("Setup")]
    public DartsBoardTarget[] boards = new DartsBoardTarget[5];
    public LayerMask dartsLayerMask;

    [Header("Rules")]
    public int startingScore = 501;
    public float rayMaxDistance = 100f;
    public bool requireExactFinish = true;

    [Header("Landing Delay / Timing")]
    public bool delayScoreUntilImpact = true;
    public float serverDartSpeed = 40f;

    [Header("Projectile Visuals")]
    public GameObject dartProjectilePrefab;   // assign a NetworkIdentity prefab
    public float projArcHeight = 0.15f;
    public float projSpinSpeed = 540f;
    public float projStickDepth = 0.02f;
    public float projLifeAfterStick = 8f;

    [Header("Debug")]
    public bool debugLogs = true;
    public bool autoLearnLayerFromFallback = true;
    public bool drawDebugRay = true;

    public readonly SyncList<int> scores = new SyncList<int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureScoresCapacity();
        Server_ResetScores();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureScoresCapacity();
        scores.Callback += OnScoresChanged;
        for (int i = 0; i < scores.Count; i++) ApplyScoreToBoard(i + 1, scores[i]);
    }

    public override void OnStopClient()
    {
        scores.Callback -= OnScoresChanged;
        base.OnStopClient();
    }

    void EnsureScoresCapacity()
    {
        if (isServer)
        {
            while (scores.Count < 5) scores.Add(startingScore);
            while (scores.Count > 5) scores.RemoveAt(scores.Count - 1);
        }
    }

    void OnScoresChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        ApplyScoreToBoard(index + 1, newItem);
    }

    void ApplyScoreToBoard(int boardIndex1Based, int value)
    {
        int idx = Mathf.Clamp(boardIndex1Based - 1, 0, 4);
        var t = (boards != null && idx < boards.Length) ? boards[idx] : null;
        if (t != null) t.SetScore(value);
    }

    // ---------- Server API ----------

    [Server]
    public void Server_ResetScores()
    {
        EnsureScoresCapacity();
        for (int i = 0; i < 5; i++) scores[i] = startingScore;
        if (debugLogs) Debug.Log("[Darts] Scores reset to " + startingScore);
    }

    [Server]
    public void Server_HandleShotRay(uint shooterNetId, Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 dir = rayDirection.sqrMagnitude > 0f ? rayDirection.normalized : Vector3.forward;
        Ray ray = new Ray(rayOrigin, dir);

        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * Mathf.Min(rayMaxDistance, 8f), Color.red, 1f, false);

        int mask = (dartsLayerMask.value != 0) ? dartsLayerMask.value : Physics.DefaultRaycastLayers;

        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, mask, QueryTriggerInteraction.Collide))
        {
            int boardIndex = 0;
            int val = 0;

            var maskScorer = hit.collider.GetComponentInParent<DartsBoardMaskScorer>();
            if (maskScorer != null)
            {
                bool ok = maskScorer.TryScoreFromHit(hit, out boardIndex, out val);
                if (debugLogs) Debug.Log("[Darts] MaskScorer ok=" + ok + " board=" + boardIndex + " val=" + val + " hit=" + hit.collider.name);
            }
            else
            {
                var hb = hit.collider.GetComponent<DartsHitbox>();
                if (hb != null)
                {
                    boardIndex = hb.boardIndex1Based;
                    val = Mathf.Max(0, hb.scoreValue);
                    if (debugLogs) Debug.Log("[Darts] Fallback Hitbox board=" + boardIndex + " val=" + val);
                }
            }

            // Spawn networked projectile so everyone sees it
            float distance = hit.distance;
            float travel = Mathf.Max(0.05f, distance / Mathf.Max(0.01f, serverDartSpeed));
            Server_SpawnProjectile(ray.origin, hit.point, hit.normal, travel);

            // Validate score
            if (!IsPlausibleScore(val) || boardIndex < 1 || boardIndex > 5)
            {
                if (debugLogs) Debug.Log("[Darts] Ignored score=" + val + " board=" + boardIndex + " (invalid or outside).");
                return;
            }

            if (delayScoreUntilImpact)
            {
                float delay = travel;
                if (debugLogs) Debug.Log("[Darts] SCHEDULE seat " + boardIndex + " val=" + val + " delay=" + delay.ToString("F2"));
                StartCoroutine(Server_ApplyScoreAfterDelay(boardIndex, val, delay));
            }
            else
            {
                Server_ApplyScoreNow(boardIndex, val, 0, hit.collider);
            }
        }
        else
        {
            if (Physics.Raycast(ray, out RaycastHit fb, rayMaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                string layerName = LayerMask.LayerToName(fb.collider.gameObject.layer);
                if (debugLogs)
                    Debug.Log("[Darts] MISS on dartsLayerMask, fallback hit '" + fb.collider.name +
                              "' layer=" + fb.collider.gameObject.layer +
                              " (" + (string.IsNullOrEmpty(layerName) ? "unnamed" : layerName) + ")");
                if (autoLearnLayerFromFallback)
                {
                    int bit = 1 << fb.collider.gameObject.layer;
                    if ((dartsLayerMask.value & bit) == 0)
                    {
                        dartsLayerMask = dartsLayerMask.value | bit;
                        if (debugLogs) Debug.Log("[Darts] Auto-added layer to dartsLayerMask. Try again.");
                    }
                }
            }
            else
            {
                if (debugLogs) Debug.Log("[Darts] MISS: no collider hit.");
            }
        }
    }

    [Server]
    private void Server_SpawnProjectile(Vector3 start, Vector3 end, Vector3 normal, float travel)
    {
        if (dartProjectilePrefab == null) return;
        var go = Instantiate(dartProjectilePrefab, start, Quaternion.identity);
        var proj = go.GetComponent<DartsProjectile>();
        if (proj != null)
        {
            proj.startPos = start;
            proj.endPos = end;
            proj.hitNormal = (normal.sqrMagnitude > 0.0001f) ? normal.normalized : Vector3.forward;
            proj.startTime = NetworkTime.time;
            proj.travelTime = Mathf.Max(0.05f, travel);
            proj.arcHeight = Mathf.Max(0.01f, projArcHeight);
            proj.spinSpeed = projSpinSpeed;
            proj.stickDepth = Mathf.Max(0f, projStickDepth);
            proj.lifeAfterStick = Mathf.Max(0f, projLifeAfterStick);
        }
        NetworkServer.Spawn(go);
    }

    [Server]
    IEnumerator Server_ApplyScoreAfterDelay(int boardIndex1Based, int val, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Server_ApplyScoreNow(boardIndex1Based, val, 0, null);
    }

    [Server]
    void Server_ApplyScoreNow(int boardIndex1Based, int val, uint shooterNetId, Collider col)
    {
        int idx = Mathf.Clamp(boardIndex1Based - 1, 0, 4);
        int before = scores[idx];

        if (requireExactFinish && val > before)
        {
            if (debugLogs)
                Debug.Log("[Darts] Exact finish: val " + val + " > current " + before + " -> no change.");
            return;
        }

        int after = Mathf.Max(0, before - val);
        scores[idx] = after;

        if (debugLogs)
        {
            string colName = col ? col.name : "<n/a>";
            Debug.Log("[Darts] APPLY seat " + boardIndex1Based +
                      " val=" + val + " before=" + before + " after=" + after +
                      " collider=" + colName);
        }
    }

    static bool IsPlausibleScore(int v)
    {
        if (v == 25 || v == 50) return true;
        return v >= 1 && v <= 60;
    }
}
