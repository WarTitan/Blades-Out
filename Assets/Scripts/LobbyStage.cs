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

    [Header("Start Conditions (legacy)")]
    public int minPlayersToStart = 2;
    public bool autoStartWhenMinReady = true;

    // Single-bool event: true when lobby is active.
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
    }

    void OnLobbyActiveChanged(bool oldVal, bool newVal)
    {
        ApplyCameraState();
        OnLobbyStateChanged?.Invoke(newVal);
    }

    // Apply only when state actually changes. Do NOT toggle every frame.
    void ApplyCameraState()
    {
        if (lobbyCamera != null)
            lobbyCamera.enabled = lobbyActive;
    }

    // NEW: Teleport any player who toggled ready ON. Lobby stays active for others.
    [Server]
    public void Server_NotifyReadyChanged()
    {
        if (!lobbyActive) return;

        var mgr = NetworkManager.singleton as PlayerSpawnManager;

        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            var go = conn.identity.gameObject;
            var lr = go.GetComponent<LobbyReady>();
            if (lr == null || !lr.isReady) continue;

            var ps = go.GetComponent<PlayerState>();
            if (ps == null) continue;

            // Pick a game/table spawn for this seat; fallback to current pos if none
            int seat = ps.seatIndex >= 0 ? ps.seatIndex : 0;

            Vector3 dstPos = go.transform.position;
            Quaternion dstRot = go.transform.rotation;

            if (mgr != null && mgr.spawnPoints != null && mgr.spawnPoints.Count > 0)
            {
                var sp = mgr.spawnPoints[seat % mgr.spawnPoints.Count];
                if (sp != null)
                {
                    dstPos = sp.position;
                    dstRot = sp.rotation;
                }
            }

            // Server authoritative snap (handles CharacterController safely)
            var cc = go.GetComponent<CharacterController>();
            bool hadCC = (cc != null && cc.enabled);
            if (hadCC) cc.enabled = false;
            go.transform.SetPositionAndRotation(dstPos, dstRot);
            if (hadCC) cc.enabled = true;

            // Owner local snap + force gameplay camera/controls
            var tp = go.GetComponent<TeleportHelper>();
            if (tp != null && ps.connectionToClient != null)
                tp.TargetSnapAndEnterGameplay(ps.connectionToClient, dstPos, dstRot);

            // Clear ready so it doesn't retrigger next tick
            lr.isReady = false;
        }

        // State didn't change; notify listeners anyway
        OnLobbyStateChanged?.Invoke(lobbyActive);
    }

    // Optional helper for any client to hard-disable the lobby camera locally if needed
    [Client]
    public void Client_DisableLobbyCameraLocal()
    {
        if (lobbyCamera != null)
            lobbyCamera.enabled = false;
    }
}
