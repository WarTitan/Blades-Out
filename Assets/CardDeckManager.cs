using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CardDeckManager : NetworkBehaviour
{
    public static CardDeckManager Instance { get; private set; }

    [Header("Multiplayer Settings")]
    public int maxPlayers = 6;
    public Transform[] spawnPoints; // 👈 Assign these in Inspector
    public float tableRadius = 5f;

    [Header("Prefabs")]
    public GameObject cardPrefab;
    public GameObject heartBarPrefab;

    private readonly List<NetworkPlayer> connectedPlayers = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        connectedPlayers.Clear();
        connectedPlayers.AddRange(players);

        Debug.Log($"✅ Player {clientId} joined. Total players: {connectedPlayers.Count}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"❌ Player {clientId} disconnected.");
    }

    public Vector3 GetSpawnPositionForPlayer(ulong clientId)
    {
        var index = GetPlayerIndex(clientId);
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Clamp index to available spawn points
            index = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            return spawnPoints[index].position;
        }

        // 🔄 Fallback to circle layout
        if (maxPlayers <= 0) maxPlayers = 1;
        float angle = 360f / maxPlayers * index;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 center = Vector3.zero;
        Vector3 pos = center + new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * tableRadius;
        return pos;
    }

    public Quaternion GetSpawnRotationForPlayer(ulong clientId)
    {
        var index = GetPlayerIndex(clientId);
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            index = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            return spawnPoints[index].rotation;
        }

        float angle = 360f / maxPlayers * index + 180f;
        return Quaternion.Euler(0, angle, 0);
    }

    private int GetPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            if (connectedPlayers[i].OwnerClientId == clientId)
                return i;
        }
        return connectedPlayers.Count;
    }

    private void Update()
    {
        // Host manually spawns a card
        if (IsServer && Input.GetKeyDown(KeyCode.Space))
        {
            SpawnCard();
        }
    }

    private void SpawnCard()
    {
        if (cardPrefab == null) return;

        GameObject card = Instantiate(cardPrefab, Vector3.zero + Vector3.up * 2f, Quaternion.identity);
        var netObj = card.GetComponent<NetworkObject>();
        netObj.Spawn();

        Debug.Log("🃏 Host spawned a card.");
    }
}
