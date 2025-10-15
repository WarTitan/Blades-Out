using UnityEngine;
using Mirror;
using System.Collections.Generic;

// PlayerState for Blades Out
// - Only SetReaction cards can be moved to the Set row
// - Only one action at a time (server action lock to prevent multi-plays/sets)
// - Integrates with TurnManager, CardDatabase, CardEffectResolver
// - ASCII only

public class PlayerState : NetworkBehaviour
{
    [Header("Refs")]
    public CardDatabase database;

    [Header("Stats")]
    [SyncVar] public int hp = 5;
    [SyncVar] public int armor = 5;
    [SyncVar] public int gold = 0;

    // Exposed max values (UI may read these)
    [SyncVar] public int maxHP = 5;
    [SyncVar] public int maxArmor = 5;

    public int MaxHP => maxHP;
    public int MaxArmor => maxArmor;

    [Header("Seat / Turn")]
    [SyncVar] public int seatIndex = -1;
    [HideInInspector] public TurnManager turnManager;

    // --- CARDS ---
    // Hand row
    public readonly SyncList<int> handIds = new SyncList<int>();
    public readonly SyncList<byte> handLvls = new SyncList<byte>();

    // Set row
    public readonly SyncList<int> setIds = new SyncList<int>();
    public readonly SyncList<byte> setLvls = new SyncList<byte>();

    // Prevent overlapping actions in the same moment (server-side)
    private bool actionLock = false;

    // Minimal status system (example: Poison)
    private readonly List<StatusData> statuses = new List<StatusData>();
    private struct StatusData { public StatusType type; public int magnitude; public int turns; }
    private enum StatusType { Poison }

