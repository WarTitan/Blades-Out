using UnityEngine;
using Mirror;

// ONE place for all keyboard hotkeys for a player.
// - 3 : Request Start Game
// - E : End Turn
// - V : Set the currently selected hand card (must be SetReaction)
// - 2 : Upgrade the currently selected hand card (server checks gold & max level)
// - Q : Deselect selected card (optional; right-click also deselects via Raycaster)
//
// Attach this to the same GameObject that has PlayerState. It will auto-find the Raycaster.
public class PlayerHotkeys : NetworkBehaviour
{
    [Header("Refs (auto)")]
    public PlayerState localPlayer;
    public CardRaycasterOnRoot raycaster;

    [Header("Keys")]
    public KeyCode startGameKey = KeyCode.Alpha3;
    public KeyCode endTurnKey = KeyCode.E;
    public KeyCode setKey = KeyCode.V;
    public KeyCode upgradeKey = KeyCode.Alpha2;
    public KeyCode deselectKey = KeyCode.Q;

    [Header("Rules")]
    public bool requireTurnToSet = true;
    public bool requireTurnToUpgrade = true;

    void Awake()
    {
        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (!localPlayer) return;

        // 3 -> start game
        if (Input.GetKeyDown(startGameKey))
        {
            Debug.Log("[Hotkeys] Start Game");
            localPlayer.CmdRequestStartGame();
        }

        // E -> end turn
        if (Input.GetKeyDown(endTurnKey))
        {
            Debug.Log("[Hotkeys] End Turn");
            localPlayer.CmdEndTurn();
        }

        // V -> Set selected hand card (must be SetReaction)
        if (Input.GetKeyDown(setKey))
        {
            int handIndex = GetSelectedHandIndex();
            if (handIndex < 0) { Debug.Log("[Hotkeys] No selected hand card to Set."); }
            else
            {
                if (requireTurnToSet)
                {
                    var tm = TurnManager.Instance;
                    if (tm == null) { Debug.LogWarning("[Hotkeys] TurnManager not ready."); return; }
                    if (!tm.IsPlayersTurn(localPlayer)) { Debug.LogWarning("[Hotkeys] Not your turn."); return; }
                }

                // Style check (only SetReaction may be set)
                if (handIndex >= localPlayer.handIds.Count) { Debug.LogWarning("[Hotkeys] Hand index out of range."); return; }
                var def = localPlayer.database ? localPlayer.database.Get(localPlayer.handIds[handIndex]) : null;
                if (def == null || def.playStyle != CardDefinition.PlayStyle.SetReaction)
                {
                    Debug.LogWarning("[Hotkeys] Selected card is not SetReaction; cannot Set.");
                    return;
                }

                Debug.Log("[Hotkeys] CmdSetCard(" + handIndex + ")");
                localPlayer.CmdSetCard(handIndex);
                Deselect();
            }
        }

        // 2 -> Upgrade selected hand card
        if (Input.GetKeyDown(upgradeKey))
        {
            int handIndex = GetSelectedHandIndex();
            if (handIndex < 0) { Debug.Log("[Hotkeys] No selected hand card to Upgrade."); }
            else
            {
                if (requireTurnToUpgrade)
                {
                    var tm = TurnManager.Instance;
                    if (tm == null) { Debug.LogWarning("[Hotkeys] TurnManager not ready."); return; }
                    if (!tm.IsPlayersTurn(localPlayer)) { Debug.LogWarning("[Hotkeys] Not your turn."); return; }
                }

                Debug.Log("[Hotkeys] CmdUpgradeCard(" + handIndex + ")");
                localPlayer.CmdUpgradeCard(handIndex);
                // keep selection after upgrade so player can spam upgrades if they have gold
            }
        }

        // Q -> Deselect (optional; right-click also works via Raycaster)
        if (Input.GetKeyDown(deselectKey)) Deselect();
    }

    int GetSelectedHandIndex()
    {
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
        if (!raycaster) return -1;
        return raycaster.SelectedHandIndex;
    }

    void Deselect()
    {
        if (!raycaster) return;
        raycaster.DeselectPublic();
    }
}
