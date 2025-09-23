// 9/23/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using System.Collections.Generic; // For List<>
using UnityEngine.InputSystem; // For the New Input System

public class CardDeckManager : MonoBehaviour
{
    private GameObject currentCardObject; // To track the currently displayed card

    public List<Card> deck; // The current deck of cards
    public Transform cardSpawnPoint; // The position where cards will appear
    public GameObject cardPrefab; // The card prefab to instantiate

    [SerializeField]
    private List<Card> masterCardList; // The full list of all cards for reshuffling

    private PlayerInputActions inputActions; // Reference to the generated Input Actions class

    private bool hasDrawnCard = false; // Flag to track if the current player has drawn a card

    private void Awake()
    {
        // Initialize the Input Actions
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        // Enable the Input Actions
        inputActions.Enable();

        // Bind the DrawCard action to the method
        inputActions.Player.DrawCard.performed += OnDrawCard;
    }

    private void OnDisable()
    {
        // Unbind the action and disable the Input Actions
        inputActions.Player.DrawCard.performed -= OnDrawCard;
        inputActions.Disable(); // Correctly disabling the input actions
    }

    private void OnDrawCard(InputAction.CallbackContext context)
    {
        // Trigger the DrawCard method when the action is performed
        DrawCard();
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

    public void DrawCard()
    {
        // Check if the current player has already drawn a card
        if (hasDrawnCard)
        {
            Debug.Log("You can only draw one card per turn!");
            return; // Prevent drawing more than one card
        }

        // Destroy the old card if it exists
        if (currentCardObject != null)
        {
            Destroy(currentCardObject);
        }

        // Check if the deck is empty
        if (deck == null || deck.Count == 0)
        {
            Debug.LogError("Deck is empty! Cannot draw a card.");
            return;
        }

        // Draw a random card without removing it from the deck
        int randomIndex = Random.Range(0, deck.Count);
        Card drawnCard = deck[randomIndex];

        // Add the drawn card to the current player's hand
        Player currentPlayer = players[currentPlayerIndex];
        currentPlayer.AddCardToHand(drawnCard);

        Debug.Log($"{currentPlayer.playerName} drew a card: {drawnCard.cardName}");

        // Instantiate the new card and set it as the current card
        currentCardObject = Instantiate(cardPrefab, cardSpawnPoint.position, Quaternion.identity, cardSpawnPoint.parent);
        CardDisplay cardDisplay = currentCardObject.GetComponent<CardDisplay>();

        // Create a copy of the card for display
        Card cardCopy = ScriptableObject.CreateInstance<Card>();
        cardCopy.cardName = drawnCard.cardName;
        cardCopy.cardImage = drawnCard.cardImage;
        cardCopy.description = drawnCard.description;

        cardDisplay.SetCard(cardCopy);

        // Mark that the player has drawn a card
        hasDrawnCard = true;
    }

    private List<Player> players = new List<Player>();
    private int currentPlayerIndex = 0;

    private void Start()
    {
        players.Add(new Player("Player 1"));
        players.Add(new Player("Player 2"));
        players.Add(new Player("Player 3"));
        players.Add(new Player("Player 4"));

        Debug.Log("Game initialized with 4 players.");
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