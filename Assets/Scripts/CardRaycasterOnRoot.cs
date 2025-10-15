using UnityEngine;
using Mirror;

[AddComponentMenu("Cards/Card Raycaster (Root)")]
public class CardRaycasterOnRoot : NetworkBehaviour
{
    [Header("Refs (auto)")]
    public Camera cam;   // auto-find child camera

    [Header("Raycast")]
    public LayerMask cardMask;     // set to Card layer
    public LayerMask playerMask;   // set to Player layer
    public float maxDistance = 100f;
    public bool hitTriggers = false;
    [Range(0f, 0.2f)] public float sphereCastRadius = 0.03f;

    [Header("Rules")]
    public bool requireTurnToCast = true;
    public bool handOnlyForUse = true;

    [Header("Keys")]
    public KeyCode setKey = KeyCode.V;
    public KeyCode deselectKey = KeyCode.Mouse1;
    public KeyCode debugCastNext = KeyCode.F;
    public KeyCode debugRayAllKey = KeyCode.BackQuote; // ~

    private CardView selectedCard;

    public override void OnStartLocalPlayer()
    {
        if (!cam)
        {
            var cams = GetComponentsInChildren<Camera>(true); // finds disabled too
            if (cams.Length > 0) cam = cams[0];
        }
        if (!cam) Debug.LogWarning("[Raycaster] No Camera found under local player.");
        else Debug.Log($"[Raycaster] Using camera: {cam.name} @ {cam.transform.position}");
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (!cam) return;

        // Hover/select
        if (Input.GetMouseButtonDown(0))
        {
            // If we already selected a card, try to cast at a player
            if (selectedCard != null)
            {
                var target = RaycastPlayer();
                if (target != null && target != GetComponent<PlayerState>())
                {
                    TryCastSelectedOn(target);
                    return;
                }
            }

            // Otherwise try to select a card under the crosshair
            var cv = RaycastCard();
            if (cv != null) SelectCard(cv);
            else Debug.Log("[Raycaster] No card under crosshair.");
        }

        if (Input.GetKeyDown(deselectKey)) Deselect();

        if (Input.GetKeyDown(setKey))
        {
            if (selectedCard == null) { Debug.Log("[Raycaster] No selected card to Set."); }
            else if (!selectedCard.isInHand) { Debug.LogWarning("[Raycaster] Selected card is not in hand; cannot Set."); }
            else
            {
                var me = GetComponent<PlayerState>();
                Debug.Log($"[Raycaster] CmdSetCard({selectedCard.handIndex})");
                me.CmdSetCard(selectedCard.handIndex);
                Deselect();
            }
        }

        if (Input.GetKeyDown(debugCastNext))
        {
            if (selectedCard == null) { Debug.Log("[Raycaster] Select a hand card first, then press F."); return; }
            var me = GetComponent<PlayerState>();
            var target = FindAnyOtherPlayer(me);
            if (target != null) TryCastSelectedOn(target);
            else Debug.LogWarning("[Raycaster] No other player found.");
        }

        if (Input.GetKeyDown(debugRayAllKey)) DebugRayEverything();
    }

    // ---- selection/casting ----
    void SelectCard(CardView cv)
    {
        if (selectedCard == cv) return;
        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = cv; selectedCard.SetSelected(true);
        Debug.Log($"[Raycaster] Selected card id={cv.cardId}, handIndex={cv.handIndex}, inHand={cv.isInHand}");
    }

    void Deselect()
    {
        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = null;
        Debug.Log("[Raycaster] Deselected.");
    }

    void TryCastSelectedOn(PlayerState target)
    {
        var me = GetComponent<PlayerState>();
        if (selectedCard == null) { Debug.LogWarning("[Raycaster] No selected card to cast."); return; }
        if (handOnlyForUse && !selectedCard.isInHand) { Debug.LogWarning("[Raycaster] Selected card is not in hand; cannot cast."); return; }
        if (requireTurnToCast && !(TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(me))) { Debug.LogWarning("[Raycaster] Not your turn."); return; }
        if (selectedCard.handIndex < 0 || selectedCard.handIndex >= me.handIds.Count) { Debug.LogWarning("[Raycaster] Hand index invalid."); Deselect(); return; }

        Debug.Log($"[Raycaster] CmdPlayInstant(handIndex={selectedCard.handIndex}, target={target.netId})");
        me.CmdPlayInstant(selectedCard.handIndex, target.netId);
        Deselect();
    }

    // ---- raycasts ----
    CardView RaycastCard()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        var q = hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        if (Physics.Raycast(ray, out var hit, maxDistance, cardMask, q))
        {
            var cv = hit.collider.GetComponentInParent<CardView>();
            if (cv != null) return cv;
        }
        if (sphereCastRadius > 0f &&
            Physics.SphereCast(ray, sphereCastRadius, out var sh, maxDistance, cardMask, q))
        {
            var cv = sh.collider.GetComponentInParent<CardView>();
            if (cv != null) return cv;
        }
        return null;
    }

    PlayerState RaycastPlayer()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        var q = hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        if (Physics.Raycast(ray, out var hit, maxDistance, playerMask, q))
        {
            var ps = hit.collider.GetComponentInParent<PlayerState>();
            if (ps != null) return ps;
        }
        if (sphereCastRadius > 0f &&
            Physics.SphereCast(ray, sphereCastRadius, out var sh, maxDistance, playerMask, q))
        {
            var ps = sh.collider.GetComponentInParent<PlayerState>();
            if (ps != null) return ps;
        }
        return null;
    }

    void DebugRayEverything()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        var hits = Physics.RaycastAll(ray, maxDistance, ~0, hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        if (hits.Length == 0) { Debug.Log("[Raycaster] DebugRayAll: hit nothing."); return; }
        Debug.Log($"[Raycaster] DebugRayAll: {hits.Length} hit(s):");
        foreach (var h in hits)
            Debug.Log($"  - {h.collider.name} (layer {LayerMask.LayerToName(h.collider.gameObject.layer)}) dist {h.distance:F2}");
    }

    // ---- helpers ----
    PlayerState FindAnyOtherPlayer(PlayerState me)
    {
        foreach (var kv in NetworkServer.spawned)
        {
            var ps = kv.Value.GetComponent<PlayerState>();
            if (ps && ps != me) return ps;
        }
        return null;
    }
}
