// 10/9/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

// 10/9/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using System.Collections.Generic; // For List<>
using UnityEngine.InputSystem; // For the New Input System

public class CardDeckManager : MonoBehaviour
{
    private GameObject currentCardObject; // To track the currently displayed card

    public List<Card> deck; // The current deck of cards
    public Transform[] cardSpawnPoints; // Spawn points for each player
    public GameObject cardPrefab; // The card prefab to instantiate

    [SerializeField]
    private List<Card> masterCardList; // The full list of all cards for reshuffling

    private PlayerInputActions inputActions; // Reference to the generated Input Actions class
    private bool hasDrawnCard = false; // Flag to track if the current player has drawn a card

    private List<Player> players = new List<Player>();
    private int currentPlayerIndex = 0;

    private void Awake()
    {
        // Initialize the Input Actions
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        // Enable the Input Actions
        inputActions.Enable();

        // Subscribe to the SpawnCardAction
        inputActions.Player.SpawnCardAction.performed += OnSpawnCardActionPerformed;
    }

    private void OnDisable()
    {
        // Unsubscribe from the SpawnCardAction
        inputActions.Player.SpawnCardAction.performed -= OnSpawnCardActionPerformed;

        // Disable the Input Actions
        inputActions.Disable();
    }

    public void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(0, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    private void Start()
    {
        // Initialize players
        players.Add(new Player("Player 1"));
        players.Add(new Player("Player 2"));
        players.Add(new Player("Player 3"));
        players.Add(new Player("Player 4"));
        players.Add(new Player("Player 5"));
        players.Add(new Player("Player 6"));

        Debug.Log("Game initialized with 6 players.");

        // Distribute 4 cards to each player at the start
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                DrawCardForPlayer(i);
            }
        }

        Debug.Log("Each player starts with 4 cards.");
    }

    private void OnSpawnCardActionPerformed(InputAction.CallbackContext context)
    {
        // Triggered when SpawnCardAction is performed
        Debug.Log("SpawnCardAction triggered!");

        // Give 1 card to the current player
        DrawCardForPlayer(currentPlayerIndex);

        // Move to the next player
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;

        // Reset the flag for the new player's turn
        hasDrawnCard = false;

        Debug.Log($"It's now {players[currentPlayerIndex].playerName}'s turn.");
    }

    public void DrawCardForPlayer(int playerIndex)
    {
        if (deck == null || deck.Count == 0)
        {
            Debug.LogError("Deck is empty! Cannot draw a card.");
            return;
        }

        // Draw a random card
        int randomIndex = Random.Range(0, deck.Count);
        Card drawnCard = deck[randomIndex];

        // Remove the card from the deck
        deck.RemoveAt(randomIndex);

        // Add the card to the player's hand
        players[playerIndex].AddCardToHand(drawnCard);

        // Instantiate the card at the player's spawn point
        Transform spawnPoint = cardSpawnPoints[playerIndex];
        GameObject cardObject = Instantiate(cardPrefab, spawnPoint.position, Quaternion.identity);
        CardDisplay cardDisplay = cardObject.GetComponent<CardDisplay>();

        // Set the card's data for display
        cardDisplay.SetCard(drawnCard);

        Debug.Log($"{players[playerIndex].playerName} drew a card: {drawnCard.cardName}");
    }

    private void NextTurn()
    {
        // Increment the current player index
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;

        // Reset the flag for the new player's turn
        hasDrawnCard = false;

        // Log the current player's turn
        Debug.Log($"It's now {players[currentPlayerIndex].playerName}'s turn.");
    }
}