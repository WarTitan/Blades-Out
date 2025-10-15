using UnityEngine;
using Mirror;
using System.Linq;

[AddComponentMenu("Cards/Card Raycaster (Root)")]
public class CardRaycasterOnRoot : NetworkBehaviour
{
    [Header("Refs (auto)")]
    public Camera cam;   // auto-found at OnStartLocalPlayer

    [Header("Raycast")]
    public LayerMask cardMask;     // auto-set to "Card" if empty
    public LayerMask playerMask;   // auto-set to "Player" if empty
    public float maxDistance = 100f;
    public bool hitTriggers = false;
    [Range(0f, 0.2f)] public float sphereCastRadius = 0.05f;

    [Header("Selection rules")]
    public bool onlySelectOwnCards = true;     // block selecting opponent cards
    public bool requireInHandToSelect = true;  // block selecting set-row cards

    [Header("Gameplay rules")]
    public bool requireTurnToCast = true;      // must be your turn to cast
    public bool blockSelfTarget = true;      // cannot cast on yourself
    public bool onlyCastFromHand = true;      // block casting set-row cards

    [Header("Keys")]
    public KeyCode setKey = KeyCode.V;          // move selected hand card to Set row
    public KeyCode deselectKey = KeyCode.Mouse1;     // right click
    public KeyCode debugCastNext = KeyCode.F;          // quick-cast to any other player
    public KeyCode debugRayAllKey = KeyCode.BackQuote;  // ~  dump ray hits
    public KeyCode debugDiagKey = KeyCode.L;          // print diagnostics
    public KeyCode dropTestCube = KeyCode.T;          // spawn a cube 2m ahead (aim sanity)

    private CardView selectedCard;
    private bool didPrintSetup;

    public override void OnStartLocalPlayer()
    {
        // Auto-find local camera (even if disabled in Awake)
        if (!cam)
        {
            var cams = GetComponentsInChildren<Camera>(true);
            if (cams.Length > 0) cam = cams[0];
        }

        // Auto-fill masks if empty
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

        // Camera may be enabled later; keep trying
        if (!cam)
        {
            var cams = GetComponentsInChildren<Camera>(true);
            if (cams.Length > 0) cam = cams[0];
            if (!cam) { if (!didPrintSetup) { Debug.LogWarning("[Raycaster] No Camera found under local player."); didPrintSetup = true; } return; }
        }

        // Debug helpers
        if (Input.GetKeyDown(debugDiagKey)) PrintDiagnostics();
        if (Input.GetKeyDown(dropTestCube)) DropCubeForAimTest();

        // LEFT CLICK
        if (Input.GetMouseButtonDown(0))
        {
            var me = GetComponent<PlayerState>();

            // If a card is already selected, try to cast on a player
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

            // Otherwise: try selecting a card under the crosshair
            var cv = RaycastCard();
            if (cv != null)
            {
                // Selection filters
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

        // RIGHT CLICK → deselect
        if (Input.GetKeyDown(deselectKey))
            Deselect();

        // V → Set selected hand card
        if (Input.GetKeyDown(setKey))
        {
            if (selectedCard == null)
            {
                Debug.Log("[Raycaster] No selected card to Set.");
            }
            else if (!selectedCard.isInHand)
            {
                Debug.LogWarning("[Raycaster] Selected card is not in hand; cannot Set.");
            }
            else
            {
                var me = GetComponent<PlayerState>();
                Debug.Log($"[Raycaster] CmdSetCard({selectedCard.handIndex})");
                me.CmdSetCard(selectedCard.handIndex);
                Deselect();
            }
        }

        // F → debug cast to any other player (handy for testing)
        if (Input.GetKeyDown(debugCastNext))
        {
            if (selectedCard == null) { Debug.Log("[Raycaster] Select a hand card first, then press F."); return; }
            var me = GetComponent<PlayerState>();
            var target = FindAnyOtherPlayer(me);
            if (target != null) TryCastSelectedOn(target);
            else Debug.LogWarning("[Raycaster] No other player found.");
        }

        // ~ → dump all ray hits along forward
        if (Input.GetKeyDown(debugRayAllKey))
            DebugRayEverything();

        Debug.DrawRay(cam.transform.position, cam.transform.forward * 1.0f, Color.cyan, 0f, false);
    }

    // ---------- selection / casting ----------
    void SelectCard(CardView cv)
    {
        if (selectedCard == cv) return;
        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = cv;
        selectedCard.SetSelected(true);
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
        if (onlyCastFromHand && !selectedCard.isInHand) { Debug.LogWarning("[Raycaster] Selected card is not in hand; cannot cast."); return; }
        if (requireTurnToCast && !(TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(me)))
        {
            Debug.LogWarning("[Raycaster] Not your turn.");
            return;
        }
        if (selectedCard.handIndex < 0 || selectedCard.handIndex >= me.handIds.Count)
        {
            Debug.LogWarning("[Raycaster] Hand index invalid (hand changed?).");
            Deselect();
            return;
        }

        Debug.Log($"[Raycaster] CmdPlayInstant(handIndex={selectedCard.handIndex}, target={target.netId})");
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
    void DebugRayEverything()
    {
        if (!cam) { Debug.Log("[Raycaster] DebugRayAll: no cam."); return; }
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        var hits = Physics.RaycastAll(ray, maxDistance, ~0,
                    hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        if (hits.Length == 0) { Debug.Log("[Raycaster] DebugRayAll: hit nothing."); return; }
        Debug.Log($"[Raycaster] DebugRayAll: {hits.Length} hit(s):");
        foreach (var h in hits)
            Debug.Log($"  - {h.collider.name} (layer {LayerMask.LayerToName(h.collider.gameObject.layer)}) dist {h.distance:F2} parent {h.collider.transform.root.name}");
    }

    void PrintDiagnostics()
    {
        var me = GetComponent<PlayerState>();
        var camName = cam ? cam.name : "<none>";
        var camPos = cam ? cam.transform.position.ToString("F2") : "<none>";
        int cardLayer = LayerMask.NameToLayer("Card");
        int playerLayer = LayerMask.NameToLayer("Player");

        var allCols = FindObjectsOfType<Collider>(true);
        int cardCols = allCols.Count(c => c.gameObject.layer == cardLayer && c.enabled && c.gameObject.activeInHierarchy);
        int plyrCols = allCols.Count(c => c.gameObject.layer == playerLayer && c.enabled && c.gameObject.activeInHierarchy);

        Debug.Log($"[Raycaster] DIAG: cam={camName} @ {camPos}, seat={(me ? me.seatIndex : -1)}");
        Debug.Log($"[Raycaster] DIAG: cardMask={(int)cardMask.value} (Card layer={cardLayer}), playerMask={(int)playerMask.value} (Player layer={playerLayer})");
        Debug.Log($"[Raycaster] DIAG: active colliders -> Card:{cardCols}, Player:{plyrCols} (scene total:{allCols.Length})");
        didPrintSetup = true;
    }

    void DropCubeForAimTest()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = cam.transform.position + cam.transform.forward * 2f;
        cube.transform.localScale = Vector3.one * 0.1f;
        Destroy(cube, 2f);
    }

    // ---------- helpers ----------
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
