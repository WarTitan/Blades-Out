// FILE: PlayerSpawnManager.cs
// NetworkManager subclass that handles seats and teleports.
// Includes Server_TeleportOne(...) and Server_TeleportAllPlayersToGameSpawns().

using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSpawnManager : NetworkManager
{
    [Header("Lobby Spawns (optional)")]
    public List<Transform> lobbySpawnPoints = new List<Transform>();

    [Header("Game/Table Spawns")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Match Size")]
    [SerializeField] private int maxPlayers = 5;

    // Track used seats
    private readonly HashSet<int> usedSeats = new HashSet<int>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        usedSeats.Clear();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // Create the player object
        GameObject player = Instantiate(playerPrefab);

        // Seat assignment without collisions
        int seat = PickFreeSeat();
        var ps = player.GetComponent<PlayerState>();
        if (ps != null) ps.seatIndex = seat;

        // Lobby spawn position (optional)
        if (lobbySpawnPoints != null && lobbySpawnPoints.Count > 0)
        {
            var sp = lobbySpawnPoints[seat % lobbySpawnPoints.Count];
            if (sp != null)
                player.transform.SetPositionAndRotation(sp.position, sp.rotation);
        }

        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // Free their seat
        if (conn != null && conn.identity != null)
        {
            var ps = conn.identity.GetComponent<PlayerState>();
            if (ps != null && ps.seatIndex >= 0)
                usedSeats.Remove(ps.seatIndex);
        }

        base.OnServerDisconnect(conn);
    }

    private int PickFreeSeat()
    {
        for (int i = 0; i < maxPlayers; i++)
        {
            if (!usedSeats.Contains(i))
            {
                usedSeats.Add(i);
                return i;
            }
        }
        // fallback if all taken
        return 0;
    }

    // ---------- Teleport exactly one player to their table spawn ----------
    [Server]
    public void Server_TeleportOne(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null)
        {
            Debug.LogWarning("[PlayerSpawnManager] Server_TeleportOne: invalid connection/identity");
            return;
        }

        var go = conn.identity.gameObject;
        var ps = go.GetComponent<PlayerState>();
        int seat = (ps != null && ps.seatIndex >= 0) ? ps.seatIndex : 0;

        Vector3 dstPos = go.transform.position;
        Quaternion dstRot = go.transform.rotation;

        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            var sp = spawnPoints[seat % spawnPoints.Count];
            if (sp != null)
            {
                dstPos = sp.position;
                dstRot = sp.rotation;
            }
            else
            {
                Debug.LogWarning("[PlayerSpawnManager] Server_TeleportOne: spawnPoints slot was null, using current transform.");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerSpawnManager] Server_TeleportOne: no game/table spawns assigned, using current transform.");
        }

        // Authoritative snap (handle CharacterController safely)
        var cc = go.GetComponent<CharacterController>();
        bool hadCC = (cc != null && cc.enabled);
        if (hadCC) cc.enabled = false;
        go.transform.SetPositionAndRotation(dstPos, dstRot);
        if (hadCC) cc.enabled = true;

        // Tell owner client to enter gameplay view and enforce camera/look
        var tp = go.GetComponent<TeleportHelper>();
        if (tp != null && ps != null && ps.connectionToClient != null)
        {
            tp.TargetSnapAndEnterGameplay(ps.connectionToClient, dstPos, dstRot);
        }
        else
        {
            Debug.LogWarning("[PlayerSpawnManager] Server_TeleportOne: missing TeleportHelper or connectionToClient.");
        }
    }

    // ---------- Teleport ALL players to their game/table spawns ----------
    [Server]
    public void Server_TeleportAllPlayersToGameSpawns()
    {
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            var go = conn.identity.gameObject;
            var ps = go.GetComponent<PlayerState>();
            int seat = (ps != null && ps.seatIndex >= 0) ? ps.seatIndex : 0;

            Vector3 dstPos = go.transform.position;
            Quaternion dstRot = go.transform.rotation;

            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                var sp = spawnPoints[seat % spawnPoints.Count];
                if (sp != null)
                {
                    dstPos = sp.position;
                    dstRot = sp.rotation;
                }
            }

            // Server snap
            var cc = go.GetComponent<CharacterController>();
            bool hadCC = (cc != null && cc.enabled);
            if (hadCC) cc.enabled = false;
            go.transform.SetPositionAndRotation(dstPos, dstRot);
            if (hadCC) cc.enabled = true;

            // Owner local snap + enter gameplay
            var tp = go.GetComponent<TeleportHelper>();
            if (tp != null && ps != null && ps.connectionToClient != null)
            {
                tp.TargetSnapAndEnterGameplay(ps.connectionToClient, dstPos, dstRot);
            }
        }
    }
}
