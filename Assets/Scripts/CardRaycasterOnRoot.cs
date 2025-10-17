using UnityEngine;
using Mirror;

[AddComponentMenu("Cards/Card Raycaster (Root)")]
public class CardRaycasterOnRoot : NetworkBehaviour
{
    [Header("Refs (auto)")]
    public Camera cam;

    [Header("Raycast")]
    public LayerMask cardMask;
    public LayerMask playerMask;
    public float maxDistance = 100f;
    public bool hitTriggers = false;
    [Range(0f, 0.2f)] public float sphereCastRadius = 0.05f;

    [Header("Selection rules")]
    public bool onlySelectOwnCards = true;
    public bool requireInHandToSelect = true;

    [Header("Gameplay rules")]
    public bool requireTurnToCast = true;
    public bool blockSelfTarget = true;
    public bool onlyCastFromHand = true;

    [Header("Mouse")]
    public KeyCode deselectMouse = KeyCode.Mouse1;

    private CardView selectedCard;
    private bool didPrintSetup;

    // Public access for the hotkey script
    public int SelectedHandIndex => selectedCard != null ? selectedCard.handIndex : -1;
    public void DeselectPublic() => Deselect();

    public override void OnStartLocalPlayer()
    {
        if (!cam)
        {
            var cams = GetComponentsInChildren<Camera>(true);
            if (cams.Length > 0) cam = cams[0];
        }

        if (cardMask.value == 0)
        {
            int idx = LayerMask.NameToLayer("Card");
            if (idx >= 0) cardMask = 1 << idx;
            else Debug.LogWarning("[Raycaster] Layer 'Card' does not exist. Add it in Project Settings > Tags & Layers.");
        }
        if (playerMask.value == 0)
        {
            int idx = LayerMask.NameToLayer("Player");
            if (idx >= 0) playerMask = 1 << idx;
            else Debug.LogWarning("[Raycaster] Layer 'Player' does not exist. Add it in Project Settings > Tags & Layers.");
        }

        PrintDiagnostics();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (!cam)
        {
            var cams = GetComponentsInChildren<Camera>(true);
            if (cams.Length > 0) cam = cams[0];
            if (!cam)
            {
                if (!didPrintSetup)
                {
                    Debug.LogWarning("[Raycaster] No Camera found under local player.");
                    didPrintSetup = true;
                }
                return;
            }
        }

        // LEFT CLICK: select a card or cast on a player if a card is selected
        if (Input.GetMouseButtonDown(0))
        {
            var me = GetComponent<PlayerState>();

            // If a card is selected, try to cast on a player
            if (selectedCard != null)
            {
                var target = RaycastPlayer();
                if (target != null)
                {
                    if (blockSelfTarget && target == me)
                    {
                        Debug.Log("[Raycaster] Ignored: cannot target yourself.");
                    }
                    else
                    {
                        TryCastSelectedOn(target);
                        return;
                    }
                }
            }

            // Otherwise: try selecting a card
            var cv = RaycastCard();
            if (cv != null)
            {
                if (onlySelectOwnCards && cv.owner != me)
                {
                    Debug.Log("[Raycaster] Ignored: not your card.");
                }
                else if (requireInHandToSelect && !cv.isInHand)
                {
                    Debug.Log("[Raycaster] Ignored: card is not in hand.");
                }
                else
                {
                    SelectCard(cv);
                }
            }
            else
            {
                Debug.Log("[Raycaster] No card under crosshair.");
            }
        }

        // RIGHT CLICK -> deselect
        if (Input.GetKeyDown(deselectMouse))
            Deselect();

        Debug.DrawRay(cam.transform.position, cam.transform.forward * 1.0f, Color.cyan, 0f, false);
    }

    // ---------- selection / casting ----------
    void SelectCard(CardView cv)
    {
        if (selectedCard == cv) return;
        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = cv;
        selectedCard.SetSelected(true);
        Debug.Log("[Raycaster] Selected card id=" + cv.cardId + ", handIndex=" + cv.handIndex + ", inHand=" + cv.isInHand);
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
        if (onlyCastFromHand && !selectedCard.isInHand) { Debug.LogWarning("[Raycaster] Selected card is not in hand; cannot cast."); return; }

        if (requireTurnToCast)
        {
            var tm = TurnManager.Instance;
            if (tm == null) { Debug.LogWarning("[Raycaster] TurnManager not ready yet."); return; }
            if (!tm.IsPlayersTurn(me)) { Debug.LogWarning("[Raycaster] Not your turn."); return; }
        }

        if (selectedCard.handIndex < 0 || selectedCard.handIndex >= me.handIds.Count)
        {
            Debug.LogWarning("[Raycaster] Hand index invalid (hand changed?).");
            Deselect();
            return;
        }

        Debug.Log("[Raycaster] CmdPlayInstant(handIndex=" + selectedCard.handIndex + ", target=" + target.netId + ")");
        me.CmdPlayInstant(selectedCard.handIndex, target.netId);
        Deselect();
    }

    // ---------- raycasts ----------
    CardView RaycastCard()
    {
        var q = hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (cardMask.value != 0 && Physics.Raycast(ray, out var hit, maxDistance, cardMask, q))
        {
            var cv = hit.collider.GetComponentInParent<CardView>();
            if (cv != null) return cv;
        }
        if (cardMask.value != 0 && sphereCastRadius > 0f &&
            Physics.SphereCast(ray, sphereCastRadius, out var sh, maxDistance, cardMask, q))
        {
            var cv = sh.collider.GetComponentInParent<CardView>();
            if (cv != null) return cv;
        }
        return null;
    }

    PlayerState RaycastPlayer()
    {
        var q = hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (playerMask.value != 0 && Physics.Raycast(ray, out var hit, maxDistance, playerMask, q))
        {
            var ps = hit.collider.GetComponentInParent<PlayerState>();
            if (ps != null) return ps;
        }
        if (playerMask.value != 0 && sphereCastRadius > 0f &&
            Physics.SphereCast(ray, sphereCastRadius, out var sh, maxDistance, playerMask, q))
        {
            var ps = sh.collider.GetComponentInParent<PlayerState>();
            if (ps != null) return ps;
        }
        return null;
    }

    // ---------- debug ----------
    void PrintDiagnostics()
    {
        var me = GetComponent<PlayerState>();
        var camName = cam ? cam.name : "<none>";
        var camPos = cam ? cam.transform.position.ToString("F2") : "<none>";
        Debug.Log("[Raycaster] DIAG cam=" + camName + " @ " + camPos + ", seat=" + (me ? me.seatIndex : -1));
        Debug.Log("[Raycaster] DIAG masks -> cardMask=" + (int)cardMask.value + ", playerMask=" + (int)playerMask.value);
    }
}
