// =============================================
// File: Card3DAdapter.cs
// =============================================
using UnityEngine;
using TMPro;

public class Card3DAdapter : MonoBehaviour
{
    [Header("References")]
    public MeshRenderer frontRenderer;
    public TextMeshPro cardNameText;
    public TextMeshPro cardDescriptionText;

    private CardDatabase database;
    private int cardId = -1;
    private int level = 1;

    public void Bind(int id, int lvl, CardDatabase db)
    {
        database = db;
        cardId = id;
        level = Mathf.Max(1, lvl);
        Apply();
    }

    public void Apply()
    {
        if (database == null) return;
        var def = database.Get(cardId);
        if (def == null) return;

        if (cardNameText != null) cardNameText.text = def.cardName;

        string desc = def.description;
        var tier = def.GetTier(level);
        if (!string.IsNullOrEmpty(tier.effectText)) desc = tier.effectText;
        if (cardDescriptionText != null) cardDescriptionText.text = desc;

        if (def.image != null && frontRenderer != null)
        {
            Texture2D tex = def.image.texture;
            if (tex != null) frontRenderer.material.mainTexture = tex;
        }
    }
}
