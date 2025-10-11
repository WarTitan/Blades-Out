using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Steamworks;

public class CardDeckManager : NetworkBehaviour
{
    [Header("Card Setup")]
    public Card3D card3D;                           // Prefab for 3D card
    public List<Card> masterCardList;               // All available cards
    public Transform[] cardSpawnPoints;             // Spawn positions for each player
    public Camera[] playerCameras;                  // Player cameras

    [Header("Card Layout")]
    public float cardSpacing = 0.20f;               // Distance between cards
    public Vector3 cardScale = new Vector3(0.085f, 0.12f, 0.01f);
    public float cardTiltAngle = 45f;               // Tilt toward player
    public float forwardOffset = 0.15f;             // Forward offset from spawn point

    [Header("Game Settings")]
    public int startingCardsPerPlayer = 4;

    [Header("❤️ Health Bar Setup")]
    [SerializeField] private HeartBar heartBarPrefab;   // Prefab for Minecraft-style heart bar
    [SerializeField] private NetworkPlayer networkPlayerPrefab; // Networked player prefab

    private List<Player> players = new List<Player>();
    private PlayerInputActions inputActions;
    private int currentPlayerIndex = 0;
    private Dictionary<int, int> playerCardCounts = new Dictionary<int, int>();

    // For networked player management
    private readonly Dictionary<ulong, NetworkPlayer> connectedPlayers = new Dictionary<ulong, NetworkPlayer>();

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.SpawnCardAction.performed += OnSpawnCardPressed;
    }

    private void OnDisable()
    {
        inputActions.Player.SpawnCardAction.performed -= OnSpawnCardPressed;
        inputActions.Disable();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            ShuffleMasterDeck();
        }

        // Local fallback (single-player mode)
        if (!NetworkManager.Singleton.IsListening)
            LocalSinglePlayerSetup();
    }

    private void LocalSinglePlayerSetup()
    {
        // --- Create local players ---
        for (int i = 0; i < cardSpawnPoints.Length; i++)
        {
            Player player = new Player($"Player {i + 1}");
            players.Add(player);
            playerCardCounts[i] = 0;
        }

        ShuffleMasterDeck();

        // --- Deal starting cards ---
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = 0; j < startingCardsPerPlayer; j++)
                DrawCardForPlayer(i);
        }

        // --- Spawn Heart Bars ---
        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            if (heartBarPrefab != null && i < playerCameras.Length && i < cardSpawnPoints.Length)
            {
                HeartBar hb = Instantiate(heartBarPrefab);
                hb.Initialize(playerCameras[i].transform, playerCameras[i], cardSpawnPoints[i]);
                hb.SetHearts(player.currentHearts, player.maxHearts);
                player.heartBar = hb;
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        int spawnIndex = connectedPlayers.Count % cardSpawnPoints.Length;
        Transform spawn = cardSpawnPoints[spawnIndex];

        NetworkPlayer player = Instantiate(networkPlayerPrefab, spawn.position, spawn.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        connectedPlayers[clientId] = player;

        Debug.Log($"Spawned player {clientId} at spawn point {spawnIndex}");

        // Spawn health bar for this player
        HeartBar hb = Instantiate(heartBarPrefab);
        hb.Initialize(spawn, player.playerCamera, spawn);
        hb.SetHearts(player.currentHearts.Value, player.maxHearts.Value);
        player.heartBar = hb;

        // Deal starting cards for that player
        for (int j = 0; j < startingCardsPerPlayer; j++)
            DealCardToClient(clientId, spawnIndex);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (connectedPlayers.TryGetValue(clientId, out NetworkPlayer player))
        {
            if (player != null && player.gameObject != null)
                Destroy(player.gameObject);

            connectedPlayers.Remove(clientId);
        }
    }

    private void OnSpawnCardPressed(InputAction.CallbackContext context)
    {
        // Local spawn fallback for testing
        if (!NetworkManager.Singleton.IsListening)
        {
            DrawCardForPlayer(currentPlayerIndex);
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        }
        else if (IsServer)
        {
            foreach (var kv in connectedPlayers)
            {
                DealCardToClient(kv.Key, (int)(kv.Key % (ulong)cardSpawnPoints.Length));
            }
        }
        else if (IsClient)
        {
            RequestDrawCardServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDrawCardServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        int spawnIndex = (int)(senderId % (ulong)cardSpawnPoints.Length);
        DealCardToClient(senderId, spawnIndex);
    }

    private void DealCardToClient(ulong clientId, int playerIndex)
    {
        if (masterCardList == null || masterCardList.Count == 0) return;
        Card randomCard = masterCardList[Random.Range(0, masterCardList.Count)];
        masterCardList.Remove(randomCard);

        SendCardToClientClientRpc(randomCard.id, playerIndex, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    private void SendCardToClientClientRpc(int cardId, int playerIndex, ClientRpcParams rpcParams = default)
    {
        if (playerIndex < 0 || playerIndex >= cardSpawnPoints.Length) return;

        Transform spawnPoint = cardSpawnPoints[playerIndex];
        Camera cam = (playerIndex < playerCameras.Length) ? playerCameras[playerIndex] : null;
        if (cam == null) return;

        Card cardData = masterCardList.Find(c => c.id == cardId);
        if (cardData == null) return;

        // Spawn visually
        SpawnCardVisual(cardData, playerIndex);
    }

    private void DrawCardForPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count) return;
        if (masterCardList == null || masterCardList.Count == 0) return;

        Card randomCard = masterCardList[Random.Range(0, masterCardList.Count)];
        players[playerIndex].AddCardToHand(randomCard);
        SpawnCardVisual(randomCard, playerIndex);
    }

    private void SpawnCardVisual(Card randomCard, int playerIndex)
    {
        Transform spawnPoint = cardSpawnPoints[playerIndex];
        Camera cam = playerCameras[playerIndex];

        // Geometry (same as before)
        Vector3 center = cam.transform.position;
        float radius = Vector3.Distance(spawnPoint.position, center);
        Vector3 centerToSpawn = (spawnPoint.position - center).normalized;

        int n = playerCardCounts.ContainsKey(playerIndex) ? playerCardCounts[playerIndex] : 0;
        int k = (n + 1) / 2;
        float sign = (n % 2 == 1) ? +1f : -1f;
        if (n == 0) sign = 0f;

        float fanStepAngle = 12.5f;
        float angleOffset = sign * k * fanStepAngle;

        Quaternion rotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
        Vector3 dirOnRim = rotation * centerToSpawn;
        Vector3 spawnPos = center + dirOnRim * radius;

        Card3D cardInstance = Instantiate(card3D, spawnPos, Quaternion.identity);
        Vector3 toCamera = (cam.transform.position - spawnPos).normalized;
        cardInstance.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);

        // Face front to camera
        cardInstance.transform.Rotate(180f, 180f, 0f, Space.Self);
        cardInstance.transform.Rotate(cardTiltAngle, 0f, 0f, Space.Self);
        cardInstance.transform.Rotate(Vector3.up, angleOffset * 0.5f, Space.World);
        cardInstance.transform.localScale = cardScale;

        cardInstance.cardData = randomCard;
        cardInstance.ApplyCardData();

        playerCardCounts[playerIndex] = n + 1;
    }

    private void ShuffleMasterDeck()
    {
        for (int i = 0; i < masterCardList.Count; i++)
        {
            int r = Random.Range(i, masterCardList.Count);
            (masterCardList[i], masterCardList[r]) = (masterCardList[r], masterCardList[i]);
        }
    }
}
