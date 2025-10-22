using UnityEngine;
using Mirror;
using UnityEngine.EventSystems;
using System.Text;

[AddComponentMenu("Cards/Card Raycaster (Root, Client-Only, Verbose)")]
public class CardRaycasterOnRoot : MonoBehaviour
{
    [Header("Raycast")]
    public Camera cam;
    public LayerMask raycastMask = ~0;
    public float maxDistance = 100f;

    [Header("Client gating")]
    public bool preferUpgradeWhenClientThinksOffTurn = false;

    [Header("Self-heal")]
    public bool allowOwnerInference = true;

    [Header("Debug")]
    public bool verbose = true;
    public bool debugOverlay = true;
    public Vector2 overlayPosition = new Vector2(16, 16);
    public int overlayWidth = 500;

    int selectedHandIndex = -1;
    CardView selectedView;

    PlayerState localPlayer;
    TurnManager tm;

    string lastFailureReason = "";
    float lastFailureTime = 0f;
    string lastHitInfo = "";

    // DB cache + warning gate
    CardDatabase cachedDB;
    static bool warnedNoDB = false;

    public int SelectedHandIndex => selectedHandIndex;
    public void DeselectPublic() => ClearSelection();
    public void PublicTrySetSelected() => TrySetSelected();
    public void PublicTryCastSelectedOn(PlayerState target) => TryCastSelectedOn(target);
    public void PublicSelectCardInHand(CardView cv) => SelectCardInHand(cv);

    void Start()
    {
        TryResolveLocalPlayer(true);
        if (!cam) cam = Camera.main;
        if (verbose) Debug.Log("[Raycaster] Boot complete.");
    }

