using UnityEngine;
using Mirror;
using System.Collections.Generic;

// PlayerState for Blades Out
// - Hearthstone-style chips: +1 max (cap via TurnManager) & refill at start of your turn
// - Chips spent when casting instants and when setting reaction cards
// - Global upgrades per cardId (affects all copies + future draws)
// - Turn gating + debounce + server action lock
// - MaxHP/MaxArmor properties for UI scripts

public class PlayerState : NetworkBehaviour
{
    [Header("Refs")]
    public CardDatabase database;

    [Header("Stats")]
    [SyncVar] public int hp = 5;
    [SyncVar] public int armor = 5;
    [SyncVar] public int gold = 0;

    [SyncVar] public int maxHP = 5;
    [SyncVar] public int maxArmor = 5;

    // Properties expected by some UI (e.g., StatusBarsForPlayer)
    public int MaxHP => maxHP;
    public int MaxArmor => maxArmor;

    [Header("Chips (Hearthstone style)")]
    // Hooks so UI can refresh instantly
    [SyncVar(hook = nameof(OnChipsChanged))] public int chips = 0;     // current
    [SyncVar(hook = nameof(OnMaxChipsChanged))] public int maxChips = 0;  // increases +1 each of your turns, capped by TurnManager

    // Simple events for UI
    public event System.Action<PlayerState> ChipsChanged;
    public event System.Action<PlayerState> MaxChipsChanged;
    void OnChipsChanged(int oldV, int newV) { ChipsChanged?.Invoke(this); }
    void OnMaxChipsChanged(int oldV, int newV) { MaxChipsChanged?.Invoke(this); }

    [Header("Seat / Turn")]
    [SyncVar] public int seatIndex = -1;
    [HideInInspector] public TurnManager turnManager;

    // Rows
    public readonly SyncList<int> handIds = new SyncList<int>();
    public readonly SyncList<byte> handLvls = new SyncList<byte>();
    public readonly SyncList<int> setIds = new SyncList<int>();
    public readonly SyncList<byte> setLvls = new SyncList<byte>();

    // Global upgrades per cardId (affects all copies + future draws)
    public readonly SyncDictionary<int, byte> upgradeLevels = new SyncDictionary<int, byte>();

    // Prevent overlapping actions
    private bool actionLock = false;

    // Debounce to avoid accidental double actions
    private float lastActionTime = -999f;
    [SerializeField] private float minActionInterval = 0.10f; // seconds

