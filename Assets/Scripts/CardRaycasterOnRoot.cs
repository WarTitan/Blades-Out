using UnityEngine;
using Mirror;
using UnityEngine.EventSystems;
using System.Text;

public class CardRaycasterOnRoot : MonoBehaviour
{
    [Header("Raycast")]
    public Camera cam;
    public float maxDistance = 100f;
    [Tooltip("Optional filter. Leave empty to hit everything.")]
    public LayerMask raycastMask = ~0;

    [Header("Debug")]
    public bool verbose = false;

    [Header("Debug Overlay (On-Screen)")]
    public bool debugOverlay = false;
    public Vector2 overlayPosition = new Vector2(16, 16);
    public int overlayWidth = 420;

    // selection (hand)
    int selectedHandIndex = -1;
    CardView selectedView;

    PlayerState localPlayer;
    TurnManager tm;

    // last debug reason shown in overlay
    string lastFailureReason = "";
    float lastFailureTime = 0f;

    // === PUBLIC API used by PlayerHotkeys/UI ===
    public int SelectedHandIndex => selectedHandIndex;
    public void DeselectPublic() => ClearSelection();
    public void PublicTrySetSelected() => TrySetSelected();
    public void PublicTryCastSelectedOn(PlayerState target) => TryCastSelectedOn(target);
    public void PublicSelectCardInHand(CardView cv) => SelectCardInHand(cv);

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (localPlayer == null) TryResolveLocalPlayer();
        if (tm == null) tm = TurnManager.Instance;
        if (!cam || !localPlayer) return;

        // Right-click = deselect
        if (Input.GetMouseButtonDown(1))
        {
            ClearSelection();
            return;
        }

        // Ignore clicks over UI
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;

        // Left click = main interaction
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            if (TryRaycast(out hit))
            {
                // 1) Card clicked?
                var cv = hit.transform.GetComponentInParent<CardView>();
                if (cv && cv.owner == localPlayer)
                {
                    if (cv.isInHand)
                    {
                        HandleClickHandCard(cv);
                        return;
                    }
                    else
                    {
                        // set-row card clicked -> ignore for play, just clear
                        if (verbose) Debug.Log("[Raycaster] Clicked a set-row card. Selection cleared.");
                        ClearSelection();
                        return;
                    }
                }

                // 2) Player clicked? (cast for InstantWithTarget if we have selection)
                var ps = hit.transform.GetComponentInParent<PlayerState>();
                if (ps && selectedHandIndex >= 0)
                {
                    TryCastSelectedOn(ps);
                    return;
                }
            }

