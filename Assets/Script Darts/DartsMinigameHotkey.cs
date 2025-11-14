// FILE: DartsMinigameHotkey.cs
// Press 6 to start darts test; press 3 to return to table.
// Uses PlayerSpawnManager server methods.

using UnityEngine;
using Mirror;

[AddComponentMenu("Minigames/Darts Minigame Hotkey")]
public class DartsMinigameHotkey : NetworkBehaviour
{
    [Tooltip("Only allow triggering while lobby is active.")]
    public bool onlyInLobby = true;

    void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            if (!onlyInLobby || IsLobbyActive())
                Cmd_StartDartsTest();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Cmd_EndDartsTestAndReturn();
        }
    }

    private bool IsLobbyActive()
    {
        var ls = LobbyStage.Instance;
        if (ls == null) return true;
        return ls.lobbyActive;
    }

    [Command]
    private void Cmd_StartDartsTest()
    {
        var pm = NetworkManager.singleton as PlayerSpawnManager;
        if (pm != null) pm.Server_StartDartsTest();
    }

    [Command]
    private void Cmd_EndDartsTestAndReturn()
    {
        var pm = NetworkManager.singleton as PlayerSpawnManager;
        if (pm != null) pm.Server_EndDartsTestAndReturnToTable();
    }
}
