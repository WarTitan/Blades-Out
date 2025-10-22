using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "BladesOut/Card Database", fileName = "CardDatabase")]
public class CardDatabase : ScriptableObject
{
    // Runtime singleton so clients (incl. clone) can always resolve a DB.
    public static CardDatabase Active { get; private set; }

    [Tooltip("All card definitions in the game.")]
    public List<CardDefinition> cards = new List<CardDefinition>();

    private Dictionary<int, CardDefinition> lookup;

    // Keep original API
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
        return def; // null if not found
    }

    public int GetRandomId()
    {
        if (cards == null || cards.Count == 0) return -1;
        int idx = Random.Range(0, cards.Count);
        return cards[idx] != null ? cards[idx].id : -1;
    }

    // -------- New helpers (optional to use) --------

    public void MakeActive(bool overwrite = true)
    {
        if (Active != null && Active != this && !overwrite) return;
        Active = this;
        InitIfNeeded();
    }

    public static bool TrySetActive(CardDatabase db, bool overwrite = true)
    {
        if (db == null) return false;
        if (Active != null && Active != db && !overwrite) return false;
        Active = db;
        Active.InitIfNeeded();
        return true;
    }

    public static CardDatabase FindOrLoadActive()
    {
        if (Active != null) return Active;

        // 1) Already-loaded assets
        var loaded = Resources.FindObjectsOfTypeAll<CardDatabase>();
        if (loaded != null && loaded.Length > 0)
        {
            CardDatabase pick = null;
            foreach (var x in loaded)
            {
                if (x == null) continue;
                if (pick == null) pick = x;
                if (x.name == "CardDatabase" || x.name.EndsWith("(Default)"))
                {
                    pick = x;
                    break;
                }
            }
            pick.MakeActive();
            return Active;
        }

        // 2) Try Resources.Load
        string[] paths = { "CardDatabase", "Cards/CardDatabase", "CardDatabase_Default" };
        foreach (var p in paths)
        {
            var res = Resources.Load<CardDatabase>(p);
            if (res != null)
            {
                res.MakeActive();
                return Active;
            }
        }

        // 3) Any PlayerState.database in scene
#if UNITY_2023_1_OR_NEWER
        var players = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
        var players = Object.FindObjectsOfType<PlayerState>();
#endif
        foreach (var ps in players)
        {
            if (ps != null && ps.database != null)
            {
                ps.database.MakeActive();
                return Active;
            }
        }

        // 4) Last resort: empty runtime DB (prevents null refs; gameplay still runs, but defs missing)
        Active = CreateInstance<CardDatabase>();
        Active.cards = new List<CardDefinition>();
        Active.InitIfNeeded();
        Debug.LogWarning("[CardDatabase] No database asset found. Created an empty runtime DB. Place your CardDatabase asset under a 'Resources' folder or call MakeActive() at startup.");
        return Active;
    }

    public static CardDefinition SGet(int id)
    {
        var db = Active ?? FindOrLoadActive();
        return db.Get(id);
    }

    public static int SGetRandomId()
    {
        var db = Active ?? FindOrLoadActive();
        return db.GetRandomId();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Force rebuild while editing
        lookup = null;
    }
#endif
}
