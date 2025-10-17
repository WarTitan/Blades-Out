using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [Header("Game Rules (Inspector)")]
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private int startingGold = 5;
    [SerializeField] private int startingHandSize = 4;
    [SerializeField] private int cardsPerTurn = 1;
    [SerializeField] private int goldPerTurn = 1;
    [SerializeField] private int defaultDeckSize = 12;
    [SerializeField] private CardDatabase database;

    [Header("Start Control")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private bool autoStartOnFull = false;

    [Header("Chips (Hearthstone style)")]
    [SerializeField] private int chipCap = 10; // max maxChips (10)

    [SyncVar] private int currentTurnIndex = -1;
    [SyncVar] private bool gameStarted = false;

    [SyncVar(hook = nameof(OnTurnNetIdChanged))]
    private uint currentTurnNetId = 0;

    private readonly List<PlayerState> turnOrder = new List<PlayerState>();

    void Awake() { Instance = this; }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Instance = this;
        Debug.Log("[TurnManager] OnStartClient netId=" + netId + " isServer=" + isServer);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Instance = this;
        Debug.Log("[TurnManager] OnStartServer netId=" + netId);
    }

    void OnTurnNetIdChanged(uint oldV, uint newV)
    {
        Debug.Log("[TurnManager] currentTurnNetId changed -> " + newV);
    }

    [Server]
    public void RegisterPlayer(PlayerState ps)
    {
        if (turnOrder.Contains(ps)) return;
        turnOrder.Add(ps);
        ps.turnManager = this;

        if (autoStartOnFull && turnOrder.Count >= Mathf.Min(maxPlayers, minPlayersToStart))
            Server_AttemptStartGame();
    }

    [Server]
    public void Server_AttemptStartGame()
    {
        if (gameStarted) return;
        if (turnOrder.Count < minPlayersToStart) return;

        for (int i = 0; i < turnOrder.Count; i++)
            turnOrder[i].Server_Init(database, defaultDeckSize, startingGold, startingHandSize);

        currentTurnIndex = 0;
        gameStarted = true;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    [Server]
    private void Server_StartTurn(PlayerState ps)
    {
        if (!gameStarted || ps == null) return;

        // Hearthstone chips: +1 max (to cap) and refill
        ps.Server_IncreaseMaxChipsAndRefill(chipCap, 1);

        // Start-of-turn processing (statuses)
        ps.Server_OnTurnStart_ProcessStatuses();

        // Draw + gold
        ps.Server_Draw(cardsPerTurn);
        ps.gold += goldPerTurn;

        // Sync whose turn it is (so clients can locally test IsPlayersTurn)
        currentTurnNetId = ps.netId;

        RpcTurnChanged(currentTurnIndex);
        TargetYourTurn(ps.connectionToClient);
    }

    [ClientRpc] private void RpcTurnChanged(int index) { /* optional UI hook */ }
    [TargetRpc] private void TargetYourTurn(NetworkConnection target) { /* optional local UI hook */ }

    [Server]
    public void Server_EndTurn(PlayerState requester)
    {
        if (!gameStarted || turnOrder.Count == 0) return;
        if (turnOrder[currentTurnIndex] != requester) return;

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    // === Upgrades (global per cardId) ===
    [Server]
    public void Server_UpgradeCard(PlayerState ps, int handIndex)
    {
        if (!gameStarted || ps == null) return;
        if (!IsPlayersTurn(ps)) return;
        if (handIndex < 0 || handIndex >= ps.handIds.Count) return;

        int cardId = ps.handIds[handIndex];
        var def = database?.Get(cardId);
        if (def == null) return;

        int currentLvl = ps.Server_GetEffectiveLevelForHandIndex(handIndex);
        if (currentLvl >= def.MaxLevel)
        {
            TargetUpgradeDenied(ps.connectionToClient, handIndex, "MAX");
            return;
        }

        var next = def.GetTier(currentLvl + 1);
        int cost = next.costGold;

        if (ps.gold < cost)
        {
            TargetUpgradeDenied(ps.connectionToClient, handIndex, "GOLD");
            return;
        }

        // pay and apply global upgrade for this cardId
        ps.gold -= cost;
        byte newLevel = (byte)(currentLvl + 1);
        ps.upgradeLevels[cardId] = newLevel;          // remember globally for future draws
        ps.Server_PropagateUpgradeToAllCopies(cardId); // update all current copies

        TargetUpgradeOk(ps.connectionToClient, handIndex, newLevel, cost);
    }

    [TargetRpc]
    private void TargetUpgradeOk(NetworkConnection target, int handIndex, int newLevel, int cost)
    {
        Debug.Log("[Upgrade] OK: hand[" + handIndex + "] -> L" + newLevel + " (spent " + cost + "g)");
    }

    [TargetRpc]
    private void TargetUpgradeDenied(NetworkConnection target, int handIndex, string reason)
    {
        if (reason == "GOLD") Debug.LogWarning("[Upgrade] Not enough gold to upgrade hand[" + handIndex + "]");
        else if (reason == "MAX") Debug.LogWarning("[Upgrade] Already at max level for hand[" + handIndex + "]");
        else Debug.LogWarning("[Upgrade] Denied for hand[" + handIndex + "]: " + reason);
    }

    // Works on BOTH server and client
    public bool IsPlayersTurn(PlayerState ps)
    {
        if (!gameStarted || ps == null) return false;

        if (isServer)
            return turnOrder.Count > 0 && turnOrder[currentTurnIndex] == ps;

        return ps.netId == currentTurnNetId;
    }

    // Example helpers for effect resolver
    [Server]
    public void Server_HealAll(int amount)
    {
        foreach (var p in turnOrder) if (p != null) p.Server_Heal(amount);
    }

    [Server]
    public void Server_ChainArc(PlayerState caster, PlayerState startTarget, int amount, int arcs)
    {
        if (turnOrder.Count == 0 || startTarget == null) return;
        int idx = turnOrder.IndexOf(startTarget);
        if (idx < 0) return;

        int hits = Mathf.Max(1, arcs + 1);
        for (int i = 0; i < hits; i++)
        {
            var t = turnOrder[(idx + i) % turnOrder.Count];
            if (t != null) t.Server_ApplyDamage(caster, amount);
        }
    }
}
