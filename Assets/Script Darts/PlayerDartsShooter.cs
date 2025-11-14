// FILE: PlayerDartsShooter.cs
// - Holster dart slides in, spins, and ONLY respawns after the previous dart lands.
// - On throw, the holster dart detaches and becomes the projectile (no duplicate).
// - Dart spins in the air around its local forward at the same speed as in holster.
// - Works with arc or line flight (keep your DartsGameManager delay on impact).

using Mirror;
using UnityEngine;
using System.Collections;

[AddComponentMenu("Minigames/Darts Player Shooter")]
public class PlayerDartsShooter : NetworkBehaviour
{
    public LocalCameraController cameraController;
    public DartsAimer aimer;

    [Header("Dart Prefab and Flight")]
    public GameObject dartPrefab;
    public float dartSpeed = 40f;

    [Tooltip("Holster local position relative to camera (meters). Increase forward to move it farther away.")]
    public float holsterForward = 1.20f;
    public float holsterRight = 0.25f;
    public float holsterUp = -0.05f;

    [Header("Holster Spin")]
    public float holsterSpinDegPerSec = 90f;   // spin about dart's local +Z while holstered

    [Header("Model Orientation")]
    public ForwardAxis modelForward = ForwardAxis.ZMinus; // set to what matches your mesh (you used ZMinus)
    public Vector3 extraModelEulerOffset = Vector3.zero;  // fine-tune in degrees

    [Header("Impact / Stick")]
    public float tipInset = 0.01f;             // embed along dart forward
    public float destroyAfterStickSeconds = 8f;

    [Header("Re-Holster Animation")]
    [Tooltip("Where the new dart appears from, relative to camera (meters). +X right, -Y down, +Z forward.")]
    public Vector3 slideInStartOffset = new Vector3(1.10f, -0.65f, 0.70f);
    public float slideInDuration = 0.35f;

    [Header("Arc Flight")]
    public bool useArcFlight = true;
    public float arcHeightPerMeter = 0.15f;
    public float minArcHeight = 0.25f;
    public float arcDurationScale = 1.00f;
    public bool arcUseWorldUp = true;

    // State
    private GameObject holsterDart;
    private bool canThrow = false;
    private bool spawningNext = false;
    private bool waitingForImpact = false;   // NEW: blocks respawn until landing
    private Camera cam;

    public enum ForwardAxis { ZPlus, ZMinus, XPlus, XMinus, YPlus, YMinus }

    void Start()
    {
        if (cameraController == null) cameraController = GetComponent<LocalCameraController>();
        if (aimer == null) aimer = GetComponent<DartsAimer>();
        cam = (cameraController != null ? cameraController.playerCamera : null);
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        var tm = TurnManagerNet.Instance;
        bool inDarts = (tm != null && tm.phase == TurnManagerNet.Phase.Darts);

        if (!inDarts)
        {
            CleanupHolster();
            return;
        }

        if (cam == null)
        {
            cam = (cameraController != null ? cameraController.playerCamera : null);
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
        }

        // Spawn the first holster dart only once (slide-in), and ONLY respawn after impact.
        if (holsterDart == null && !spawningNext && !waitingForImpact)
        {
            StartCoroutine(SpawnHolsterDart(false)); // slide-in
        }

        if (canThrow && Input.GetMouseButtonDown(0))
        {
            TryThrow();
        }
    }

