using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;

/// Mouse-hover highlighter for cards, scale-only.
/// Locks to the LOCAL player's camera, RaycastAll, and recognizes DraftPickChoice / CardView / Card3DAdapter.
[AddComponentMenu("Cards/Card Hover Highlighter (Card3D Compatible)")]
public class CardHoverHighlighter : MonoBehaviour
{
    [Header("Camera Binding")]
    public Camera cam;                          // leave null to auto-bind to local player's camera
    public bool bindToLocalPlayerCamera = true;

    [Header("Raycast")]
    public LayerMask raycastMask = ~0;          // set to Everything (or include all layers you use)
    public float maxDistance = 200f;
    public bool allowHoverOverUI = true;        // if false, UI under mouse will block hover

    [Header("Targets")]
    public bool includeDraftChoices = true;     // hover draft choices too

    [Header("Hover Style (applied if HoverLift is auto-added)")]
    public float defaultScaleMultiplier = 1.20f; // scales to 1.2x
    public float defaultDurationSeconds = 1.00f;  // 1s in, 1s out

    private HoverLift current;
    private GameObject currentHost;
    private bool warnedNoCamera = false;
    private bool triedBind = false;

    void OnEnable() { triedBind = false; }

    void Start()
    {
        EnsureCameraBound();
        if (raycastMask.value == 0) raycastMask = ~0; // safety fallback
    }

    void Update()
    {
        EnsureCameraBound();

        if (cam == null || !cam.isActiveAndEnabled)
        {
            if (!warnedNoCamera)
            {
                Debug.LogWarning("[CardHoverHighlighter] No active camera. Assign 'cam' or ensure local player camera exists/enabled.");
                warnedNoCamera = true;
            }
            ClearHover();
            return;
        }

        if (!allowHoverOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        // Raycast ALL and pick the nearest qualifying card
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, raycastMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            ClearHover();
            return;
        }
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        GameObject host = null;
        for (int i = 0; i < hits.Length; i++)
        {
            var tr = hits[i].transform;
            if (tr == null) continue;

            // 1) DraftPickChoice (for the 3 draft cards)
            if (includeDraftChoices)
            {
                var pick = tr.GetComponentInParent<DraftPickChoice>();
                if (pick != null) { host = pick.gameObject; break; }
            }

            // 2) CardView (hand cards)
            var cv = tr.GetComponentInParent<CardView>();
            if (cv != null) { host = cv.gameObject; break; }

            // 3) Card3DAdapter (your Card3D prefab)
            var adapter = tr.GetComponentInParent<Card3DAdapter>();
            if (adapter != null) { host = adapter.gameObject; break; }
        }

        if (host == null)
        {
            ClearHover();
            return;
        }

        // If host changed, un-hover the previous one first
        if (host != currentHost)
        {
            if (current != null) current.SetHovered(false);
            current = null;
        }

        // Add (or get) the simple scale tweener on the host object only
        var hl = host.GetComponent<HoverLift>();
        if (!hl) hl = host.AddComponent<HoverLift>();

        // Apply simple 1.0 -> 1.2 in 1.0s (and back) settings
        hl.scaleMultiplier = defaultScaleMultiplier;
        hl.durationSeconds = defaultDurationSeconds;

        // Drive hover
        current = hl;
        currentHost = host;
        current.SetHovered(true);
    }

    private void ClearHover()
    {
        if (current != null) current.SetHovered(false);
        current = null;
        currentHost = null;
    }

    private void EnsureCameraBound()
    {
        if (cam != null && cam.isActiveAndEnabled) return;

        if (bindToLocalPlayerCamera && !triedBind)
        {
            triedBind = true;

            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                var root = NetworkClient.localPlayer.transform;

                // Prefer LocalCameraActivator.playerCamera on the local player
                var lca = root.GetComponentInChildren<LocalCameraActivator>(true);
                if (lca != null && lca.playerCamera != null)
                {
                    cam = lca.playerCamera;
                    return;
                }

                // Else any Camera under the local player
                var cams = root.GetComponentsInChildren<Camera>(true);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null) { cam = cams[i]; return; }
                }
            }

            // Fallbacks
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled) { cam = main; return; }

            Camera[] all = Camera.allCameras;
            Camera best = null;
            float bestDepth = float.NegativeInfinity;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.targetDisplay != 0) continue;
                if (c.depth >= bestDepth) { best = c; bestDepth = c.depth; }
            }
            if (best != null) cam = best;
        }
    }
}
