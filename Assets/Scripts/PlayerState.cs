// =============================================
// File: PlayerState.cs
// =============================================
using UnityEngine;
using Mirror;

public class PlayerState : NetworkBehaviour
{
    [Header("Config (per-player)")]
    [SerializeField] private int maxHP = 5;
    [SerializeField] private int maxArmor = 5;
    [SerializeField] private int startingGold = 5; // used if TurnManager does not override
    [SerializeField] private int startingHandSize = 4; // used if TurnManager does not override

    [Header("Runtime (synced)")]
    [SyncVar] public int hp;
    [SyncVar] public int armor;
    [SyncVar] public int gold;

    // Deck/hand/discard represented as parallel lists: ids and levels.
    public readonly SyncList<int> deckIds = new SyncList<int>();
    public readonly SyncList<byte> deckLvls = new SyncList<byte>();
    public readonly SyncList<int> handIds = new SyncList<int>();
    public readonly SyncList<byte> handLvls = new SyncList<byte>();
    public readonly SyncList<int> discardIds = new SyncList<int>();
    public readonly SyncList<byte> discardLvls = new SyncList<byte>();

    [HideInInspector] public TurnManager turnManager; // assigned by server

    // Called on server when game starts for this player
    [Server]
    public void Server_Init(CardDatabase db, int defaultDeckSize, int overrideStartGold, int overrideStartHand)
    {
        hp = maxHP;
        armor = maxArmor;
        gold = (overrideStartGold >= 0) ? overrideStartGold : startingGold;

        deckIds.Clear(); deckLvls.Clear();
        handIds.Clear(); handLvls.Clear();
        discardIds.Clear(); discardLvls.Clear();

        int deckSize = Mathf.Max(0, defaultDeckSize);
        for (int i = 0; i < deckSize; i++)
        {
            int id = db != null ? db.GetRandomId() : -1;
            if (id < 0) break;
            deckIds.Add(id);
            deckLvls.Add(1);
        }

        int startHand = (overrideStartHand >= 0) ? overrideStartHand : startingHandSize;
        Server_Draw(startHand);
    }

    [Server]
    public void Server_Draw(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deckIds.Count == 0) Server_ShuffleDiscardIntoDeck();
            if (deckIds.Count == 0) return; // still empty

            int idx = deckIds.Count - 1; // take from end
            int id = deckIds[idx];
            byte lvl = deckLvls[idx];
            deckIds.RemoveAt(idx);
            deckLvls.RemoveAt(idx);
            handIds.Add(id);
            handLvls.Add(lvl);
        }
    }

    [Server]
    public void Server_DiscardFromHand(int handIndex)
    {
        if (handIndex < 0 || handIndex >= handIds.Count) return;
        discardIds.Add(handIds[handIndex]);
        discardLvls.Add(handLvls[handIndex]);
        handIds.RemoveAt(handIndex);
        handLvls.RemoveAt(handIndex);
    }

    [Server]
    public void Server_ShuffleDiscardIntoDeck()
    {
        // move discard to deck
        for (int i = 0; i < discardIds.Count; i++)
        {
            deckIds.Add(discardIds[i]);
            deckLvls.Add(discardLvls[i]);
        }
        discardIds.Clear();
        discardLvls.Clear();

        // simple Fisher-Yates shuffle
        for (int i = 0; i < deckIds.Count; i++)
        {
            int j = Random.Range(i, deckIds.Count);
            int tmpId = deckIds[i]; deckIds[i] = deckIds[j]; deckIds[j] = tmpId;
            byte tmpLvl = deckLvls[i]; deckLvls[i] = deckLvls[j]; deckLvls[j] = tmpLvl;
        }
    }

    [Command]
    public void CmdRequestStartGame()
    {
        if (turnManager != null)
        {
            turnManager.Server_AttemptStartGame();
        }
    }


    [Command]
    public void CmdEndTurn()
    {
        if (turnManager != null)
            turnManager.Server_EndTurn(this);
    }

    [Command]
    public void CmdUpgradeCard(int handIndex)
    {
        if (turnManager != null)
            turnManager.Server_UpgradeCard(this, handIndex);
    }

    // Optional helper for taking damage (armor first then hp)
    [Server]
    public void Server_ApplyDamage(int dmg)
    {
        int remaining = Mathf.Max(0, dmg);
        if (armor > 0)
        {
            int used = Mathf.Min(armor, remaining);
            armor -= used;
            remaining -= used;
        }
        if (remaining > 0)
        {
            hp = Mathf.Max(0, hp - remaining);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        var tm = TurnManager.Instance != null ? TurnManager.Instance : FindObjectOfType<TurnManager>();
        if (tm != null) tm.RegisterPlayer(this);
    }
}
