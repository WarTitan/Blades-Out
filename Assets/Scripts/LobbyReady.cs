using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Lobby Ready")]
public class LobbyReady : NetworkBehaviour
{
    [SyncVar] public bool isReady;
    public KeyCode readyKey = KeyCode.Alpha3; // press 3 to toggle ready

    void Update()
    {
        if (!isLocalPlayer) return;
        if (LobbyStage.Instance == null || !LobbyStage.Instance.lobbyActive) return;

        if (Input.GetKeyDown(readyKey))
        {
            CmdSetReady(!isReady);
        }
    }

    [Command(requiresAuthority = true)]
    public void CmdSetReady(bool value)
    {
        isReady = value;

        // Keep your original notification (don’t remove)
        if (LobbyStage.Instance) LobbyStage.Instance.Server_NotifyReadyChanged();

        // NEW: also teleport this one player immediately (even if lobby stays open)
        var spm = NetworkManager.singleton as PlayerSpawnManager;
        if (value && spm != null)
        {
            spm.Server_TeleportOne(connectionToClient);
        }
    }
}
