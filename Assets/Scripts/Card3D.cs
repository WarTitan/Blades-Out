using UnityEngine;
using TMPro;

public class Card3D : MonoBehaviour
{
    [Header("References")]
    public MeshRenderer frontRenderer; // Quad for the card front
    public MeshRenderer backRenderer;  // Quad for the card back
    public TextMeshPro cardNameText;
    public TextMeshPro cardDescriptionText;

    [Header("Card Data")]
    public Card cardData; // ScriptableObject that holds the card info

    private void Start()
    {
        if (cardData != null)
            ApplyCardData();
        else
            Debug.LogWarning($"{name} has no CardData assigned.");
    }

    public void ApplyCardData()
    {
        // Update text fields
        if (cardNameText != null)
            cardNameText.text = cardData.cardName;

        if (cardDescriptionText != null)
            cardDescriptionText.text = cardData.description;

        // Update artwork texture
        if (cardData.artwork != null && frontRenderer != null)
        {
            // Convert Sprite to Texture2D
            Texture2D texture = cardData.artwork.texture;
            if (texture != null)
                frontRenderer.material.mainTexture = texture;
        }
        else if (frontRenderer != null)
        {
            // Use default color if no artwork
            frontRenderer.material.color = Color.gray;
        }
    }

    // Optional: flip animation
    public void FlipCard(bool showFront)
    {
        if (frontRenderer != null)
            frontRenderer.gameObject.SetActive(showFront);
        if (backRenderer != null)
            backRenderer.gameObject.SetActive(!showFront);
    }
}