    void Update()
    {
        if (localPlayer == null || tm == null || cam == null)
        {
            TryResolveLocalPlayer(false);
            if (!cam) cam = Camera.main;
        }
        if (localPlayer == null || cam == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            ClearSelection();
            return;
        }

        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            lastHitInfo = "";
            if (TryRaycast(out hit))
            {
                LogHitInfo(hit);

                var cv = hit.transform.GetComponentInParent<CardView>();
                if (cv)
                {
                    // Bind missing owner fast if the card lives under our PlayerState
                    if (allowOwnerInference && cv.owner == null)
                    {
                        var parentPs = hit.transform.GetComponentInParent<PlayerState>();
                        if (parentPs == localPlayer)
                        {
                            cv.Init(localPlayer, cv.handIndex, cv.cardId, cv.level, cv.isInHand);
                            if (verbose) Debug.Log("[Raycaster] Fixed missing CardView.owner by inferring parent PlayerState.");
                        }
                    }

                    if (cv.owner != localPlayer)
                    {
                        Warn($"Ignored card (not yours). ownerSeat={(cv.owner ? cv.owner.seatIndex : -999)} localSeat={localPlayer.seatIndex}");
                        ClearSelection();
                        return;
                    }

                    if (cv.isInHand)
                    {
                        HandleClickHandCard(cv);
                        return;
                    }
                    else
                    {
                        if (verbose) Debug.Log("[Raycaster] Clicked a set-row card. Selection cleared.");
                        ClearSelection();
                        return;
                    }
                }

                var ps2 = hit.transform.GetComponentInParent<PlayerState>();
                if (ps2 && selectedHandIndex >= 0)
                {
                    TryCastSelectedOn(ps2);
                    return;
                }
            }
            else
            {
                if (verbose) Debug.Log("[Raycaster] Raycast hit nothing.");
            }

            ClearSelection();
        }
    }

    void OnGUI()
    {
        if (!debugOverlay || !localPlayer) return;

        var sb = new StringBuilder();
        sb.AppendLine("<b>Raycaster Debug</b>");

        bool myTurn = tm && tm.IsPlayersTurn(localPlayer);
        sb.AppendLine($"Local: seat={localPlayer.seatIndex}, netId={localPlayer.netId}");
        sb.AppendLine($"Turn (client view): {(myTurn ? "YOURS" : "Other")}");
        sb.AppendLine($"Chips: {localPlayer.chips}/{localPlayer.maxChips}   Gold: {localPlayer.gold}");
        sb.AppendLine($"Set Count: {localPlayer.setIds.Count}");

        if (!string.IsNullOrEmpty(lastHitInfo))
        {
            sb.AppendLine("");
            sb.AppendLine("<b>Last Raycast:</b>");
            sb.AppendLine(lastHitInfo);
        }

        if (selectedHandIndex >= 0 && selectedHandIndex < localPlayer.handIds.Count)
        {
            int id = localPlayer.handIds[selectedHandIndex];
            int lvl = localPlayer.handLvls[selectedHandIndex];
            var def = ResolveDef(id);

            sb.AppendLine("");
            sb.AppendLine("<b>Selected:</b>");
            if (def != null)
            {
                var inferred = InferPlayStyle(def);
                sb.AppendLine($"- {def.cardName}  (id {id}, L{lvl}, style {def.playStyle} -> inferred {inferred}, effect {def.effect})");
                sb.AppendLine($"- Chip Cost: {def.GetCastChipCost(lvl)}");
            }
            else
            {
                sb.AppendLine($"- id {id} (no def), L{lvl}");
            }
        }
        else
        {
            sb.AppendLine("");
            sb.AppendLine("<i>No hand card selected</i>");
        }

        if (!string.IsNullOrEmpty(lastFailureReason))
        {
            sb.AppendLine("");
            sb.AppendLine($"<color=orange>Last Fail:</color> {lastFailureReason}");
        }

        var style = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };
        Rect r = new Rect(overlayPosition.x, overlayPosition.y, overlayWidth, Screen.height * 0.7f);
        GUI.Box(r, sb.ToString(), style);
    }

    // ---------- Core helpers ----------
    void TryResolveLocalPlayer(bool bootLog)
    {
        if (NetworkClient.active && NetworkClient.localPlayer != null)
        {
            var ps = NetworkClient.localPlayer.GetComponent<PlayerState>()
                     ?? NetworkClient.localPlayer.GetComponentInChildren<PlayerState>(true);
            if (ps != null && ps != localPlayer)
            {
                localPlayer = ps;
                tm = TurnManager.Instance;
                if (bootLog || verbose)
                    Debug.Log($"[Raycaster] Local player resolved (localPlayer): seat={ps.seatIndex}, netId={ps.netId}");
            }
        }

        // Pre-bind DB if possible
        ResolveDB();
    }

    CardDatabase ResolveDB()
    {
        if (cachedDB) return cachedDB;

        // 1) Use player's reference if present
        if (localPlayer && localPlayer.database) cachedDB = localPlayer.database;

        // 2) Find a CardDatabase MonoBehaviour in the scene
        if (!cachedDB)
        {
#if UNITY_2023_1_OR_NEWER
            cachedDB = Object.FindFirstObjectByType<CardDatabase>();
#else
            cachedDB = Object.FindObjectOfType<CardDatabase>();
#endif
        }

        // 3) As a last resort, check any PlayerState.database in scene
        if (!cachedDB)
        {
#if UNITY_2023_1_OR_NEWER
            var players = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
            var players = Object.FindObjectsOfType<PlayerState>();
#endif
            foreach (var p in players)
            {
                if (p != null && p.database != null)
                {
                    cachedDB = p.database;
                    break;
                }
            }
        }

        if (!cachedDB && !warnedNoDB)
        {
            warnedNoDB = true;
            Warn("No CardDatabase found in scene.");
        }
        else if (cachedDB && verbose)
        {
            Debug.Log("[Raycaster] CardDatabase resolved.");
        }

        // keep local player in sync (client-side only)
        if (cachedDB && localPlayer && localPlayer.database == null)
            localPlayer.database = cachedDB;

        return cachedDB;
    }

    CardDefinition ResolveDef(int cardId)
    {
        var db = ResolveDB();
        if (!db) return null;
        var def = db.Get(cardId);
        if (def == null && verbose) Debug.LogWarning($"[Raycaster] CardDefinition not found for cardId={cardId}.");
        return def;
    }

    bool TryRaycast(out RaycastHit hit)
    {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(r, out hit, maxDistance, raycastMask, QueryTriggerInteraction.Collide);
    }

    void HandleClickHandCard(CardView cv)
    {
        // Always attempt server command only when we know what to do.
        bool clientThinksMyTurn = tm && tm.IsPlayersTurn(localPlayer);

        if (verbose)
            Debug.Log("[Raycaster] Click hand card: handIndex=" + cv.handIndex + ", cardId=" + cv.cardId + ", lvl=" + cv.level + ", inHand=" + cv.isInHand);

        // Repair init if needed
        if ((cv.cardId <= 0 || cv.level <= 0) && cv.handIndex >= 0 && cv.handIndex < localPlayer.handIds.Count)
        {
            cv.cardId = localPlayer.handIds[cv.handIndex];
            cv.level = localPlayer.handLvls[cv.handIndex];
            if (verbose) Debug.Log("[Raycaster] Repaired CardView cardId/level from PlayerState hand arrays.");
        }

        if (cv.handIndex < 0)
        {
            Warn("CardView.handIndex is -1. Fix HandVisualizer to Init(...) with correct index.");
            ClearSelection();
            return;
        }

        // OFF-TURN: try UPGRADE (server enforces and uses gold)
        if (!clientThinksMyTurn && preferUpgradeWhenClientThinksOffTurn)
        {
            localPlayer.CmdUpgradeCard(cv.handIndex);
            if (verbose) Debug.Log("[Raycaster] Off-turn: requested upgrade.");
            ClearSelection();
            return;
        }

        // MY TURN
        var def = ResolveDef(cv.cardId);
        if (def == null)
        {
            // Do NOT auto-cast; we cannot infer play style.
            // Keep selection so user can click a target; server will enforce target requirement.
            if (verbose) Debug.LogWarning("[Raycaster] No definition on clicked card; selecting and waiting for target.");
            SelectCardInHand(cv);
            return;
        }

        var style = InferPlayStyle(def);

        switch (style)
        {
            case CardDefinition.PlayStyle.Instant:
                localPlayer.CmdPlayInstant(cv.handIndex, 0);
                if (verbose) Debug.Log("[Raycaster] Requested cast: Instant.");
                ClearSelection();
                break;

            case CardDefinition.PlayStyle.InstantWithTarget:
                SelectCardInHand(cv);
                if (verbose)
                {
                    if (def.playStyle != style)
                        Debug.Log("[Raycaster] Auto-corrected PlayStyle " + def.playStyle + " -> " + style + " for '" + def.cardName + "'.");
                    Debug.Log("[Raycaster] Selected InstantWithTarget. Click an enemy to cast.");
                }
                break;

            case CardDefinition.PlayStyle.SetReaction:
                localPlayer.CmdSetCard(cv.handIndex);
                if (verbose) Debug.Log("[Raycaster] Requested set: SetReaction.");
                ClearSelection();
                break;
        }
    }


    void SelectCardInHand(CardView cv)
    {
        if (cv == null) return;
        if (cv.owner != localPlayer) { Warn("Select ignored: not your card."); return; }
        if (!cv.isInHand) { Warn("Select ignored: not in hand."); return; }

        ClearSelection();
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

    void TrySetSelected()
    {
        if (localPlayer == null) return;
        if (selectedHandIndex < 0) { Warn("Select a HAND card to set."); return; }
        localPlayer.CmdSetCard(selectedHandIndex);
        if (verbose) Debug.Log("[Raycaster] Requested set via selection.");
        ClearSelection();
    }

    void TryCastSelectedOn(PlayerState target)
    {
        if (localPlayer == null) return;
        if (selectedHandIndex < 0) return;

        // Try to get definition (may be null on clone before DB resolves)
        CardDefinition def = null;
        if (selectedHandIndex < localPlayer.handIds.Count)
            def = ResolveDef(localPlayer.handIds[selectedHandIndex]);

        // If def known and not a targeted card, ignore.
        if (def != null)
        {
            var style = InferPlayStyle(def);
            if (style != CardDefinition.PlayStyle.InstantWithTarget)
            {
                ClearSelection();
                return;
            }
        }

        // Send with target anyway; server will enforce and deny if not a targeted card.
        uint netId = target ? target.netId : 0;
        localPlayer.CmdPlayInstant(selectedHandIndex, netId);
        if (verbose) Debug.Log("[Raycaster] Requested cast on target.");
        ClearSelection();
    }

    void LogHitInfo(RaycastHit hit)
    {
        var tr = hit.transform;
        var sb = new StringBuilder();
        sb.AppendLine($"Object: {tr.name} (layer={LayerMask.LayerToName(tr.gameObject.layer)}/{tr.gameObject.layer})");
        sb.AppendLine($"Path: {GetHierarchyPath(tr)}");
        var comps = tr.GetComponents<Component>();
        sb.Append("Comps: ");
        for (int i = 0; i < comps.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(comps[i].GetType().Name);
        }
        lastHitInfo = sb.ToString();
        if (verbose) Debug.Log("[Raycaster] " + lastHitInfo);
    }

    string GetHierarchyPath(Transform t)
    {
        var sb = new StringBuilder();
        while (t != null)
        {
            sb.Insert(0, "/" + t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    string DescribeSelectedForLog()
    {
        if (localPlayer == null || selectedHandIndex < 0 || selectedHandIndex >= localPlayer.handIds.Count)
            return "none";

        int id = localPlayer.handIds[selectedHandIndex];
        int lvl = localPlayer.handLvls[selectedHandIndex];
        var def = ResolveDef(id);

        if (def == null) return $"id={id}, L{lvl} (no def)";
        int chip = def.GetCastChipCost(lvl);
        bool myTurn = tm && tm.IsPlayersTurn(localPlayer);
        return $"{def.cardName} (id {id}) L{lvl} [{def.playStyle}] effect={def.effect}, chipCost={chip}, myTurn(client)={myTurn}, chips={localPlayer.chips}/{localPlayer.maxChips}, gold={localPlayer.gold}, setCount={localPlayer.setIds.Count}";
    }

    CardDefinition.PlayStyle InferPlayStyle(CardDefinition def)
    {
        switch (def.effect)
        {
            case CardDefinition.EffectType.LovePotion_HealXSelf:
            case CardDefinition.EffectType.Bomb_AllPlayersTakeX:
            case CardDefinition.EffectType.PhoenixFeather_HealX_ReviveTo2IfDead:
            case CardDefinition.EffectType.BlackHole_DiscardHandsRedrawSame:
            case CardDefinition.EffectType.Shield_GainXArmor:
                return CardDefinition.PlayStyle.Instant;

            case CardDefinition.EffectType.Knife_DealX:
            case CardDefinition.EffectType.KnifePotion_DealX_HealXSelf:
            case CardDefinition.EffectType.C4_ExplodeOnTargetAfter3Turns:
            case CardDefinition.EffectType.GoblinHands_MoveOneSetItemToCaster:
            case CardDefinition.EffectType.Pickpocket_StealOneRandomHandCard:
            case CardDefinition.EffectType.Turtle_TargetSkipsNextTurn:
                return CardDefinition.PlayStyle.InstantWithTarget;

            case CardDefinition.EffectType.Cactus_ReflectUpToX_For3Turns:
            case CardDefinition.EffectType.BearTrap_FirstAttackerTakesX:
            case CardDefinition.EffectType.MirrorShield_ReflectFirstAttackFull:
                return CardDefinition.PlayStyle.SetReaction;
        }
        return def.playStyle;
    }

    void Warn(string msg)
    {
        lastFailureReason = msg;
        lastFailureTime = Time.time;
        Debug.LogWarning("[Raycaster] " + msg);
    }
}