    private void TryThrow()
    {
        if (holsterDart == null || !canThrow) return;

        Vector2 screenPt = (aimer != null)
            ? aimer.GetCurrentScreenPoint()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Ray ray = cam.ScreenPointToRay(screenPt);

        float maxDist = (DartsGameManager.Instance != null ? DartsGameManager.Instance.rayMaxDistance : 100f);
        bool gotHit = Physics.Raycast(ray, out RaycastHit hit, maxDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        // Detach and convert holster dart into the projectile (no duplicate)
        var spinner = holsterDart.GetComponent<_HolsterSpinner>();
        if (spinner != null) Destroy(spinner);

        holsterDart.transform.SetParent(null, true);

        var rb = holsterDart.GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }

        Vector3 start = holsterDart.transform.position;
        Vector3 targetPoint = gotHit ? hit.point : (ray.origin + ray.direction * 12f);

        Quaternion modelFix = AxisToZ(modelForward) * Quaternion.Euler(extraModelEulerOffset);

        Vector3 dir = (targetPoint - start);
        if (dir.sqrMagnitude < 0.0001f) dir = cam.transform.forward;
        holsterDart.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * modelFix;

        // Choose mover (both spin in air at holsterSpinDegPerSec)
        if (useArcFlight)
        {
            var mover = holsterDart.AddComponent<_DartArcMover>();
            mover.Init(
                start,
                targetPoint,
                gotHit ? hit.collider.transform : null,
                dartSpeed,
                arcDurationScale,
                tipInset,
                modelFix,
                destroyAfterStickSeconds,
                this,
                arcUseWorldUp ? Vector3.up : cam.transform.up,
                Mathf.Max(minArcHeight, arcHeightPerMeter * Vector3.Distance(start, targetPoint)),
                holsterSpinDegPerSec // spin in air
            );
        }
        else
        {
            var mover = holsterDart.AddComponent<_DartLineMover>();
            mover.Init(
                targetPoint,
                gotHit ? hit.collider.transform : null,
                dartSpeed,
                tipInset,
                modelFix,
                destroyAfterStickSeconds,
                this,
                holsterSpinDegPerSec // spin in air
            );
        }

        holsterDart = null;
        canThrow = false;
        waitingForImpact = true; // NEW: do not spawn until we land

        // Server scoring (delayed until impact there)
        Cmd_Fire(ray.origin, ray.direction);
    }

    [Command]
    private void Cmd_Fire(Vector3 rayOrigin, Vector3 rayDirection)
    {
        var gm = DartsGameManager.Instance;
        if (gm != null)
        {
            var id = GetComponent<NetworkIdentity>();
            uint nid = id != null ? id.netId : 0;
            gm.Server_HandleShotRay(nid, rayOrigin, rayDirection);
        }
    }

    // Called by mover when the projectile sticks
    private void OnLocalDartLanded()
    {
        waitingForImpact = false;            // allow respawn now
        if (!spawningNext) StartCoroutine(SpawnHolsterDart(false)); // slide-in
    }

    private IEnumerator SpawnHolsterDart(bool immediate)
    {
        spawningNext = true;

        holsterDart = Instantiate(dartPrefab);
        PrepareDartForHolster(holsterDart);

        var spinner = holsterDart.AddComponent<_HolsterSpinner>();
        spinner.Init(
            cam.transform,
            AxisToZ(modelForward) * Quaternion.Euler(extraModelEulerOffset),
            holsterForward, holsterRight, holsterUp,
            holsterSpinDegPerSec
        );

        // Always slide in from the right/bottom
        Vector3 startLocal = slideInStartOffset;
        Vector3 endLocal = new Vector3(holsterRight, holsterUp, holsterForward);

        float t0 = Time.unscaledTime;
        float dur = Mathf.Max(0.05f, slideInDuration);

        while (true)
        {
            float t = (Time.unscaledTime - t0) / dur;
            if (t >= 1f) t = 1f;

            float s = t * t * (3f - 2f * t);  // smoothstep
            Vector3 cur = Vector3.Lerp(startLocal, endLocal, s);
            spinner.SetLocalOffsets(cur.z, cur.x, cur.y); // (forward, right, up)

            if (t >= 1f) break;
            yield return null;
        }

        canThrow = true;
        spawningNext = false;
    }

    private void PrepareDartForHolster(GameObject go)
    {
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(holsterRight, holsterUp, holsterForward);

        var rb = go.GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }

