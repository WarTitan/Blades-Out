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
    [SerializeField] private int startingGold = 5;     // used if TurnManager does not override
    [SerializeField] private int startingHandSize = 4; // used if TurnManager does not override

    [Header("Runtime (synced)")]
    [SyncVar] public int hp;
    [SyncVar] public int armor;
    [SyncVar] public int gold;
    [SyncVar] public int seatIndex = -1;

    // Deck/hand/discard represented as parallel lists: ids and levels.
    public readonly SyncList<int> deckIds = new SyncList<int>();
    public readonly SyncList<byte> deckLvls = new SyncList<byte>();
    public readonly SyncList<int> handIds = new SyncList<int>();
    public readonly SyncList<byte> handLvls = new SyncList<byte>();
    public readonly SyncList<int> discardIds = new SyncList<int>();
    public readonly SyncList<byte> discardLvls = new SyncList<byte>();

    [HideInInspector] public TurnManager turnManager; // assigned by server

    public int MaxHP { get { return maxHP; } }
    public int MaxArmor { get { return maxArmor; } }

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

        Debug.Log("[PlayerState] Init begin netId=" + netId +
                  " deckSize=" + deckIds.Count +
                  " startHand=" + startHand +
                  " hp=" + hp + " armor=" + armor + " gold=" + gold);

        Server_Draw(startHand);

        Debug.Log("[PlayerState] Init end netId=" + netId +
                  " hand=" + handIds.Count +
                  " deck=" + deckIds.Count +
                  " discard=" + discardIds.Count);
    }

    [Server]
    public void Server_Draw(int count)
    {
        Debug.Log("[PlayerState] Draw request count=" + count +
                  " BEFORE hand=" + handIds.Count +
                  " deck=" + deckIds.Count +
                  " discard=" + discardIds.Count +
                  " netId=" + netId);

        for (int i = 0; i < count; i++)
        {
            if (deckIds.Count == 0) Server_ShuffleDiscardIntoDeck();
            if (deckIds.Count == 0)
            {
                Debug.LogWarning("[PlayerState] Draw aborted, deck empty after shuffle. netId=" + netId);
                return; // still empty
            }

            int idx = deckIds.Count - 1; // take from end
            int id = deckIds[idx];
            byte lvl = deckLvls[idx];
            deckIds.RemoveAt(idx);
            deckLvls.RemoveAt(idx);
            handIds.Add(id);
            handLvls.Add(lvl);

            Debug.Log("[PlayerState] Drew card id=" + id + " lvl=" + lvl +
                      " NOW hand=" + handIds.Count +
                      " deck=" + deckIds.Count +
                      " netId=" + netId);
        }
    }

    [Server]
    public void Server_DiscardFromHand(int handIndex)
    {
        if (handIndex < 0 || handIndex >= handIds.Count)
        {
            Debug.LogWarning("[PlayerState] Discard invalid index " + handIndex + " netId=" + netId);
            return;
        }

        discardIds.Add(handIds[handIndex]);
        discardLvls.Add(handLvls[handIndex]);
        handIds.RemoveAt(handIndex);
        handLvls.RemoveAt(handIndex);

        Debug.Log("[PlayerState] Discarded from hand index=" + handIndex +
                  " NOW hand=" + handIds.Count +
                  " discard=" + discardIds.Count +
                  " netId=" + netId);
    }

    [Server]
    public void Server_ShuffleDiscardIntoDeck()
    {
        // move discard to deck
        int moved = discardIds.Count;

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

        Debug.Log("[PlayerState] Shuffled discard into deck moved=" + moved +
                  " deck now=" + deckIds.Count +
                  " netId=" + netId);
    }

    [Command]
    public void CmdEndTurn()
    {
        Debug.Log("[PlayerState] CmdEndTurn from netId=" + netId);
        if (turnManager != null)
            turnManager.Server_EndTurn(this);
        else
            Debug.LogWarning("[PlayerState] CmdEndTurn: turnManager is null. netId=" + netId);
    }

    [Command]
    public void CmdUpgradeCard(int handIndex)
    {
        Debug.Log("[PlayerState] CmdUpgradeCard idx=" + handIndex + " from netId=" + netId);
        if (turnManager != null)
            turnManager.Server_UpgradeCard(this, handIndex);
        else
            Debug.LogWarning("[PlayerState] CmdUpgradeCard: turnManager is null. netId=" + netId);
    }

    // Start game request (manual start button / hotkey)
    [Command]
    public void CmdRequestStartGame()
    {
        Debug.Log("[PlayerState] CmdRequestStartGame from netId=" + netId);
        if (turnManager != null)
            turnManager.Server_AttemptStartGame();
        else
            Debug.LogWarning("[PlayerState] CmdRequestStartGame: turnManager is null. netId=" + netId);
    }

    // Optional helper for taking damage (armor first then hp)
    [Server]
    public void Server_ApplyDamage(int dmg)
    {
        int remaining = Mathf.Max(0, dmg);
        int armorBefore = armor;
        int hpBefore = hp;

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

        Debug.Log("[PlayerState] ApplyDamage dmg=" + dmg +
                  " armor " + armorBefore + "->" + armor +
                  " hp " + hpBefore + "->" + hp +
                  " netId=" + netId);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Prefer a direct Instance, otherwise find a TurnManager in scene.
        var tm = TurnManager.Instance;
        if (tm == null)
        {
#if UNITY_2023_1_OR_NEWER
            tm = Object.FindFirstObjectByType<TurnManager>();
            if (tm == null) tm = Object.FindAnyObjectByType<TurnManager>();
#else
            tm = Object.FindObjectOfType<TurnManager>();
#endif
        }

        if (tm != null)
        {
            tm.RegisterPlayer(this);
            Debug.Log("[PlayerState] OnStartServer registered with TurnManager. netId=" + netId);
        }
        else
        {
            Debug.LogWarning("[PlayerState] OnStartServer could not find TurnManager. netId=" + netId);
        }
    }
}
