// FILE: PlayerSpawnManager.cs
// Starts/ends darts test without creating an extra overlay HUD.
// On end (press 3), immediately grants drawAfterTurn via TurnManagerNet.Server_GrantReturnToTableDraw().

using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSpawnManager : NetworkManager
{
    [Header("Lobby Spawns (optional)")]
    public List<Transform> lobbySpawnPoints = new List<Transform>();

    [Header("Game/Table Spawns (ensure count >= expected players)")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Darts Test Spawns (5 recommended)")]
    public List<Transform> dartsSpawnPoints = new List<Transform>();

    public enum DartsFacingMode { SpawnRotation, FaceWorldZ, FaceBoardAnchor }

    [Header("Darts Facing")]
    public DartsFacingMode dartsFacing = DartsFacingMode.FaceWorldZ;
    public Transform dartsBoardAnchor;

    [Header("Match Size")]
    [SerializeField] private int maxPlayers = 5;

    private readonly Dictionary<int, int> connToSeat = new Dictionary<int, int>();

    public override void OnStartServer() { base.OnStartServer(); connToSeat.Clear(); }
    public override void OnStopServer() { base.OnStopServer(); connToSeat.Clear(); }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GameObject player = Instantiate(playerPrefab);
        int seat = AssignSeatForConnection(conn);

        var ps = player.GetComponent<PlayerState>();
        if (ps != null) ps.seatIndex = seat;

        if (lobbySpawnPoints != null && lobbySpawnPoints.Count > 0)
        {
            Transform lsp = lobbySpawnPoints[seat % lobbySpawnPoints.Count];
            if (lsp != null) player.transform.SetPositionAndRotation(lsp.position, YawOnly(lsp));
        }

        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn != null) connToSeat.Remove(conn.connectionId);
        base.OnServerDisconnect(conn);
    }

    // ---------- Start darts test (press 6) ----------
    [Server]
    public void Server_StartDartsTest()
    {
        var tm = TurnManagerNet.Instance;
        if (tm != null)
        {
            tm.ignoreLobbyForTesting = true;
            tm.Server_BeginDartsTest();         // phase becomes Darts -> original HUD should show "Playing Darts"
        }

        var ls = LobbyStage.Instance;
        if (ls != null) ls.lobbyActive = false;

        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;
            var tp = conn.identity.GetComponent<TeleportHelper>();
            if (tp != null)
            {
                tp.Target_DisableLobbyCameraLocal(conn); // no overlay HUD anymore
            }
        }

        Server_TeleportAllPlayersToDartsSpawns();

        Debug.Log("[PlayerSpawnManager] Darts test started: lobby OFF, everyone at darts spawns.");
    }

    // ---------- End darts test and return to table (press 3) ----------
    [Server]
    public void Server_EndDartsTestAndReturnToTable()
    {
        var tm = TurnManagerNet.Instance;
        if (tm != null)
        {
            tm.Server_EndDartsTest();
            tm.ignoreLobbyForTesting = false;
        }

        Server_TeleportAllPlayersToGameSpawns();

        // Give items immediately so players do NOT see zero on return.
        if (tm != null) tm.Server_GrantReturnToTableDraw();

        Debug.Log("[PlayerSpawnManager] Darts test ended: returned to table spawns and granted items.");
    }

    // ---------- Teleports ----------
    [Server]
    public void Server_TeleportOne(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null) return;

        GameObject go = conn.identity.gameObject;
        int seat = AssignSeatForConnection(conn);

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

    [Server]
    public void Server_TeleportAllPlayersToGameSpawns()
    {
        foreach (var kv in NetworkServer.connections)
        {
            var c = kv.Value;
            if (c == null || c.identity == null) continue;
            int seat = AssignSeatForConnection(c);
            var ps = c.identity.GetComponent<PlayerState>();
            if (ps != null) ps.seatIndex = seat;
        }

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

    [Server]
    public void Server_TeleportAllPlayersToDartsSpawns()
    {
        if (dartsSpawnPoints == null || dartsSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] No dartsSpawnPoints set. Aborting darts teleport.");
            return;
        }

        foreach (var kv in NetworkServer.connections)
        {
            var c = kv.Value;
            if (c == null || c.identity == null) continue;
            int seat = AssignSeatForConnection(c);
            var ps = c.identity.GetComponent<PlayerState>();
            if (ps != null) ps.seatIndex = seat;
        }

        int rr = 0;

        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            GameObject go = conn.identity.gameObject;
            int seat = connToSeat.TryGetValue(conn.connectionId, out int s) ? s : 0;
            int seatIdx0 = Mathf.Max(0, seat);

            Transform spawn = null;
            if (seatIdx0 < dartsSpawnPoints.Count) spawn = dartsSpawnPoints[seatIdx0];

            if (spawn == null)
            {
                for (int tries = 0; tries < dartsSpawnPoints.Count; tries++)
                {
                    int idx = (rr + tries) % dartsSpawnPoints.Count;
                    if (dartsSpawnPoints[idx] != null)
                    {
                        spawn = dartsSpawnPoints[idx];
                        rr = (idx + 1) % dartsSpawnPoints.Count;
                        break;
                    }
                }
            }

            if (spawn == null)
            {
                Debug.LogWarning("[PlayerSpawnManager] No DARTS spawn for player '" + go.name + "'.");
                continue;
            }

            Quaternion rot = ComputeDartsFacing(spawn);

            var cc = go.GetComponent<CharacterController>();
            bool hadCC = (cc != null && cc.enabled);
            if (hadCC) cc.enabled = false;

            go.transform.SetPositionAndRotation(spawn.position, rot);

            if (hadCC) cc.enabled = true;

            var ps = go.GetComponent<PlayerState>();
            var tp = go.GetComponent<TeleportHelper>();
            if (tp != null && ps != null && ps.connectionToClient != null)
                tp.TargetSnapAndEnterGameplay(ps.connectionToClient, spawn.position, rot);
        }

        Debug.Log("[PlayerSpawnManager] Moved all players to DARTS spawns.");
    }

    // ---------- Helpers ----------
    [Server]
    private int AssignSeatForConnection(NetworkConnectionToClient conn)
    {
        int seats = Mathf.Max(1, SeatsCount());

        if (connToSeat.TryGetValue(conn.connectionId, out int existing))
        {
            if (existing >= 0 && existing < seats) return existing;
        }

        bool[] used = new bool[seats];
        foreach (var kv in connToSeat) { int s = kv.Value; if (s >= 0 && s < seats) used[s] = true; }

        int chosen = 0;
        for (int i = 0; i < seats; i++) { if (!used[i]) { chosen = i; break; } }

        connToSeat[conn.connectionId] = chosen;
        return chosen;
    }

    private int SeatsCount()
    {
        return (spawnPoints != null && spawnPoints.Count > 0) ? spawnPoints.Count : maxPlayers;
    }

    private void GetSeatPose(int seat, Vector3 fallbackPos, Quaternion fallbackRot, out Vector3 pos, out Quaternion rot)
    {
        pos = fallbackPos; rot = fallbackRot;
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            Transform sp = spawnPoints[seat % spawnPoints.Count];
            if (sp != null) { pos = sp.position; rot = YawOnly(sp); }
        }
    }

    private Quaternion ComputeDartsFacing(Transform spawn)
    {
        switch (dartsFacing)
        {
            case DartsFacingMode.FaceWorldZ: return Quaternion.LookRotation(Vector3.forward, Vector3.up);
            case DartsFacingMode.FaceBoardAnchor:
                if (dartsBoardAnchor != null)
                {
                    Vector3 to = dartsBoardAnchor.position - spawn.position; to.y = 0f;
                    if (to.sqrMagnitude > 0.0001f) return Quaternion.LookRotation(to.normalized, Vector3.up);
                }
                return YawOnly(spawn);
            case DartsFacingMode.SpawnRotation:
            default: return YawOnly(spawn);
        }
    }

    private static Quaternion YawOnly(Transform t)
    {
        Vector3 fwd = t.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        return Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }
}
