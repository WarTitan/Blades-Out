using UnityEngine;
using System.Collections.Generic;

public class Player
{
    // === Basic Player Data ===
    public string playerName;
    public int playerIndex;

    // === Health System ===
    public int maxHearts = 10;
    public int currentHearts = 10;
    public HeartBar heartBar;

    // === Card System ===
    public List<Card> hand = new List<Card>();

    // === Camera Reference (for heart bar follow) ===
    public Camera playerCamera;

    // --- Constructors ---
    public Player(string name)
    {
        playerName = name;
    }

    // --- Card Management ---
    public void AddCardToHand(Card card)
    {
        hand.Add(card);
    }

    // --- Health Management ---
    public void TakeDamage(int amount)
    {
        currentHearts -= amount;
        currentHearts = Mathf.Clamp(currentHearts, 0, maxHearts);

        if (heartBar != null)
        {
            heartBar.SetHearts(currentHearts, maxHearts);
        }
    }

    public void Heal(int amount)
    {
        currentHearts += amount;
        currentHearts = Mathf.Clamp(currentHearts, 0, maxHearts);

        if (heartBar != null)
        {
            heartBar.SetHearts(currentHearts, maxHearts);
        }
    }
}
