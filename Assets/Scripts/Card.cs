using UnityEngine;

[System.Serializable]
public class Card
{
    [Header("Card Identification")]
    public int id;   // Unique ID used for networking and lookup

    [Header("Card Data")]
    public string cardName;
    public string description;

    // 🔹 Both names for compatibility (old scripts may use "artwork")
    public Sprite artwork;    // Used by Card3D
    public Sprite cardImage;  // Used by newer logic

    [Header("Stats (optional)")]
    public int attack;
    public int defense;
    public int cost;

    // ✅ Constructor — supports both artwork and cardImage
    public Card(int id, string name, string desc, Sprite image, int atk = 0, int def = 0, int c = 0)
    {
        this.id = id;
        cardName = name;
        description = desc;

        // Assign to both fields to keep them in sync
        artwork = image;
        cardImage = image;

        attack = atk;
        defense = def;
        cost = c;
    }

    // ✅ Clone for duplication
    public Card Clone()
    {
        Card clone = new Card(id, cardName, description, artwork, attack, defense, cost);
        return clone;
    }
}
