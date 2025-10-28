// FILE: LobbyStage.cs
// Starts match when the readiness threshold is met and teleports EVERYONE (not just the host).
// Keeps lobby camera enabled only while the lobby is active.

using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Lobby Stage")]
public class LobbyStage : NetworkBehaviour
{
    public static LobbyStage Instance;

    [Header("Lobby Camera")]
    public Camera lobbyCamera;

    [Header("Start Conditions")]
    [Tooltip("If true, all connected players must be ready to start.")]
    public bool requireAllReady = false;

    [Tooltip("If RequireAllReady is false, start when at least this many players are ready.")]
    public int minPlayersToStart = 1;

    [SyncVar(hook = nameof(OnLobbyActiveChanged))]
    public bool lobbyActive = true;

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

    public override void OnStartClient()
    {
        base.OnStartClient();
        OnLobbyStateChanged?.Invoke(lobbyActive);
        ApplyCameraState();
    }

    void OnLobbyActiveChanged(bool oldValue, bool newValue)
    {
        ApplyCameraState();
        OnLobbyStateChanged?.Invoke(newValue);
    }

    void ApplyCameraState()
    {
        if (lobbyCamera != null)
            lobbyCamera.enabled = lobbyActive;
    }

    [Server]
    public void Server_NotifyReadyChanged()
    {
        if (!lobbyActive) return;

        int total = 0;
        int ready = 0;

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<LobbyReady>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<LobbyReady>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var lr = all[i];
            if (lr == null) continue;
            var id = lr.GetComponent<NetworkIdentity>();
            if (id == null || !id.isServer) continue;
            total++;
            if (lr.isReady) ready++;
        }

        if (requireAllReady)
        {
            if (total > 0 && ready == total)
                Server_StartMatch();
        }
        else
        {
            int needed = Mathf.Max(1, minPlayersToStart);
            if (ready >= needed)
                Server_StartMatch();
        }
    }

    [Server]
    public void Server_StartMatch()
    {
        if (!lobbyActive) return;

        lobbyActive = false; // disables lobby cam via hook and notifies listeners

        var mgr = NetworkManager.singleton as PlayerSpawnManager;
        if (mgr != null)
        {
            // TELEPORT EVERYONE, not just the host
            mgr.Server_TeleportAllPlayersToGameSpawns();
        }
        else
        {
            Debug.LogWarning("[LobbyStage] PlayerSpawnManager not found.");
        }
    }

    [Client]
    public void Client_DisableLobbyCameraLocal()
    {
        if (lobbyCamera != null)
            lobbyCamera.enabled = false;
    }
}
