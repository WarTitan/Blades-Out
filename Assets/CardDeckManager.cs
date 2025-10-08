using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardDeckManager : MonoBehaviour
{
    public List<string> deck; // List of card names
    public Transform cardSpawnPoint; // Spawn point for cards
    public GameObject cardPrefab; // Card prefab to instantiate

    private PlayerInput playerInput; // Reference to PlayerInput component

    private void Awake()
    {
        // Get the PlayerInput component attached to this GameObject
        playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        // Subscribe to the SpawnCardAction
        playerInput.actions["SpawnCardAction"].performed += OnSpacePressed;
    }

    private void OnDisable()
    {
        // Unsubscribe from the SpawnCardAction
        playerInput.actions["SpawnCardAction"].performed -= OnSpacePressed;
    }

    private void OnSpacePressed(InputAction.CallbackContext context)
    {
        // Call the method to spawn a random card
        SpawnRandomCard();
    }

    public void SpawnRandomCard()
    {
        if (deck.Count == 0)
        {
            Debug.LogWarning("Deck is empty!");
            return;
        }

        // Pick a random card from the deck
        int randomIndex = Random.Range(0, deck.Count);
        string cardName = deck[randomIndex];

        // Instantiate the card prefab at the spawn point
        GameObject newCard = Instantiate(cardPrefab, cardSpawnPoint.position, Quaternion.identity);
        newCard.name = cardName;

        Debug.Log($"Spawned card: {cardName}");
    }
}