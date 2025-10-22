using UnityEngine;
using Mirror;

// Keyboard hotkeys for the local player.
// Lobby: 3 toggles READY (handled via LobbyReady on this player).
// Gameplay: 1 End Turn, 2 Upgrade (off-turn), 4 Set selected card, Q/Esc Deselect.
// NOTE: We intentionally do NOT call CmdRequestStartGame here.
//       The match starts automatically when LobbyStage reaches the min ready players.
public class PlayerHotkeys : NetworkBehaviour
{
    [Header("Refs (auto)")]
    public PlayerState localPlayer;
    public CardRaycasterOnRoot raycaster;
    public LobbyReady lobbyReady;

    // Primary number-row keys (gameplay only)
    [Header("Key Bindings (number row)")]
    public KeyCode endTurnKey = KeyCode.Alpha1;
    public KeyCode upgradeKey = KeyCode.Alpha2;
    // 3 is RESERVED for LobbyReady (do not bind here)
    public KeyCode setKey = KeyCode.Alpha4;

    // Numpad alternates (gameplay only)
    [Header("Key Bindings (numpad)")]
    public KeyCode endTurnKeypad = KeyCode.Keypad1;
    public KeyCode upgradeKeypad = KeyCode.Keypad2;
    // Keypad3 reserved for LobbyReady
    public KeyCode setKeypad = KeyCode.Keypad4;

    [Header("Other")]
    public KeyCode deselectKey = KeyCode.Q;

    void Awake()
    {
        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
        if (!lobbyReady) lobbyReady = GetComponent<LobbyReady>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
        if (!lobbyReady) lobbyReady = GetComponent<LobbyReady>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
        if (!lobbyReady) lobbyReady = GetComponent<LobbyReady>();
        if (!localPlayer) return;

        bool inLobby = LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : false;

        // ---------------- LOBBY HOTKEYS ----------------
        if (inLobby)
        {
            // 3 -> Toggle READY (owned by LobbyReady)
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                if (lobbyReady)
                {
                    bool next = !lobbyReady.isReady;
                    lobbyReady.CmdSetReady(next);
                    Debug.Log("[Hotkeys] Lobby Ready toggled -> " + (next ? "ON" : "OFF"));
                }
                else
                {
                    Debug.LogWarning("[Hotkeys] LobbyReady component missing on player.");
                }
            }

            // Allow deselect to clear any UI/card selection even in lobby
            if (Input.GetKeyDown(deselectKey) || Input.GetKeyDown(KeyCode.Escape))
                raycaster?.DeselectPublic();

            // Block gameplay hotkeys while in lobby
            return;
        }

        // ---------------- GAMEPLAY HOTKEYS ----------------

        // 1 -> End Turn (only on your turn)
        if (Pressed(endTurnKey, endTurnKeypad))
        {
            var tm = TurnManager.Instance;
            if (tm && !tm.IsPlayersTurn(localPlayer))
            {
                Debug.LogWarning("[Hotkeys] Not your turn.");
            }
            else
            {
                Debug.Log("[Hotkeys] End Turn.");
                localPlayer.CmdEndTurn();
            }
        }

        // 4 -> Set selected hand card (SetReaction only, YOUR turn, max 1 set)
        if (Pressed(setKey, setKeypad))
        {
            int handIndex = raycaster != null ? raycaster.SelectedHandIndex : -1;
            if (handIndex < 0)
            {
                Debug.Log("[Hotkeys] No HAND card selected. Click a card in your hand first.");
            }
            else
            {
                var tm = TurnManager.Instance;
                if (tm && !tm.IsPlayersTurn(localPlayer))
                {
                    Debug.LogWarning("[Hotkeys] Set is only during YOUR turn.");
                }
                else if (localPlayer.setIds.Count > 0)
                {
                    Debug.LogWarning("[Hotkeys] You already have a set card.");
                }
                else
                {
                    // Raycaster validates playstyle == SetReaction and logs reasons if it fails.
                    raycaster.PublicTrySetSelected();
                }
            }
        }

        // 2 -> Upgrade selected hand card (OFF-turn only)
        if (Pressed(upgradeKey, upgradeKeypad))
        {
            var tm = TurnManager.Instance;
            if (tm && tm.IsPlayersTurn(localPlayer))
            {
                Debug.LogWarning("[Hotkeys] Upgrade is only OFF-turn.");
            }
            else
            {
                int hi = raycaster != null ? raycaster.SelectedHandIndex : -1;
                if (hi < 0) Debug.Log("[Hotkeys] Select a HAND card to upgrade.");
                else localPlayer.CmdUpgradeCard(hi);
            }
        }

        // Q or Esc -> Deselect
        if (Input.GetKeyDown(deselectKey) || Input.GetKeyDown(KeyCode.Escape))
        {
            raycaster?.DeselectPublic();
        }
    }

    bool Pressed(KeyCode main, KeyCode alt)
        => Input.GetKeyDown(main) || Input.GetKeyDown(alt);
}