    // ---------- Lifecycle ----------
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterPlayer(this);
            turnManager = TurnManager.Instance;
        }
    }

    // ---------- Setup / init ----------
    [Server]
    public void Server_Init(CardDatabase db, int deckSize, int startGold, int startHand)
    {
        database = db;
        gold = startGold;

        // Reset current stats to max
        hp = maxHP;
        armor = maxArmor;

        handIds.Clear(); handLvls.Clear();
        setIds.Clear(); setLvls.Clear();

        // Simple opening draw
        for (int i = 0; i < startHand; i++)
        {
            int id = database != null ? database.GetRandomId() : -1;
            if (id >= 0)
            {
                handIds.Add(id);
                handLvls.Add(1);
            }
        }
    }

    // Draw N cards from the database (random for now)
    [Server]
    public void Server_Draw(int count)
    {
        if (count <= 0 || database == null) return;
        for (int i = 0; i < count; i++)
        {
            int id = database.GetRandomId();
            if (id >= 0)
            {
                handIds.Add(id);
                handLvls.Add(1);
            }
        }
    }

    // ---------- Turn / commands ----------

    [Command]
    public void CmdRequestStartGame()
    {
        TurnManager.Instance?.Server_AttemptStartGame();
    }

    [Command]
    public void CmdEndTurn()
    {
        TurnManager.Instance?.Server_EndTurn(this);
    }

    [Command]
    public void CmdUpgradeCard(int handIndex)
    {
        TurnManager.Instance?.Server_UpgradeCard(this, handIndex);
    }

    // Play an instant from hand, resolves immediately
    [Command]
    public void CmdPlayInstant(int handIndex, uint targetNetId)
    {
        if (!IsYourTurn()) return;
        if (!ValidHandIndex(handIndex)) return;
        if (actionLock) return;

        var def = database != null ? database.Get(handIds[handIndex]) : null;
        if (def == null) return;

        // Do not allow casting set-only reactions as instants
        if (def.playStyle == CardDefinition.PlayStyle.SetReaction) return;

        var target = FindPlayerByNetId(targetNetId);
        int lvl = handLvls[handIndex];

        actionLock = true;
        try
        {
            // Remove from hand first so SyncLists update immediately
            RemoveHandAt(handIndex);
            CardEffectResolver.PlayInstant(def, lvl, this, target);
        }
        finally { actionLock = false; }
    }

    // Move a hand card to the set row (only SetReaction allowed)
    [Command(requiresAuthority = false)]
    public void CmdSetCard(int handIndex)
    {
        // Only allow the sender to operate on their own PlayerState
        var senderIdentity = connectionToClient != null ? connectionToClient.identity : null;
        var senderPS = senderIdentity ? senderIdentity.GetComponent<PlayerState>() : null;
        if (senderPS == null || senderPS != this) return;

        if (actionLock) return;
        if (!Server_IsSetReactionInHand(handIndex)) return;

        actionLock = true;
        try
        {
            Server_SetCard_ByIndex(handIndex);
        }
        finally { actionLock = false; }
    }

    // ---------- Helpers ----------

    public bool IsYourTurn()
    {
        return (TurnManager.Instance != null && TurnManager.Instance.IsPlayersTurn(this));
    }

    private bool ValidHandIndex(int i)
    {
        return i >= 0 && i < handIds.Count && i < handLvls.Count;
    }

    private PlayerState FindPlayerByNetId(uint netId)
    {
        if (netId == 0) return null;
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity nid))
            return nid != null ? nid.GetComponent<PlayerState>() : null;
        return null;
    }

    private void RemoveHandAt(int index)
    {
        handIds.RemoveAt(index);
        handLvls.RemoveAt(index);
    }

    [Server]
    private bool Server_IsSetReactionInHand(int handIndex)
    {
        if (!ValidHandIndex(handIndex) || database == null) return false;
        var def = database.Get(handIds[handIndex]);
        return def != null && def.playStyle == CardDefinition.PlayStyle.SetReaction;
    }

    [Server]
    private void Server_SetCard_ByIndex(int handIndex)
    {
        if (handIndex < 0 || handIndex >= handIds.Count || handIndex >= handLvls.Count)
        {
            Debug.LogWarning("[PlayerState] Server_SetCard_ByIndex: invalid handIndex " + handIndex + " (hand count=" + handIds.Count + ")");
            return;
        }

        int id = handIds[handIndex];
        byte lvl = handLvls[handIndex];

        // Remove from hand
        handIds.RemoveAt(handIndex);
        handLvls.RemoveAt(handIndex);

        // Add to set
        setIds.Add(id);
        setLvls.Add(lvl);
    }

    [Server]
    public void Server_ConsumeSetAt(int index)
    {
        if (index < 0 || index >= setIds.Count || index >= setLvls.Count) return;
        setIds.RemoveAt(index);
        setLvls.RemoveAt(index);
    }

    // ---------- Damage / healing / statuses ----------

    [Server]
    public void Server_ApplyDamage(PlayerState src, int amount)
    {
        if (amount <= 0) return;

        int incoming = amount;
        // Give defender reactions a chance
        CardEffectResolver.TryReactOnIncomingHit(this, src, ref incoming);

        int left = incoming;
        if (armor > 0)
        {
            int used = Mathf.Min(armor, left);
            armor -= used;
            left -= used;
        }
        if (left > 0)
        {
            hp -= left;
            if (hp < 0) hp = 0;
        }

        RpcHitFlash(incoming);
        // TODO: death / knockout handling if needed
    }

    [Server]
    public void Server_Heal(int amount)
    {
        if (amount <= 0) return;
        hp += amount;
        if (hp > maxHP) hp = maxHP;
        RpcHealed(amount);
    }

    [Server]
    public void Server_AddPoison(int magnitude, int turns)
    {
        statuses.Add(new StatusData { type = StatusType.Poison, magnitude = magnitude, turns = turns });
        RpcGotStatus("Poison", magnitude, turns);
    }

    [Server]
    public void Server_OnTurnStart_ProcessStatuses()
    {
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            var s = statuses[i];
            switch (s.type)
            {
                case StatusType.Poison:
                    Server_ApplyDamage(null, s.magnitude);
                    s.turns -= 1;
                    if (s.turns <= 0) statuses.RemoveAt(i);
                    else statuses[i] = s;
                    break;
            }
        }
    }

    // ---------- Client FX hooks ----------
    [ClientRpc] private void RpcHitFlash(int dmg) { /* TODO: add VFX */ }
    [ClientRpc] private void RpcHealed(int amt) { /* TODO: add VFX */ }
    [ClientRpc] private void RpcGotStatus(string name, int mag, int t) { /* TODO: add UI */ }
}
