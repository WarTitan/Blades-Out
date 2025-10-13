using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(menuName = "BladesOut/Card Database", fileName = "CardDatabase")]
public class CardDatabase : ScriptableObject
{
    public List<CardDefinition> cards = new List<CardDefinition>();


    private Dictionary<int, CardDefinition> lookup;


    public void InitIfNeeded()
    {
        if (lookup != null) return;
        lookup = new Dictionary<int, CardDefinition>();
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c != null) lookup[c.id] = c;
        }
    }


    public CardDefinition Get(int id)
    {
        InitIfNeeded();
        CardDefinition def;
        lookup.TryGetValue(id, out def);
        return def;
    }


    public int GetRandomId()
    {
        if (cards == null || cards.Count == 0) return -1;
        int idx = Random.Range(0, cards.Count);
        return cards[idx] != null ? cards[idx].id : -1;
    }
}