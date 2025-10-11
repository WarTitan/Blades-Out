using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CardDeckManager : MonoBehaviour
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

    private List<Player> players = new List<Player>();
    private PlayerInputActions inputActions;
    private int currentPlayerIndex = 0;
    private Dictionary<int, int> playerCardCounts = new Dictionary<int, int>();

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
    }

    private void Start()
    {
        // --- Create players ---
        for (int i = 0; i < cardSpawnPoints.Length; i++)
        {
            Player player = new Player($"Player {i + 1}");
            players.Add(player);
            playerCardCounts[i] = 0;
        }

        // Shuffle the deck
        ShuffleMasterDeck();

        // --- Deal starting cards ---
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = 0; j < startingCardsPerPlayer; j++)
                DrawCardForPlayer(i);
        }

        // --- Spawn Minecraft-style Heart Bars ---
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
            else
            {
                Debug.LogWarning($"Missing HeartBar prefab or camera/spawnpoint for player {i + 1}");
            }
        }
    }

    private void OnSpawnCardPressed(InputAction.CallbackContext context)
    {
        DrawCardForPlayer(currentPlayerIndex);
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
    }

    private void DrawCardForPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count) return;
        if (masterCardList == null || masterCardList.Count == 0) return;

        Transform spawnPoint = cardSpawnPoints[playerIndex];
        if (spawnPoint == null) return;

        // Pick a random card
        Card randomCard = masterCardList[Random.Range(0, masterCardList.Count)];
        players[playerIndex].AddCardToHand(randomCard);

        // Get camera (center of the circle)
        Camera cam = (playerIndex < playerCameras.Length) ? playerCameras[playerIndex] : null;
        if (cam == null)
        {
            Debug.LogWarning($"No camera assigned for player {playerIndex + 1}");
            return;
        }

        // --- Circle geometry ---
        Vector3 center = cam.transform.position;
        float radius = Vector3.Distance(spawnPoint.position, center);
        Vector3 centerToSpawn = (spawnPoint.position - center).normalized;

        // --- Alternating arc pattern ---
        int n = playerCardCounts[playerIndex]; // index of the card
        int k = (n + 1) / 2;
        float sign = (n % 2 == 1) ? +1f : -1f;
        if (n == 0) sign = 0f;

        float fanStepAngle = 12.5f;
        float angleOffset = sign * k * fanStepAngle;

        // --- Position on circle rim ---
        Quaternion rotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
        Vector3 dirOnRim = rotation * centerToSpawn;
        Vector3 spawnPos = center + dirOnRim * radius;

        // --- Instantiate card ---
        Card3D cardInstance = Instantiate(card3D, spawnPos, Quaternion.identity);

        // --- Make card face toward the camera ---
        Vector3 toCamera = (cam.transform.position - spawnPos).normalized;
        cardInstance.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);

        // Flip 180° so the FRONT faces the camera
        cardInstance.transform.Rotate(180f, 180f, 0f, Space.Self);
        // Add tilt
        cardInstance.transform.Rotate(cardTiltAngle, 0f, 0f, Space.Self);

        // Slight curve rotation for realism
        cardInstance.transform.Rotate(Vector3.up, angleOffset * 0.5f, Space.World);

        // Scale down
        cardInstance.transform.localScale = cardScale;

        // Apply card data
        cardInstance.cardData = randomCard;
        cardInstance.ApplyCardData();

        playerCardCounts[playerIndex]++;
    }

    private Vector3 GetForwardBetweenPlayerAndSpawn(int playerIndex)
    {
        Camera cam = (playerIndex < playerCameras.Length) ? playerCameras[playerIndex] : null;
        Transform spawn = cardSpawnPoints[playerIndex];

        if (cam == null || spawn == null)
            return spawn.forward; // fallback

        Vector3 toCam = (cam.transform.position - spawn.position).normalized;
        return toCam;
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
