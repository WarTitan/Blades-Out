// 9/12/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;

public class Player
{
    public string playerName; // The name of the player
    public List<Card> hand; // The player's hand of cards

    public Player(string name)
    {
        playerName = name;
        hand = new List<Card>(); // Initialize the hand as an empty list
    }

    // Add a card to the player's hand
    public void AddCardToHand(Card card)
    {
        hand.Add(card);
    }

    // Remove a card from the player's hand
    public void RemoveCardFromHand(Card card)
    {
        hand.Remove(card);
    }

    // Get the number of cards in the player's hand
    public int GetHandSize()
    {
        return hand.Count;
    }
}