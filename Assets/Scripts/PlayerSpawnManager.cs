// =============================================
// File: PlayerSpawnManager.cs (updated to 5 players)
// =============================================
using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSpawnManager : NetworkManager
{
    [Header("Spawn Points (set 1-5 transforms here)")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Match Size")]
    [SerializeField] private int maxPlayers = 5; // total including host

    private int nextSpawnIndex = 0;

    public override void Awake()
    {
        base.Awake();
        if (spawnPrefabs == null) spawnPrefabs = new List<GameObject>();
        if (playerPrefab == null) Debug.LogError("[PlayerSpawnManager] Player Prefab is not assigned.");

        // Mirror's maxConnections is for clients only. For 5 total players
        // (1 host + 4 clients), set maxConnections to 4.
        maxConnections = Mathf.Max(0, maxPlayers - 1);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!autoCreatePlayer)
            Debug.LogWarning("[PlayerSpawnManager] Auto Create Player is OFF. Host will not auto-spawn a player.");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxPlayers)
        {
            conn.Disconnect();
            return;
        }

        Transform startPos = null;
        int usedIndex = 0;

        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            usedIndex = nextSpawnIndex % spawnPoints.Count;
            startPos = spawnPoints[usedIndex];
            nextSpawnIndex++;
        }

        GameObject player = startPos != null
            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
            : Instantiate(playerPrefab);

        // NEW: set seat index before adding to connection
        var ps = player.GetComponent<PlayerState>();
        if (ps != null) ps.seatIndex = usedIndex;

        NetworkServer.AddPlayerForConnection(conn, player);
    }

}