            // Clicked nothing relevant
            ClearSelection();
        }
    }

    void OnGUI()
    {
        if (!debugOverlay || !localPlayer) return;

        var sb = new StringBuilder();
        sb.AppendLine("<b>Raycaster Debug</b>");

        bool myTurn = tm && tm.IsPlayersTurn(localPlayer);
        sb.AppendLine($"Turn: {(myTurn ? "YOURS" : "Other")}");
        sb.AppendLine($"Chips: {localPlayer.chips}/{localPlayer.maxChips}   Gold: {localPlayer.gold}");
        sb.AppendLine($"Set Count: {localPlayer.setIds.Count}");

        if (selectedHandIndex >= 0 && selectedHandIndex < localPlayer.handIds.Count)
        {
            var id = localPlayer.handIds[selectedHandIndex];
            var lvl = localPlayer.handLvls[selectedHandIndex];
            var db = localPlayer.database;
            var def = db ? db.Get(id) : null;

            sb.AppendLine("<b>Selected:</b>");
            if (def != null)
            {
                sb.AppendLine($"- {def.cardName}  (id {id}, L{lvl}, style {def.playStyle}, effect {def.effect})");
                sb.AppendLine($"- Chip Cost: {def.GetCastChipCost(lvl)}");

                string castReason, setReason;
                CanCastReason(out castReason, localPlayer, selectedHandIndex, target: null);
                CanSetReason(out setReason, localPlayer, selectedHandIndex);

                sb.AppendLine($"Cast OK? {(string.IsNullOrEmpty(castReason) ? "Yes" : "No")} {(castReason ?? "")}");
                sb.AppendLine($"Set  OK? {(string.IsNullOrEmpty(setReason) ? "Yes" : "No")} {(setReason ?? "")}");
            }
            else
            {
                sb.AppendLine($"- id {id} (no def), L{lvl}");
            }
        }
        else
        {
            sb.AppendLine("<i>No hand card selected</i>");
        }

        if (!string.IsNullOrEmpty(lastFailureReason))
        {
            sb.AppendLine("");
            sb.AppendLine($"<color=orange>Last Fail:</color> {lastFailureReason}");
        }

        var style = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };
        Rect r = new Rect(overlayPosition.x, overlayPosition.y, overlayWidth, Screen.height * 0.5f);
        GUI.Box(r, sb.ToString(), style);
    }

    // ───────── Core helpers ─────────
    void TryResolveLocalPlayer()
    {
        if (!NetworkClient.active || NetworkClient.localPlayer == null) return;
        localPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        if (!cam) cam = Camera.main;
        if (verbose && localPlayer != null) Debug.Log("[Raycaster] Local player & camera resolved.");
    }

    bool TryRaycast(out RaycastHit hit)
    {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        if (raycastMask.value == ~0)
            return Physics.Raycast(r, out hit, maxDistance, ~0, QueryTriggerInteraction.Collide);
        else
            return Physics.Raycast(r, out hit, maxDistance, raycastMask, QueryTriggerInteraction.Collide);
    }

    void HandleClickHandCard(CardView cv)
    {
        bool myTurn = tm && tm.IsPlayersTurn(localPlayer);

        // NOT my turn → try upgrade immediately
        if (!myTurn)
        {
            int handIndex = cv.handIndex;
            if (handIndex >= 0)
            {
                if (verbose) Debug.Log($"[Raycaster] Off-turn click: try upgrade hand[{handIndex}]");
                localPlayer.CmdUpgradeCard(handIndex);
            }
            ClearSelection();
            return;
        }

        // MY turn
        var db = localPlayer.database;
        var def = db ? db.Get(cv.cardId) : null;
        if (def == null) { ClearSelection(); return; }

        int handIndexNow = cv.handIndex;
        int lvl = cv.level;

        switch (def.playStyle)
        {
            case CardDefinition.PlayStyle.Instant:
                {
                    // Auto-cast immediately
                    string reasonCast;
                    if (!string.IsNullOrEmpty(CanCastReason(out reasonCast, localPlayer, handIndexNow, target: null)))
                    {
                        Fail(reasonCast);
                        ClearSelection();
                        return;
                    }
                    localPlayer.CmdPlayInstant(handIndexNow, 0);
                    if (verbose) Debug.Log("[Raycaster] Auto-cast Instant.");
                    ClearSelection();
                    break;
                }

            case CardDefinition.PlayStyle.InstantWithTarget:
                {
                    // Select, then wait for enemy click
                    SelectCardInHand(cv);
                    if (verbose) Debug.Log("[Raycaster] Selected InstantWithTarget. Click an enemy to cast.");
                    break;
                }

            case CardDefinition.PlayStyle.SetReaction:
                {
                    // Auto-set if allowed (chips + capacity)
                    string reasonSet;
                    if (!string.IsNullOrEmpty(CanSetReason(out reasonSet, localPlayer, handIndexNow)))
                    {
                        Fail(reasonSet);
                        ClearSelection();
                        return;
                    }
                    localPlayer.CmdSetCard(handIndexNow);
                    if (verbose) Debug.Log("[Raycaster] Auto-set SetReaction.");
                    ClearSelection();
                    break;
                }
        }
    }

    void SelectCardInHand(CardView cv)
    {
        if (cv == null || cv.owner != localPlayer || !cv.isInHand) return;
        ClearSelection(); // single select
        selectedView = cv;
        selectedHandIndex = cv.handIndex;
        if (verbose) Debug.Log($"[Raycaster] Selected hand[{selectedHandIndex}] {DescribeSelectedForLog()}");
    }

    void ClearSelection()
    {
        selectedHandIndex = -1;
        selectedView = null;
        lastFailureReason = "";
    }

    // Called by hotkeys/UI: try to set the currently selected hand card
    void TrySetSelected()
    {
        if (localPlayer == null || tm == null) return;
        if (selectedHandIndex < 0) { Fail("Select a HAND card to set."); return; }

        string reason;
        if (!string.IsNullOrEmpty(CanSetReason(out reason, localPlayer, selectedHandIndex)))
        {
            Fail(reason);
            return;
        }

        localPlayer.CmdSetCard(selectedHandIndex);
        if (verbose) Debug.Log("[Raycaster] Set OK -> CmdSetCard sent.");
        ClearSelection();
    }

    void TryCastSelectedOn(PlayerState target)
    {
        if (localPlayer == null || tm == null) return;
        if (selectedHandIndex < 0) return;

        // Ensure selected card is InstantWithTarget
        var db = localPlayer.database;
        var def = db ? db.Get(localPlayer.handIds[selectedHandIndex]) : null;
        if (def == null) { ClearSelection(); return; }
        if (def.playStyle != CardDefinition.PlayStyle.InstantWithTarget)
        {
            ClearSelection();
            return;
        }

        string reason;
        if (!string.IsNullOrEmpty(CanCastReason(out reason, localPlayer, selectedHandIndex, target)))
        {
            Fail(reason);
            return;
        }

        uint netId = target ? target.netId : 0;
        localPlayer.CmdPlayInstant(selectedHandIndex, netId);
        if (verbose) Debug.Log("[Raycaster] Cast on target OK.");
        ClearSelection();
    }

    // ───────── Validation + Debug helpers ─────────
    string DescribeSelectedForLog()
    {
        if (localPlayer == null || selectedHandIndex < 0 || selectedHandIndex >= localPlayer.handIds.Count)
            return "none";

        int id = localPlayer.handIds[selectedHandIndex];
        int lvl = localPlayer.handLvls[selectedHandIndex];
        var def = localPlayer.database ? localPlayer.database.Get(id) : null;

        if (def == null) return $"id={id}, L{lvl} (no def)";
        int chip = def.GetCastChipCost(lvl);
        bool myTurn = tm && tm.IsPlayersTurn(localPlayer);
        return $"{def.cardName} (id {id}) L{lvl} [{def.playStyle}] effect={def.effect}, chipCost={chip}, myTurn={myTurn}, chips={localPlayer.chips}/{localPlayer.maxChips}, gold={localPlayer.gold}, setCount={localPlayer.setIds.Count}";
    }

    string CanCastReason(out string reason, PlayerState me, int handIndex, PlayerState target)
    {
        reason = null;
        if (tm != null && !tm.IsPlayersTurn(me))
        {
            reason = "Cast denied: not your turn.";
            return reason;
        }

        if (handIndex < 0 || handIndex >= me.handIds.Count)
        {
            reason = "Cast denied: invalid hand index.";
            return reason;
        }

        var db = me.database;
        if (!db)
        {
            reason = "Cast denied: no CardDatabase on player.";
            return reason;
        }

        int id = me.handIds[handIndex];
        var def = db.Get(id);
        if (def == null)
        {
            reason = $"Cast denied: no definition for cardId={id}.";
            return reason;
        }

        // Must be Instant or InstantWithTarget
        if (def.playStyle == CardDefinition.PlayStyle.SetReaction)
        {
            reason = $"Cast denied: '{def.cardName}' is a SetReaction card.";
            return reason;
        }

        // Cost check
        int lvl = me.handLvls[handIndex];
        int cost = def.GetCastChipCost(lvl);
        if (me.chips < cost)
        {
            reason = $"Cast denied: need {cost} chips, you have {me.chips}.";
            return reason;
        }

        // If InstantWithTarget → require target
        if (def.playStyle == CardDefinition.PlayStyle.InstantWithTarget && target == null)
        {
            reason = $"Cast denied: '{def.cardName}' needs a target.";
            return reason;
        }

        return reason; // null = OK
    }

    string CanSetReason(out string reason, PlayerState me, int handIndex)
    {
        reason = null;

        // Set only during YOUR turn
        if (tm != null && !tm.IsPlayersTurn(me))
        {
            reason = "Set denied: only during YOUR turn.";
            return reason;
        }

        // Only if you have 0 set cards
        if (me.setIds.Count > 0)
        {
            reason = "Set denied: you already have a set card.";
            return reason;
        }

        if (handIndex < 0 || handIndex >= me.handIds.Count)
        {
            reason = "Set denied: invalid hand index.";
            return reason;
        }

        var db = me.database;
        if (!db)
        {
            reason = "Set denied: no CardDatabase on player.";
            return reason;
        }

        int id = me.handIds[handIndex];
        var def = db.Get(id);
        if (def == null)
        {
            reason = $"Set denied: no definition for cardId={id}.";
            return reason;
        }

        if (def.playStyle != CardDefinition.PlayStyle.SetReaction)
        {
            reason = $"Set denied: '{def.cardName}' is not a SetReaction card.";
            return reason;
        }

        // Cost check for setting now
        int lvl = me.handLvls[handIndex];
        int cost = def.GetCastChipCost(lvl);
        if (me.chips < cost)
        {
            reason = $"Set denied: need {cost} chips, you have {me.chips}.";
            return reason;
        }

        return reason; // null = OK
    }

    void Fail(string reason)
    {
        lastFailureReason = reason;
        lastFailureTime = Time.time;
        Debug.LogWarning("[Raycaster] " + reason);
    }
}
