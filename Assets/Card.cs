using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Cards/New 3D Card")]
public class Card : ScriptableObject
{
    public string cardName;
    [TextArea]
    public string description;

    // Artwork sprite for the card's image
    public Sprite artwork;
}
