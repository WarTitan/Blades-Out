// 10/9/2025 AI-Tag
// Updated for 3D card system using Card3D and ScriptableObject cards.

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CardDeckManager : MonoBehaviour
{
    [Header("Card Setup")]
    public GameObject cardPrefab;             // 3D Card prefab to instantiate
    public List<Card> masterCardList;         // The master list of all possible cards
    public Transform[] cardSpawnPoints;       // 6 spawn points (1 per player)

    [Header("Game Settings")]
    public int startingCardsPerPlayer = 4;    // How many cards each player starts with

    private List<Player> players = new List<Player>();
    private PlayerInputActions inputActions;
    private int currentPlayerIndex = 0;

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
        // Initialize 6 players
        for (int i = 0; i < 6; i++)
        {
            players.Add(new Player($"Player {i + 1}"));
        }

        Debug.Log("Initialized 6 players.");

        // Shuffle just for variety (optional)
        ShuffleMasterDeck();

        // Deal starting cards
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = 0; j < startingCardsPerPlayer; j++)
            {
                DrawCardForPlayer(i);
            }
        }

        Debug.Log("Each player received 4 starting cards.");
    }

    private void OnSpawnCardPressed(InputAction.CallbackContext context)
    {
        // When Space is pressed, give 1 card to the current player
        DrawCardForPlayer(currentPlayerIndex);

        // Move to the next player
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;

        Debug.Log($"Next turn: {players[currentPlayerIndex].playerName}");
    }

    // 10/10/2025 AI-Tag
    // This was created with the help of Assistant, a Unity Artificial Intelligence product.

    private void DrawCardForPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count)
        {
            Debug.LogError($"Invalid player index: {playerIndex}. Cannot draw card.");
            return;
        }

        if (masterCardList == null || masterCardList.Count == 0)
        {
            Debug.LogError("Master card list is empty! Cannot draw cards.");
            return;
        }

        if (playerIndex >= cardSpawnPoints.Length)
        {
            Debug.LogError($"Invalid spawn point index: {playerIndex}. Ensure cardSpawnPoints array has enough elements.");
            return;
        }

        // Pick a random card from the master list (infinite deck)
        Card randomCard = masterCardList[Random.Range(0, masterCardList.Count)];

        // Add the card to the player's hand
        players[playerIndex].AddCardToHand(randomCard);

        // Spawn the card visually at the player's spawn point
        Transform spawnPoint = cardSpawnPoints[playerIndex];
        GameObject cardObject = Instantiate(cardPrefab, spawnPoint.position, spawnPoint.rotation);

        // Adjust the size (scale) of the card
        cardObject.transform.localScale = new Vector3(0.22f, 0.3f, 0.01f); // Adjust these values as needed

        // Adjust the rotation of the card
        cardObject.transform.rotation = Quaternion.Euler(-270, 0, 0); // Example: Rotate 180 degrees on the Y-axis

        // Apply data to the 3D card
        Card3D card3D = cardObject.GetComponent<Card3D>();
        if (card3D != null)
        {
            card3D.cardData = randomCard;
            card3D.ApplyCardData();
        }
        else
        {
            Debug.LogWarning("Spawned card prefab does not have a Card3D component!");
        }

        Debug.Log($"{players[playerIndex].playerName} drew {randomCard.cardName}");
    }

    private void ShuffleMasterDeck()
    {
        for (int i = 0; i < masterCardList.Count; i++)
        {
            int randomIndex = Random.Range(i, masterCardList.Count);
            (masterCardList[i], masterCardList[randomIndex]) = (masterCardList[randomIndex], masterCardList[i]);
        }

        Debug.Log("Master deck shuffled.");
    }
}
