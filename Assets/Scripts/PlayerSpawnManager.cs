using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSpawnManager : NetworkManager
{
    [Header("Spawn Points (set 1–6 transforms here)")]
    public List<Transform> spawnPoints = new List<Transform>();

    private int nextSpawnIndex = 0;

    public override void Awake()
    {
        base.Awake();

        // Keep Mirror editor happy even if inspector didn’t serialize it yet
        if (spawnPrefabs == null)
            spawnPrefabs = new List<GameObject>();

        // Helpful warnings
        if (playerPrefab == null)
            Debug.LogError("[PlayerSpawnManager] Player Prefab is not assigned.");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!autoCreatePlayer)
            Debug.LogWarning("[PlayerSpawnManager] Auto Create Player is OFF. The host will not auto-spawn a player.");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Transform startPos = null;
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            startPos = spawnPoints[nextSpawnIndex % spawnPoints.Count];
            nextSpawnIndex++;
        }

        GameObject player = startPos != null
            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
            : Instantiate(playerPrefab);

        NetworkServer.AddPlayerForConnection(conn, player);
        Debug.Log("[SpawnManager] Player " + conn.connectionId + " spawned at " + (startPos != null ? startPos.name : "default"));
    }
}
