using UnityEngine;
using UnityEngine.EventSystems;

/// Smooth scale-on-hover that reuses CardRaycasterOnRoot's camera & mask.
/// Always finds the PinToAnchor owning the card and attaches HoverLift there to drive externalScale.
[RequireComponent(typeof(CardRaycasterOnRoot))]
[AddComponentMenu("Cards/Card Hover Via Raycaster")]
public class CardHoverViaRaycaster : MonoBehaviour
{
    [Header("Hover Style")]
    public float scaleMultiplier = 1.20f;   // 1.0 -> 1.2 on hover
    public float durationSeconds = 1.00f;   // 1s in/out

    [Header("Behavior")]
    public bool allowHoverOverUI = true;    // if false, UI under mouse blocks hover
    public float maxDistance = 200f;        // fallback if raycaster.maxDistance not set

    [Header("Debug")]
    public bool logHostChanges = false;
    public bool logNoPinWarning = false;

    private CardRaycasterOnRoot raycaster;
    private HoverLift current;
    private GameObject currentHost;

    void Awake()
    {
        raycaster = GetComponent<CardRaycasterOnRoot>() ?? GetComponentInChildren<CardRaycasterOnRoot>(true);
    }

    void OnDisable() { ClearHover(); }

    void Update()
    {
        if (!raycaster) return;

        if (!allowHoverOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        Camera cam = raycaster.cam != null ? raycaster.cam : Camera.main;
        if (cam == null || !cam.isActiveAndEnabled) { ClearHover(); return; }

        LayerMask mask = raycaster.raycastMask.value == 0 ? ~0 : raycaster.raycastMask;
        float dist = raycaster.maxDistance > 0f ? raycaster.maxDistance : maxDistance;

        // Raycast ALL and pick nearest card-like thing
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, dist, mask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) { ClearHover(); return; }
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Host = the GameObject that actually has the PinToAnchor controlling this card
        GameObject host = null;
        PinToAnchor pin = null;

        for (int i = 0; i < hits.Length; i++)
        {
            var tr = hits[i].transform;
            if (tr == null) continue;

            // Try direct PinToAnchor up the chain first (fast path)
            pin = tr.GetComponentInParent<PinToAnchor>(true);
            if (pin != null) { host = pin.gameObject; break; }

            // Otherwise, search card components then find their PinToAnchor parent
            var pick = tr.GetComponentInParent<DraftPickChoice>(true);
            if (pick)
            {
                pin = pick.GetComponentInParent<PinToAnchor>(true) ?? pick.GetComponent<PinToAnchor>();
                host = (pin != null) ? pin.gameObject : pick.gameObject;
                break;
            }

            var cv = tr.GetComponentInParent<CardView>(true);
            if (cv)
            {
                pin = cv.GetComponentInParent<PinToAnchor>(true) ?? cv.GetComponent<PinToAnchor>();
                host = (pin != null) ? pin.gameObject : cv.gameObject;
                break;
            }

            var adapter = tr.GetComponentInParent<Card3DAdapter>(true);
            if (adapter)
            {
                pin = adapter.GetComponentInParent<PinToAnchor>(true) ?? adapter.GetComponent<PinToAnchor>();
                host = (pin != null) ? pin.gameObject : adapter.gameObject;
                break;
            }
        }

        if (host == null) { ClearHover(); return; }

        if (host != currentHost)
        {
            if (current != null) current.SetHovered(false);
            current = null;
            if (logHostChanges) Debug.Log("[CardHoverViaRaycaster] Hover host: " + host.name);
        }

        // Ensure HoverLift is ON THE PinToAnchor object (so it can drive externalScale)
        var hl = host.GetComponent<HoverLift>();
        if (!hl) hl = host.AddComponent<HoverLift>();

        // Configure HoverLift to drive PinToAnchor.externalScale
        hl.preferPinExternalScale = true;
        hl.scaleMultiplier = scaleMultiplier;
        hl.durationSeconds = durationSeconds;

        if (logNoPinWarning && pin == null)
            Debug.LogWarning("[CardHoverViaRaycaster] No PinToAnchor found on/above host '" + host.name + "'. HoverLift will scale transform.localScale instead.");

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
}
