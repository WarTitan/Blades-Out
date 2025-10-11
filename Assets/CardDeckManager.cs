using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class CardDeckManager : NetworkBehaviour
{
    [Header("Card Setup")]
    public Card3D card3D;
    public List<Card> masterCardList;

    [Header("Card Layout")]
    public float cardSpacing = 0.20f;
    public Vector3 cardScale = new Vector3(0.085f, 0.12f, 0.01f);
    public float cardTiltAngle = 45f;

    [Header("Game Settings")]
    public int startingCardsPerPlayer = 4;

    [Header("❤️ Health Bar Setup")]
    [SerializeField] private HeartBar heartBarPrefab;
    [SerializeField] private NetworkPlayer networkPlayerPrefab;

    [Header("Multiplayer Settings")]
    [SerializeField] private int maxPlayers = 6;
    [SerializeField] private Transform[] spawnPoints; // Seats around the table

    private PlayerInputActions inputActions;
    private readonly Dictionary<ulong, NetworkPlayer> connectedPlayers = new Dictionary<ulong, NetworkPlayer>();
    private readonly Dictionary<ulong, int> playerCardCounts = new Dictionary<ulong, int>();

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
    }

    private void OnClientConnected(ulong clientId)
    {
        // Cap player count
        if (connectedPlayers.Count >= maxPlayers)
        {
            Debug.LogWarning($"Player {clientId} tried to join, but lobby is full ({maxPlayers}).");
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }

        // Pick seat (spawn point)
        int seatIndex = connectedPlayers.Count % spawnPoints.Length;
        Transform seat = spawnPoints[seatIndex];

        // Spawn player prefab at seat
        NetworkPlayer player = Instantiate(networkPlayerPrefab, seat.position, seat.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        connectedPlayers[clientId] = player;

        Debug.Log($"Spawned player {clientId} at seat {seatIndex}");

        // Spawn health bar
        if (heartBarPrefab != null)
        {
            HeartBar hb = Instantiate(heartBarPrefab);
            hb.Initialize(player.CardSpawnPoint, player.PlayerCamera, player.CardSpawnPoint);
            hb.SetHearts(player.currentHearts.Value, player.maxHearts.Value);
            player.HeartBar = hb;
        }

        // Deal starting cards
        for (int j = 0; j < startingCardsPerPlayer; j++)
            DealCardToClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (connectedPlayers.TryGetValue(clientId, out NetworkPlayer player))
        {
            if (player != null)
                Destroy(player.gameObject);
            connectedPlayers.Remove(clientId);
        }
    }

    private void OnSpawnCardPressed(InputAction.CallbackContext context)
    {
        if (IsServer)
        {
            foreach (var kv in connectedPlayers)
                DealCardToClient(kv.Key);
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
        DealCardToClient(senderId);
    }

    private void DealCardToClient(ulong clientId)
    {
        if (masterCardList == null || masterCardList.Count == 0) return;

        Card randomCard = masterCardList[Random.Range(0, masterCardList.Count)];
        masterCardList.Remove(randomCard);

        SendCardToClientClientRpc(randomCard.id, clientId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    private void SendCardToClientClientRpc(int cardId, ulong clientId, ClientRpcParams rpcParams = default)
    {
        if (!connectedPlayers.ContainsKey(clientId)) return;

        NetworkPlayer player = connectedPlayers[clientId];
        if (player == null || player.CardSpawnPoint == null) return;

        Card cardData = masterCardList.Find(c => c.id == cardId);
        if (cardData == null) return;

        SpawnCardVisual(cardData, player);
    }

    private void SpawnCardVisual(Card randomCard, NetworkPlayer player)
    {
        Transform spawnPoint = player.CardSpawnPoint;
        Camera cam = player.PlayerCamera;
        if (spawnPoint == null || cam == null) return;

        Vector3 basePos = spawnPoint.position;
        int n = playerCardCounts.ContainsKey(player.OwnerClientId) ? playerCardCounts[player.OwnerClientId] : 0;
        int k = (n + 1) / 2;
        float sign = (n % 2 == 1) ? +1f : -1f;
        if (n == 0) sign = 0f;

        float offset = sign * k * cardSpacing;
        Vector3 spawnPos = basePos + spawnPoint.right * offset;

        Card3D cardInstance = Instantiate(card3D, spawnPos, Quaternion.identity);
        Vector3 toCamera = (cam.transform.position - spawnPos).normalized;
        cardInstance.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);

        cardInstance.transform.Rotate(180f, 180f, 0f, Space.Self);
        cardInstance.transform.Rotate(cardTiltAngle, 0f, 0f, Space.Self);
        cardInstance.transform.localScale = cardScale;

        cardInstance.cardData = randomCard;
        cardInstance.ApplyCardData();

        playerCardCounts[player.OwnerClientId] = n + 1;
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
