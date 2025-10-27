// FILE: LobbyReady.cs
using UnityEngine;
using Mirror;

public class LobbyReady : NetworkBehaviour
{
    [SyncVar] public bool isReady;

    void Update()
    {
        if (!isLocalPlayer) return;
        if (LobbyStage.Instance == null) return;
        if (!LobbyStage.Instance.lobbyActive) return;

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            CmdToggleReady();
        }
    }

    [Command]
    void CmdToggleReady()
    {
        isReady = !isReady;

        // Tell the stage to re-evaluate readiness. The stage will decide if/when to start for everyone.
        var stage = LobbyStage.Instance;
        if (stage != null)
        {
            stage.Server_NotifyReadyChanged();
        }
    }
}
