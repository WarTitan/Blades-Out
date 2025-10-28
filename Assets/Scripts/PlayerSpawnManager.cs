// FILE: PlayerSpawnManager.cs
// Deterministic, connection-based seat mapping that works with host + ParrelSync clone.
// Host always gets seat 0 (lobby1), next client seat 1 (lobby2), etc.
// Teleports ALL players and orients them to the spawn point's forward (Z) axis, yaw-only.

using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSpawnManager : NetworkManager
{
    [Header("Lobby Spawns (optional)")]
    public List<Transform> lobbySpawnPoints = new List<Transform>();

    [Header("Game/Table Spawns (ensure count >= expected players)")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Match Size")]
    [SerializeField] private int maxPlayers = 5;

    // Server-only: stable mapping from connectionId -> seat index
    private readonly Dictionary<int, int> connToSeat = new Dictionary<int, int>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        connToSeat.Clear();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        connToSeat.Clear();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GameObject player = Instantiate(playerPrefab);

        // Assign seat deterministically by join order (no scene scans, no guessing)
        int seat = AssignSeatForConnection(conn);

        // Write seat to player state (if present)
        var ps = player.GetComponent<PlayerState>();
        if (ps != null) ps.seatIndex = seat;

        // Place into matching lobby spawn (optional)
        if (lobbySpawnPoints != null && lobbySpawnPoints.Count > 0)
        {
            Transform lsp = lobbySpawnPoints[seat % lobbySpawnPoints.Count];
            if (lsp != null)
                player.transform.SetPositionAndRotation(lsp.position, YawOnly(lsp));
        }

        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn != null) connToSeat.Remove(conn.connectionId);
        base.OnServerDisconnect(conn);
    }

    // ---------- Teleport exactly one player to their seat ----------
    [Server]
    public void Server_TeleportOne(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null) return;

        GameObject go = conn.identity.gameObject;
        int seat = AssignSeatForConnection(conn); // ensures mapping exists

        Vector3 pos; Quaternion rot;
        GetSeatPose(seat, go.transform.position, go.transform.rotation, out pos, out rot);

        var cc = go.GetComponent<CharacterController>();
        bool hadCC = (cc != null && cc.enabled);
        if (hadCC) cc.enabled = false;

        go.transform.SetPositionAndRotation(pos, rot);

        if (hadCC) cc.enabled = true;

        var ps = go.GetComponent<PlayerState>();
        var tp = go.GetComponent<TeleportHelper>();
        if (tp != null && ps != null && ps.connectionToClient != null)
            tp.TargetSnapAndEnterGameplay(ps.connectionToClient, pos, rot);
    }

    // ---------- Teleport ALL players to their seats ----------
    [Server]
    public void Server_TeleportAllPlayersToGameSpawns()
    {
        // First make sure every connection has a seat
        foreach (var kv in NetworkServer.connections)
        {
            var c = kv.Value;
            if (c == null || c.identity == null) continue;

            int seat = AssignSeatForConnection(c);

            // Also reflect to PlayerState so UI/debug can see it
            var ps = c.identity.GetComponent<PlayerState>();
            if (ps != null) ps.seatIndex = seat;
        }

        // Then move each to the correct spawn and notify their owner
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            GameObject go = conn.identity.gameObject;
            int seat = connToSeat.TryGetValue(conn.connectionId, out int s) ? s : 0;

            Vector3 pos; Quaternion rot;
            GetSeatPose(seat, go.transform.position, go.transform.rotation, out pos, out rot);

            var cc = go.GetComponent<CharacterController>();
            bool hadCC = (cc != null && cc.enabled);
            if (hadCC) cc.enabled = false;

            go.transform.SetPositionAndRotation(pos, rot);

            if (hadCC) cc.enabled = true;

            var ps = go.GetComponent<PlayerState>();
            var tp = go.GetComponent<TeleportHelper>();
            if (tp != null && ps != null && ps.connectionToClient != null)
                tp.TargetSnapAndEnterGameplay(ps.connectionToClient, pos, rot);
        }
    }

    // ---------- Helpers ----------

    // Assigns a stable seat for this connection: 0 for first, 1 for second, etc.
    [Server]
    private int AssignSeatForConnection(NetworkConnectionToClient conn)
    {
        int seats = Mathf.Max(1, SeatsCount());

        if (connToSeat.TryGetValue(conn.connectionId, out int existing))
        {
            // Clamp to available seats in case list changed
            if (existing >= 0 && existing < seats) return existing;
        }

        // Build used set from current mapping ONLY (no scene scans)
        bool[] used = new bool[seats];
        foreach (var kv in connToSeat)
        {
            int s = kv.Value;
            if (s >= 0 && s < seats) used[s] = true;
        }

        // Pick first free seat by index
        int chosen = 0;
        for (int i = 0; i < seats; i++)
        {
            if (!used[i]) { chosen = i; break; }
        }

        connToSeat[conn.connectionId] = chosen;
        return chosen;
    }

    private int SeatsCount()
    {
        return (spawnPoints != null && spawnPoints.Count > 0) ? spawnPoints.Count : maxPlayers;
    }

    // World pose from spawn point; rotation faces along spawn forward (Z), yaw only
    private void GetSeatPose(int seat, Vector3 fallbackPos, Quaternion fallbackRot, out Vector3 pos, out Quaternion rot)
    {
        pos = fallbackPos;
        rot = fallbackRot;

        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            Transform sp = spawnPoints[seat % spawnPoints.Count];
            if (sp != null)
            {
                pos = sp.position;
                rot = YawOnly(sp);
            }
        }
    }

    // Build a yaw-only rotation from a transform's forward axis (Z), ignoring pitch/roll
    private static Quaternion YawOnly(Transform t)
    {
        Vector3 fwd = t.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        return Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }
}
