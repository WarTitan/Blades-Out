// 9/11/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add this namespace for TextMeshPro

public class CardDisplay : MonoBehaviour
{
    public TextMeshProUGUI cardNameText; // UI Text for the card's name
    public TextMeshProUGUI descriptionText; // UI Text for the card's description
    public Image cardImage; // UI Image for the card's artwork

    public void SetCard(Card card)
    {
        cardNameText.text = card.cardName;
        descriptionText.text = card.description;
        cardImage.sprite = card.cardImage;
    }
}