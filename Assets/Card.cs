// 9/11/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card")]
public class Card : ScriptableObject
{
    public string cardName; // Name of the card
    public string description; // Description of the card effect
    public Sprite cardImage; // Visual representation of the card

    public enum CardEffect { Damage, Heal, Rotate, Block, Manipulate }

    [System.Serializable]
    public class Effect
    {
        public CardEffect effectType; // Type of effect (e.g., Damage, Heal)
        public int effectValue; // Value of the effect (e.g., damage amount, heal amount)
        public int delayTurns; // Number of turns before the effect is applied
        public int durationTurns; // How many turns the effect lasts (0 for instant effects)
    }

    public List<Effect> effects = new List<Effect>(); // List of effects the card has
}