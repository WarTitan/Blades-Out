using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Lobby Stage")]
public class LobbyStage : NetworkBehaviour
{
    public static LobbyStage Instance;

    [Header("Lobby Camera")]
    public Camera lobbyCamera;

    [SyncVar(hook = nameof(OnLobbyActiveChanged))]
    public bool lobbyActive = true;

    [Header("Start Conditions")]
    public int minPlayersToStart = 2;
    public bool autoStartWhenMinReady = true;

    public static System.Action<bool> OnLobbyStateChanged;

    void Awake()
    {
        Instance = this;
        ApplyCameraState();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnLobbyActiveChanged(bool oldVal, bool newVal)
    {
        ApplyCameraState();
        OnLobbyStateChanged?.Invoke(newVal);
    }

    void ApplyCameraState()
    {
        if (lobbyCamera != null) lobbyCamera.enabled = lobbyActive;
    }

    void Update()
    {
        // While in lobby: keep local client in lobby state (no input/crosshair)
        if (!isClient || !lobbyActive) return;

        if (NetworkClient.localPlayer != null)
        {
            var go = NetworkClient.localPlayer.gameObject;

            var lca = go.GetComponent<LocalCameraActivator>();
            if (lca) lca.ForceEnterLobby();

            var lld = go.GetComponent<LobbyLocalDisabler>();
            if (lld) lld.ForceLobby();

            if (lobbyCamera) lobbyCamera.enabled = true;
        }
    }

    [Server]
    public void Server_ExitLobbyAndTeleport()
    {
        if (!lobbyActive) return;

        var mgr = NetworkManager.singleton as PlayerSpawnManager;
        if (mgr != null) mgr.Server_TeleportAllPlayersToGameSpawns();

        lobbyActive = false; // syncvar flips clients
        RpcLobbyEnded();
    }

    [ClientRpc]
    void RpcLobbyEnded()
    {
        lobbyActive = false;
        ApplyCameraState();
    }

    [Server]
    public void Server_NotifyReadyChanged()
    {
        if (!lobbyActive) return;

        int players = 0, ready = 0;
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;
            players++;

            var lr = conn.identity.GetComponent<LobbyReady>();
            if (lr != null && lr.isReady) ready++;
        }

        if (autoStartWhenMinReady && players >= minPlayersToStart && ready >= minPlayersToStart)
        {
            Server_ExitLobbyAndTeleport();

            var tm = TurnManager.Instance;
            if (tm != null)
                tm.Server_AttemptStartGame();
        }
    }
}
