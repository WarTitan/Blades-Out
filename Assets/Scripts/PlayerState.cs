using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerState : NetworkBehaviour
{
    [Header("Refs")]
    public CardDatabase database;

    [Header("Stats")]
    [SyncVar] public int hp = 5;
    [SyncVar] public int armor = 5;
    [SyncVar] public int gold = 0;

    // Max stats (so StatusBarsForPlayer can read them)
    [SyncVar] public int maxHP = 5;
    [SyncVar] public int maxArmor = 5;

    // Properties used by StatusBarsForPlayer (exact names expected)
    public int MaxHP => maxHP;
    public int MaxArmor => maxArmor;

    [Header("Seat/Turn")]
    [SyncVar] public int seatIndex = -1;
    [HideInInspector] public TurnManager turnManager;

    // === CARDS ===
    // Hand row
    public readonly SyncList<int> handIds = new SyncList<int>();
    public readonly SyncList<byte> handLvls = new SyncList<byte>();

    // Set row (face-up for testing)
    public readonly SyncList<int> setIds = new SyncList<int>();
    public readonly SyncList<byte> setLvls = new SyncList<byte>();

    // Simple server-side status list (poison etc.)
    private readonly List<StatusData> statuses = new List<StatusData>();
    private struct StatusData { public StatusType type; public int magnitude; public int turns; }
    private enum StatusType { Poison }

    #region Lifecycle / Init

    public override void OnStartServer()
    {
        base.OnStartServer();
        TurnManager.Instance?.RegisterPlayer(this);
    }

    [Server]
    public void Server_Init(CardDatabase db, int deckSize, int startGold, int startHand)
    {
        database = db;
        gold = startGold;

        // current stats = max at start
        hp = maxHP;
        armor = maxArmor;

        handIds.Clear(); handLvls.Clear();
        setIds.Clear(); setLvls.Clear();

        // very simple start draw
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

    [Server]
    public void Server_Draw(int count)
    {
        if (database == null) return;
        for (int i = 0; i < count; i++)
        {
            int id = database.GetRandomId();
            if (id >= 0) { handIds.Add(id); handLvls.Add(1); }
        }
    }

    #endregion

    #region Commands from Client

    [Command]
    public void CmdEndTurn() => turnManager?.Server_EndTurn(this);

    [Command]
    public void CmdUpgradeCard(int handIndex)
    {
        TurnManager.Instance?.Server_UpgradeCard(this, handIndex);
    }

    // Start game request used by StartGameUI & KeyboardHotkeys
    [Command]
    public void CmdRequestStartGame()
    {
        TurnManager.Instance?.Server_AttemptStartGame();
    }

    // Play an INSTANT card from hand -> resolves immediately on server
    [Command]
    public void CmdPlayInstant(int handIndex, uint targetNetId)
    {
        if (!IsYourTurn()) return;
        if (!ValidHandIndex(handIndex)) return;

        var def = database?.Get(handIds[handIndex]);
        int lvl = handLvls[handIndex];

        var target = FindPlayerByNetId(targetNetId);

        // remove from hand first (so sync rows update fast)
        RemoveHandAt(handIndex);

        CardEffectResolver.PlayInstant(def, lvl, this, target);
    }

    // ======= SET CARD: hand -> set (robust; allows test without authority issues) =======
    [Command(requiresAuthority = false)]
    public void CmdSetCard(int handIndex)
    {
        // Identify who sent this command
        var senderIdentity = connectionToClient?.identity;
        var senderPS = senderIdentity ? senderIdentity.GetComponent<PlayerState>() : null;

        if (senderPS == null)
        {
            Debug.LogWarning("[PlayerState] CmdSetCard: sender has no PlayerState");
            return;
        }

        // Security: only allow a player to set THEIR OWN card
        if (senderPS != this)
        {
            Debug.LogWarning($"[PlayerState] CmdSetCard: mismatched target. Sender netId={senderPS.netId}, target netId={netId}");
            return;
        }

        // For testing we allow setting ANY card at ANY time (no turn gate / style gate)
        Server_SetCard_ByIndex(handIndex);
    }

    [Server]
    private void Server_SetCard_ByIndex(int handIndex)
    {
        // Validate index
        if (handIndex < 0 || handIndex >= handIds.Count || handIndex >= handLvls.Count)
        {
            Debug.LogWarning($"[PlayerState] Server_SetCard_ByIndex: invalid handIndex {handIndex} (hand count={handIds.Count})");
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

        Debug.Log($"[PlayerState] Server_SetCard_ByIndex: moved id={id} lvl={lvl} handIndex={handIndex} -> SET (player netId={netId}).");
        // SyncLists will notify clients; visualizers will rebuild automatically
    }

    #endregion

    #region Server Helpers

    [Server]
    private bool IsYourTurn()
    {
        return (TurnManager.Instance != null &&
                TurnManager.Instance.IsPlayersTurn(this));
    }

    [Server]
    private bool ValidHandIndex(int i)
    {
        return i >= 0 && i < handIds.Count && i < handLvls.Count;
    }

    [Server]
    private PlayerState FindPlayerByNetId(uint netId)
    {
        if (netId == 0) return null;
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity nid))
            return nid != null ? nid.GetComponent<PlayerState>() : null;
        return null;
    }

    [Server]
    private void RemoveHandAt(int index)
    {
        handIds.RemoveAt(index);
        handLvls.RemoveAt(index);
    }

    [Server]
    public void Server_ConsumeSetAt(int index)
    {
        if (index < 0 || index >= setIds.Count) return;
        setIds.RemoveAt(index);
        setLvls.RemoveAt(index);
    }

    #endregion

    #region Combat / Status

    // Central damage entry: applies armor, then HP; lets set-row react first
    [Server]
    public void Server_ApplyDamage(PlayerState attacker, int dmg)
    {
        if (dmg <= 0) return;

        // Defender’s set row can react/mitigate/reflect (and may consume a set card)
        CardEffectResolver.TryReactOnIncomingHit(this, attacker, ref dmg);
        if (dmg <= 0) return;

        // Armor soaks first
        int soak = Mathf.Min(armor, dmg);
        armor -= soak;
        dmg -= soak;

        // HP
        if (dmg > 0)
        {
            hp -= dmg;
            if (hp < 0) hp = 0;
            RpcHitFlash(dmg);
            if (hp == 0)
            {
                // TODO: KO / death handling, revive hooks, etc.
            }
        }
    }

    [Server]
    public void Server_Heal(int amt)
    {
        if (amt <= 0) return;
        hp = Mathf.Min(maxHP, hp + amt);
        RpcHealed(amt);
    }

    [Server]
    public void Server_AddPoison(int perTick, int turns)
    {
        if (perTick <= 0 || turns <= 0) return;
        statuses.Add(new StatusData { type = StatusType.Poison, magnitude = perTick, turns = turns });
        RpcGotStatus("Poison", perTick, turns);
    }

    // Called at start of THIS player's turn (from TurnManager)
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

    #endregion

    #region Client FX

    [ClientRpc] private void RpcHitFlash(int dmg) { /* TODO: hit VFX/SFX */ }
    [ClientRpc] private void RpcHealed(int amt) { /* TODO: heal VFX/SFX */ }
    [ClientRpc] private void RpcGotStatus(string name, int mag, int t) { /* TODO: status UI */ }

    #endregion
}
