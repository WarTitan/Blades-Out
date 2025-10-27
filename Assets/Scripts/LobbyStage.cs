// FILE: LobbyStage.cs
using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Lobby Stage")]
public class LobbyStage : NetworkBehaviour
{
    public static LobbyStage Instance;

    [Header("Lobby Camera")]
    public Camera lobbyCamera;

    [Header("Start Conditions")]
    [Tooltip("If true, every player must press 3 to be ready.")]
    public bool requireAllReady = false;

    [Tooltip("When RequireAllReady is false, start when at least this many players are ready.")]
    public int minPlayersToStart = 1; // start when at least one player is ready

    [SyncVar(hook = nameof(OnLobbyActiveChanged))]
    public bool lobbyActive = true;

    // Other scripts subscribe to this (true = lobby, false = gameplay)
    public static System.Action<bool> OnLobbyStateChanged;

    void Awake()
    {
        Instance = this;
        ApplyLobbyCameraState();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        OnLobbyStateChanged?.Invoke(lobbyActive);
        ApplyLobbyCameraState();
    }

    void OnLobbyActiveChanged(bool oldValue, bool newValue)
    {
        ApplyLobbyCameraState();
        OnLobbyStateChanged?.Invoke(newValue);
    }

    void ApplyLobbyCameraState()
    {
        if (lobbyCamera != null)
            lobbyCamera.enabled = lobbyActive;
    }

    // Called by LobbyReady on the server when a player toggles ready
    [Server]
    public void Server_NotifyReadyChanged()
    {
        EvaluateAndMaybeStart();
    }

    [Server]
    void EvaluateAndMaybeStart()
    {
        int total = 0;
        int ready = 0;

        var readies = FindObjectsOfType<LobbyReady>();
        for (int i = 0; i < readies.Length; i++)
        {
            var lr = readies[i];
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
            mgr.Server_TeleportAllPlayersToGameSpawns();
        }
        else
        {
            Debug.LogWarning("[LobbyStage] PlayerSpawnManager not found.");
        }
    }

    // Client helper so local scripts can turn off lobby cam ASAP
    [Client]
    public void Client_DisableLobbyCameraLocal()
    {
        if (lobbyCamera != null)
            lobbyCamera.enabled = false;
    }
}
