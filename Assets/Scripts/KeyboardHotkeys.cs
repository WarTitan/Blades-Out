using UnityEngine;
using Mirror;

public class KeyboardHotkeys : MonoBehaviour
{
    [Header("Upgrade")]
    [SerializeField] private int upgradeHandIndex = 0;

    [Header("Set Card (move from hand -> set row)")]
    [SerializeField] private KeyCode setCardKey = KeyCode.V;   // press V to set first card for testing
    [SerializeField] private int setHandIndex = 0;             // which hand index to set (0 = first card)

    private PlayerState localPlayer;

    void Update()
    {
        // cache local player once
        if (localPlayer == null)
        {
#if UNITY_2023_1_OR_NEWER
            var arr = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
            var arr = Object.FindObjectsOfType<PlayerState>();
#endif
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].isLocalPlayer)
                {
                    localPlayer = arr[i];
                    Debug.Log("[Hotkeys] Local Player cached: " + localPlayer.netId);
                    break;
                }
            }
        }

        if (!Application.isFocused) return;
        if (localPlayer == null) return;

        // 1 = End Turn
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            Debug.Log("[Hotkeys] 1 pressed -> End Turn requested");
            localPlayer.CmdEndTurn();
        }

        // 2 = Upgrade card at upgradeHandIndex
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            Debug.Log("[Hotkeys] 2 pressed -> Upgrade card index " + upgradeHandIndex);
            if (localPlayer.handIds.Count > upgradeHandIndex)
            {
                localPlayer.CmdUpgradeCard(upgradeHandIndex);
            }
            else
            {
                Debug.LogWarning("[Hotkeys] No card at index " + upgradeHandIndex + " to upgrade.");
            }
        }

        // 3 = Start Game
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            Debug.Log("[Hotkeys] 3 pressed -> Start Game requested");
            localPlayer.CmdRequestStartGame();
        }

        // V (default) = Set card (hand -> set row) for testing
        if (Input.GetKeyDown(setCardKey))
        {
            if (localPlayer.handIds.Count > setHandIndex)
            {
                Debug.Log($"[Hotkeys] {setCardKey} pressed -> Set hand card at index {setHandIndex}");
                localPlayer.CmdSetCard(setHandIndex);
            }
            else
            {
                Debug.LogWarning($"[Hotkeys] Cannot set card: no hand card at index {setHandIndex} (hand size = {localPlayer.handIds.Count}).");
            }
        }
    }
}
