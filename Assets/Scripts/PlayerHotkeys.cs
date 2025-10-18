using UnityEngine;
using Mirror;

// Keyboard hotkeys for the local player.
// 1  : End Turn
// 2  : Upgrade selected hand card (OFF-turn only; server checks gold & max)
// 3  : Request Start Game
// 4  : Set selected hand card (must be SetReaction, only during YOUR turn, max 1 set)
// Q/Esc : Deselect
//
// Attach to the same GameObject as PlayerState. It auto-finds CardRaycasterOnRoot in children.
public class PlayerHotkeys : NetworkBehaviour
{
    [Header("Refs (auto)")]
    public PlayerState localPlayer;
    public CardRaycasterOnRoot raycaster;

    // Primary number-row keys
    [Header("Key Bindings (number row)")]
    public KeyCode endTurnKey = KeyCode.Alpha1;
    public KeyCode upgradeKey = KeyCode.Alpha2;
    public KeyCode startGameKey = KeyCode.Alpha3;
    public KeyCode setKey = KeyCode.Alpha4;

    // Numpad alternates
    [Header("Key Bindings (numpad)")]
    public KeyCode endTurnKeypad = KeyCode.Keypad1;
    public KeyCode upgradeKeypad = KeyCode.Keypad2;
    public KeyCode startGameKeypad = KeyCode.Keypad3;
    public KeyCode setKeypad = KeyCode.Keypad4;

    [Header("Other")]
    public KeyCode deselectKey = KeyCode.Q;

    void Awake()
    {
        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (!localPlayer) localPlayer = GetComponent<PlayerState>();
        if (!raycaster) raycaster = GetComponentInChildren<CardRaycasterOnRoot>(true);
        if (!localPlayer || !raycaster) return;

        // 3 -> Start Game
        if (Pressed(startGameKey, startGameKeypad))
        {
            Debug.Log("[Hotkeys] Start Game requested.");
            localPlayer.CmdRequestStartGame();
        }

        // 1 -> End Turn
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
            int handIndex = raycaster.SelectedHandIndex;
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
                int hi = raycaster.SelectedHandIndex;
                if (hi < 0) Debug.Log("[Hotkeys] Select a HAND card to upgrade.");
                else localPlayer.CmdUpgradeCard(hi);
            }
        }

        // Q or Esc -> Deselect
        if (Input.GetKeyDown(deselectKey) || Input.GetKeyDown(KeyCode.Escape))
        {
            raycaster.DeselectPublic();
        }
    }

    bool Pressed(KeyCode main, KeyCode alt)
        => Input.GetKeyDown(main) || Input.GetKeyDown(alt);
}
