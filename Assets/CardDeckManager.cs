using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class CardDeckManager : NetworkBehaviour
{
    public static CardDeckManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject card3DPrefab;
    [SerializeField] private HeartBar heartBarPrefab;
    [SerializeField] private List<Transform> playerSpawnPoints = new List<Transform>();

    [Header("Settings")]
    [SerializeField] private int maxPlayers = 6;

    private List<NetworkPlayer> networkPlayers = new List<NetworkPlayer>();

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            Debug.Log("Server is waiting for clients...");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (networkPlayers.Count >= maxPlayers)
        {
            Debug.LogWarning("Lobby full. Connection rejected.");
            return;
        }

        NetworkPlayer[] foundPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        networkPlayers = new List<NetworkPlayer>(foundPlayers);

        int index = networkPlayers.Count - 1;
        if (index < playerSpawnPoints.Count)
        {
            NetworkPlayer player = networkPlayers[index];
            Transform spawn = playerSpawnPoints[index];

            player.transform.position = spawn.position;
            player.transform.rotation = spawn.rotation;

            if (heartBarPrefab != null)
            {
                HeartBar hb = Instantiate(heartBarPrefab);
                hb.Initialize(player.transform, player.PlayerCamera, player.CardSpawnPoint);
                hb.SetHearts(player.CurrentHearts, player.MaxHearts);
            }

            Debug.Log($"Spawned Player {index + 1} at spawn point {index + 1}");
        }
        else
        {
            Debug.LogWarning("Not enough spawn points for player " + clientId);
        }
    }

    public Vector3 GetSpawnPositionForPlayer(ulong clientId)
    {
        int index = (int)(clientId % (ulong)playerSpawnPoints.Count);
        if (index >= 0 && index < playerSpawnPoints.Count)
            return playerSpawnPoints[index].position;
        return Vector3.zero;
    }

    private void Update()
    {
        // Only the host can spawn cards
        if (!IsServer) return;

        // Press SPACE to deal cards to all players
        if (Input.GetKeyDown(KeyCode.Space))
        {
            foreach (var player in networkPlayers)
            {
                SpawnCardForPlayer(player);
            }
        }
    }

    private void SpawnCardForPlayer(NetworkPlayer player)
    {
        if (card3DPrefab == null || player == null) return;

        Transform spawnPoint = player.CardSpawnPoint;
        GameObject cardInstance = Instantiate(card3DPrefab, spawnPoint.position, spawnPoint.rotation);

        // Flip 180° so the FRONT faces the player
        cardInstance.transform.Rotate(180f, 180f, 0f, Space.Self);

        NetworkObject netObj = cardInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
    }
}
