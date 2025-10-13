using UnityEngine;
using Mirror;

public class KeyboardHotkeys : MonoBehaviour
{
    [SerializeField] private int upgradeHandIndex = 0;
    private PlayerState localPlayer;

    void Update()
    {
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

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            Debug.Log("[Hotkeys] 1 pressed -> End Turn requested");
            localPlayer.CmdEndTurn();
        }

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

        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            Debug.Log("[Hotkeys] 3 pressed -> Start Game requested");
            localPlayer.CmdRequestStartGame();
        }
    }
}