    // Minimal status demo
    private readonly List<StatusData> statuses = new List<StatusData>();
    private struct StatusData { public StatusType type; public int magnitude; public int turns; }
    private enum StatusType { Poison }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterPlayer(this);
            turnManager = TurnManager.Instance;
        }
    }

    // ---------- Game init / draw ----------

    [Server]
    public void Server_Init(CardDatabase db, int deckSize, int startGold, int startHand)
    {
        database = db;
        gold = startGold;

        hp = maxHP;
        armor = maxArmor;

        handIds.Clear(); handLvls.Clear();
        setIds.Clear(); setLvls.Clear();

        // Hearthstone flow: start at 0/0; first turn bumps to 1/1 and refills
        chips = 0;
        maxChips = 0;
        // If you want upgrades to reset each match, uncomment:
        // upgradeLevels.Clear();

        for (int i = 0; i < startHand; i++)
        {
            int id = database != null ? database.GetRandomId() : -1;
            if (id >= 0)
            {
                handIds.Add(id);
                handLvls.Add(GetLevelForNewInstance(id)); // use upgraded level if present
            }
        }
    }

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
                handLvls.Add(GetLevelForNewInstance(id)); // future draws use upgraded level if present
            }
        }
    }

    // ---------- Chips helpers ----------

    [Server] public void Server_RefillChipsToMax() { chips = maxChips; }

    // Called by TurnManager at the start of your turn
    [Server]
    public void Server_IncreaseMaxChipsAndRefill(int cap, int amount = 1)
    {
        int newMax = Mathf.Min(cap, maxChips + Mathf.Max(1, amount));
        maxChips = newMax;
        chips = maxChips; // refill to full like Hearthstone
    }

    [Server]
    public bool Server_TrySpendChips(int amount)
    {
        if (amount <= 0) return true;
        if (chips < amount) return false;
        chips -= amount;
        return true;
    }

    [TargetRpc]
    void TargetActionDenied(NetworkConnection target, string reason)
    {
        Debug.LogWarning(reason);
    }

    // ---------- Commands ----------

    [Command] public void CmdRequestStartGame() => TurnManager.Instance?.Server_AttemptStartGame();
    [Command] public void CmdEndTurn() => TurnManager.Instance?.Server_EndTurn(this);
    [Command] public void CmdUpgradeCard(int handIndex) => TurnManager.Instance?.Server_UpgradeCard(this, handIndex);

    [Command]
    public void CmdPlayInstant(int handIndex, uint targetNetId)
    {
        if (!IsYourTurn()) return;
        if (!ValidHandIndex(handIndex)) return;

        // debounce + lock
        if (Time.time - lastActionTime < minActionInterval) return;
        lastActionTime = Time.time;
        if (actionLock) return;

        var def = database != null ? database.Get(handIds[handIndex]) : null;
        if (def == null) return;
        if (def.playStyle == CardDefinition.PlayStyle.SetReaction) return; // cannot cast set-only

        // poker chip cost (tier override -> base)
        int lvl = handLvls[handIndex];
        var tier = def.GetTier(lvl);
        int cost = tier.castChipCost > 0 ? tier.castChipCost : Mathf.Max(0, def.chipCost);
        if (!Server_TrySpendChips(cost))
        {
            TargetActionDenied(connectionToClient, "[Cast] Not enough chips (" + cost + " needed).");
            return;
        }

        var target = FindPlayerByNetId(targetNetId);

        actionLock = true;
        try
        {
            RemoveHandAt(handIndex); // fast UI sync
            CardEffectResolver.PlayInstant(def, lvl, this, target);
        }
        finally { actionLock = false; }
    }

    // Hand -> Set (must be your turn, must be SetReaction), charge chips when setting
    [Command(requiresAuthority = false)]
    public void CmdSetCard(int handIndex)
    {
        var senderIdentity = connectionToClient != null ? connectionToClient.identity : null;
        var senderPS = senderIdentity ? senderIdentity.GetComponent<PlayerState>() : null;
        if (senderPS == null || senderPS != this) return;

        if (!IsYourTurn()) return;

        // debounce before lock
        if (Time.time - lastActionTime < minActionInterval) return;
        lastActionTime = Time.time;

        if (actionLock) return;
        if (!Server_IsSetReactionInHand(handIndex)) return;

        // chip cost to SET the trap (tier override -> base)
        int id = handIds[handIndex];
        var def = database.Get(id);
        int lvl = handLvls[handIndex];
        var tier = def.GetTier(lvl);
        int cost = tier.castChipCost > 0 ? tier.castChipCost : Mathf.Max(0, def.chipCost);
        if (!Server_TrySpendChips(cost))
        {
            TargetActionDenied(connectionToClient, "[Set] Not enough chips (" + cost + " needed).");
            return;
        }

        actionLock = true;
        try { Server_SetCard_ByIndex(handIndex); }
        finally { actionLock = false; }
    }

    // ---------- Helpers ----------

    public bool IsYourTurn()
    {
        return (TurnManager.Instance != null && TurnManager.Instance.IsPlayersTurn(this));
    }

    private bool ValidHandIndex(int i) => i >= 0 && i < handIds.Count && i < handLvls.Count;

    private PlayerState FindPlayerByNetId(uint netId)
    {
        if (netId == 0) return null;
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity nid))
            return nid ? nid.GetComponent<PlayerState>() : null;
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
        if (handIndex < 0 || handIndex >= handIds.Count || handIndex >= handLvls.Count) return;

        int id = handIds[handIndex];
        byte lvl = handLvls[handIndex];

        handIds.RemoveAt(handIndex);
        handLvls.RemoveAt(handIndex);

        setIds.Add(id);
        setLvls.Add(lvl);
    }

    // Used by CardEffectResolver to consume a set reaction after it triggers.
    [Server]
    public void Server_ConsumeSetAt(int index)
    {
        if (index < 0 || index >= setIds.Count || index >= setLvls.Count) return;
        setIds.RemoveAt(index);
        setLvls.RemoveAt(index);
    }

    // ===== Global-upgrade helpers =====

    // effective level for a hand card considering the upgrade map
    [Server]
    public int Server_GetEffectiveLevelForHandIndex(int handIndex)
    {
        if (!ValidHandIndex(handIndex)) return 1;
        int id = handIds[handIndex];
        if (upgradeLevels.TryGetValue(id, out byte lvl)) return lvl;
        return handLvls[handIndex];
    }

    // propagate current upgraded level to all copies in hand and set rows
    [Server]
    public void Server_PropagateUpgradeToAllCopies(int cardId)
    {
        if (!upgradeLevels.TryGetValue(cardId, out byte lvl)) return;

        // Hand
        for (int i = 0; i < handIds.Count && i < handLvls.Count; i++)
            if (handIds[i] == cardId) handLvls[i] = lvl;

        // Set row
        for (int i = 0; i < setIds.Count && i < setLvls.Count; i++)
            if (setIds[i] == cardId) setLvls[i] = lvl;
    }

    // level to assign when adding a new instance of this cardId to the hand
    [Server]
    private byte GetLevelForNewInstance(int cardId)
    {
        return upgradeLevels.TryGetValue(cardId, out byte lvl) ? lvl : (byte)1;
    }

    // ---------- Combat / Status ----------

    [Server]
    public void Server_ApplyDamage(PlayerState src, int amount)
    {
        if (amount <= 0) return;

        int incoming = amount;
        CardEffectResolver.TryReactOnIncomingHit(this, src, ref incoming);

        int left = incoming;
        if (armor > 0)
        {
            int used = Mathf.Min(armor, left);
            armor -= used;
            left -= used;
        }
        if (left > 0) { hp = Mathf.Max(0, hp - left); }

        RpcHitFlash(incoming);
    }

    [Server]
    public void Server_Heal(int amount)
    {
        if (amount > 0)
        {
            hp = Mathf.Min(maxHP, hp + amount);
            RpcHealed(amount);
        }
    }

    [Server]
    public void Server_AddPoison(int mag, int turns)
    {
        statuses.Add(new StatusData { type = StatusType.Poison, magnitude = mag, turns = turns });
        RpcGotStatus("Poison", mag, turns);
    }

    [Server]
    public void Server_OnTurnStart_ProcessStatuses()
    {
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            var s = statuses[i];
            if (s.type == StatusType.Poison)
            {
                Server_ApplyDamage(null, s.magnitude);
                s.turns -= 1;
                if (s.turns <= 0) statuses.RemoveAt(i);
                else statuses[i] = s;
            }
        }
    }

    // Client FX hooks
    [ClientRpc] private void RpcHitFlash(int dmg) { }
    [ClientRpc] private void RpcHealed(int amt) { }
    [ClientRpc] private void RpcGotStatus(string name, int mag, int t) { }
}
