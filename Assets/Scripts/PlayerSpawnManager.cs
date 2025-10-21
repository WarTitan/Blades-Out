using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSpawnManager : NetworkManager
{
    [Header("Lobby Spawns (up to 5)")]
    public List<Transform> lobbySpawnPoints = new List<Transform>();

    [Header("Game/Table Spawns (up to 5)")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Match Size")]
    [SerializeField] private int maxPlayers = 5;

    // ---------- Seat assignment (no collisions) ----------
    int GetNextFreeSeat()
    {
        bool[] used = new bool[Mathf.Max(1, maxPlayers)];
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<PlayerState>();
#endif
        foreach (var ps in all)
        {
            if (!ps) continue;
            if (ps.seatIndex >= 0 && ps.seatIndex < used.Length) used[ps.seatIndex] = true;
        }
        for (int i = 0; i < used.Length; i++)
            if (!used[i]) return i;
        return 0; // fallback
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        int seat = GetNextFreeSeat();

        Transform spawn = null;
        if (lobbySpawnPoints != null && lobbySpawnPoints.Count > 0)
            spawn = lobbySpawnPoints[seat % lobbySpawnPoints.Count];

        GameObject player = spawn != null
            ? Instantiate(playerPrefab, spawn.position, spawn.rotation)
            : Instantiate(playerPrefab);

        var ps = player.GetComponent<PlayerState>();
        if (ps != null) ps.seatIndex = seat;

        NetworkServer.AddPlayerForConnection(conn, player);

        Debug.Log($"[PlayerSpawnManager] AddPlayer connId={conn.connectionId} seat={seat} at {(spawn ? spawn.name : "NO_LOBBY_SPAWN")}");
    }

    [Server]
    public void Server_TeleportAllPlayersToGameSpawns()
    {
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            var go = conn.identity.gameObject;
            var ps = go.GetComponent<PlayerState>();
            if (ps == null)
            {
                Debug.LogWarning($"[PlayerSpawnManager] No PlayerState for connId={conn.connectionId}");
                continue;
            }

            int seat = Mathf.Clamp(ps.seatIndex, 0, Mathf.Max(0, spawnPoints.Count - 1));
            Transform sp = (spawnPoints != null && seat < spawnPoints.Count) ? spawnPoints[seat] : null;
            if (sp == null)
            {
                Debug.LogError($"[PlayerSpawnManager] MISSING game spawn for seat {seat}. Fill spawnPoints[{seat}] in inspector.");
                continue;
            }

            // Server authoritative move
            go.transform.SetPositionAndRotation(sp.position, sp.rotation);

            // Owner local snap + enter gameplay
            var tp = go.GetComponent<TeleportHelper>();
            if (tp != null && ps.connectionToClient != null)
            {
                tp.TargetSnapAndEnterGameplay(ps.connectionToClient, sp.position, sp.rotation);
                Debug.Log($"[PlayerSpawnManager] Teleported connId={conn.connectionId} seat={seat} -> {sp.name}");
            }
            else
            {
                Debug.LogWarning($"[PlayerSpawnManager] TeleportHelper or connection missing for connId={conn?.connectionId} seat={seat}");
            }
        }
    }
}