        var m1 = go.GetComponent<_DartLineMover>(); if (m1) Destroy(m1);
        var m2 = go.GetComponent<_DartArcMover>(); if (m2) Destroy(m2);
    }

    private void CleanupHolster()
    {
        if (holsterDart != null) Destroy(holsterDart);
        holsterDart = null;
        canThrow = false;
        spawningNext = false;
        waitingForImpact = false;
    }

    private static Quaternion AxisToZ(ForwardAxis axis)
    {
        switch (axis)
        {
            case ForwardAxis.ZPlus: return Quaternion.identity;
            case ForwardAxis.ZMinus: return Quaternion.Euler(0f, 180f, 0f);
            case ForwardAxis.XPlus: return Quaternion.Euler(0f, -90f, 0f);
            case ForwardAxis.XMinus: return Quaternion.Euler(0f, 90f, 0f);
            case ForwardAxis.YPlus: return Quaternion.Euler(90f, 0f, 0f);
            case ForwardAxis.YMinus: return Quaternion.Euler(-90f, 0f, 0f);
            default: return Quaternion.identity;
        }
    }

    // ---------- Helpers ----------

    // Parented spinner for holster dart (stable, no jitter)
    private class _HolsterSpinner : MonoBehaviour
    {
        private Transform camParent;
        private Quaternion modelFix;
        private float fwd, right, up;
        private float spinDegPerSec;
        private float spin;

        public void Init(Transform cameraTransform, Quaternion modelFixRot,
                         float forward, float rightOffset, float upOffset, float spinSpeed)
        {
            camParent = cameraTransform;
            modelFix = modelFixRot;
            fwd = forward; right = rightOffset; up = upOffset;
            spinDegPerSec = spinSpeed;

            transform.SetParent(camParent, false);
            spin = 0f;

            transform.localPosition = new Vector3(right, up, fwd);
            transform.localRotation = modelFix * Quaternion.AngleAxis(spin, Vector3.forward);
        }

        public void SetLocalOffsets(float forward, float rightOffset, float upOffset)
        {
            fwd = forward; right = rightOffset; up = upOffset;
        }

        void LateUpdate()
        {
            if (!camParent) return;

            transform.localPosition = new Vector3(right, up, fwd);
            spin += holsterSpinStep();
            transform.localRotation = modelFix * Quaternion.AngleAxis(spin, Vector3.forward);
        }

        private float holsterSpinStep()
        {
            return spinDegPerSec * Time.unscaledDeltaTime;
        }
    }

    // Straight-line mover (spins in air)
    private class _DartLineMover : MonoBehaviour
    {
        private Vector3 target;
        private Transform hitParent;
        private float speed;
        private float tipInset;
        private Quaternion modelFix;
        private float dieAt;
        private bool stuck;
        private PlayerDartsShooter owner;
        private float spinAngle;
        private float spinDegPerSec;

        public void Init(Vector3 tgt, Transform parent, float spd, float inset, Quaternion modelFixRot,
                         float lifeSeconds, PlayerDartsShooter ownerRef, float spinSpeed)
        {
            target = tgt;
            hitParent = parent;
            speed = Mathf.Max(1f, spd);
            tipInset = Mathf.Max(0f, inset);
            modelFix = modelFixRot;
            dieAt = Time.time + Mathf.Max(1f, lifeSeconds);
            stuck = false;
            owner = ownerRef;
            spinAngle = 0f;
            spinDegPerSec = spinSpeed;
        }

        void Update()
        {
            if (stuck)
            {
                if (Time.time >= dieAt) Destroy(gameObject);
                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            float dist = to.magnitude;

            float stepDist = speed * Time.deltaTime;
            if (dist <= stepDist || dist < 0.02f)
            {
                // Impact: embed along own forward
                Vector3 fwd = transform.forward.normalized;
                Vector3 finalPos = target - fwd * tipInset;
                transform.position = finalPos;

                if (hitParent != null) transform.SetParent(hitParent, true);

                stuck = true;
                if (owner != null) owner.OnLocalDartLanded();
                return;
            }

            // Flight step
            Vector3 stepDir = (dist > 0.0001f) ? (to / dist) : transform.forward;
            Vector3 step = stepDir * stepDist;
            transform.position = pos + step;

            // Spin around local forward at same speed as holster
            spinAngle += spinDegPerSec * Time.unscaledDeltaTime;

            if (step.sqrMagnitude > 0.000001f)
            {
                Quaternion look = Quaternion.LookRotation(stepDir, Vector3.up);
                transform.rotation = look * modelFix * Quaternion.AngleAxis(spinAngle, Vector3.forward);
            }

            if (Time.time >= dieAt) Destroy(gameObject);
        }
    }

    // Arc mover (spins in air)
    private class _DartArcMover : MonoBehaviour
    {
        private Vector3 p0, p1, p2;
        private Transform hitParent;
        private float duration;
        private float endTime;
        private float tipInset;
        private Quaternion modelFix;
        private bool stuck;
        private PlayerDartsShooter owner;
        private float spinAngle;
        private float spinDegPerSec;

        public void Init(
            Vector3 start,
            Vector3 target,
            Transform parent,
            float speed,
            float durationScale,
            float inset,
            Quaternion modelFixRot,
            float lifeSeconds,
            PlayerDartsShooter ownerRef,
            Vector3 upAxis,
            float arcHeight,
            float spinSpeed)
        {
            p0 = start;
            p2 = target;
            hitParent = parent;
            tipInset = Mathf.Max(0f, inset);
            modelFix = modelFixRot;
            owner = ownerRef;
            spinAngle = 0f;
            spinDegPerSec = spinSpeed;

            float dist = Mathf.Max(0.01f, Vector3.Distance(start, target));
            duration = Mathf.Max(0.05f, (dist / Mathf.Max(0.01f, speed)) * Mathf.Max(0.01f, durationScale));
            endTime = Time.time + duration;

            // Apex control point
            Vector3 mid = (p0 + p2) * 0.5f;
            Vector3 up = (upAxis.sqrMagnitude > 0.0001f ? upAxis.normalized : Vector3.up);
            p1 = mid + up * arcHeight;

            // Start pose
            transform.position = p0;
            Vector3 tan0 = 2f * (p1 - p0);
            Quaternion look0 = Quaternion.LookRotation(tan0.sqrMagnitude > 0.0001f ? tan0.normalized : (p2 - p0).normalized, Vector3.up);
            transform.rotation = look0 * modelFix * Quaternion.AngleAxis(spinAngle, Vector3.forward);
        }

        void Update()
        {
            if (stuck) return;

            float t = Mathf.InverseLerp(endTime - duration, endTime, Time.time);
            if (t >= 1f) t = 1f;

            Vector3 pos = Bezier(p0, p1, p2, t);
            Vector3 tan = BezierTangent(p0, p1, p2, t);

            transform.position = pos;

            // Spin around local forward at same speed as holster
            spinAngle += spinDegPerSec * Time.unscaledDeltaTime;

            if (tan.sqrMagnitude > 0.000001f)
            {
                Quaternion look = Quaternion.LookRotation(tan.normalized, Vector3.up);
                transform.rotation = look * modelFix * Quaternion.AngleAxis(spinAngle, Vector3.forward);
            }

            if (t >= 1f)
            {
                // Impact: embed along own forward by tipInset
                Vector3 fwd = transform.forward;
                transform.position = p2 - fwd * tipInset;

                if (hitParent != null) transform.SetParent(hitParent, true);

                stuck = true;
                if (owner != null) owner.OnLocalDartLanded();

                Destroy(gameObject, Mathf.Max(1f, owner.destroyAfterStickSeconds));
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return 2f * (1f - t) * (b - a) + 2f * t * (c - b);
        }
    }
}
