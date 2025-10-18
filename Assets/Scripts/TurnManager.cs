using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [Header("Game Rules (Inspector)")]
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private int startingGold = 5;
    [SerializeField] private int startingHandSize = 3;  // ▼ start with 3
    [SerializeField] private int cardsPerTurn = 1;
    [SerializeField] private int goldPerTurn = 1;
    [SerializeField] private int defaultDeckSize = 12;
    [SerializeField] private CardDatabase database;

    [Header("Start Control")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private bool autoStartOnFull = false;

    [Header("Chips")]
    [SerializeField] private int chipCap = 10;

    [SyncVar] private int currentTurnIndex = -1;
    [SyncVar] private bool gameStarted = false;

    [SyncVar(hook = nameof(OnTurnNetIdChanged))]
    private uint currentTurnNetId = 0;

    private readonly List<PlayerState> turnOrder = new List<PlayerState>();

    void Awake() { Instance = this; }
    public override void OnStartClient() { base.OnStartClient(); Instance = this; }
    public override void OnStartServer() { base.OnStartServer(); Instance = this; }

    void OnTurnNetIdChanged(uint oldV, uint newV) { /* UI hook if needed */ }

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

        currentTurnIndex = 0;              // Player 1 first
        gameStarted = true;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    [Server]
    private void Server_StartTurn(PlayerState ps)
    {
        if (!gameStarted || ps == null) return;

        // NEW RULE: start of turn gives NOTHING (resources already granted to the owner when they ENDED their previous turn)
        ps.Server_OnTurnStart_ProcessStatuses(); // still process statuses

        currentTurnNetId = ps.netId;
        RpcTurnChanged(currentTurnIndex);
        TargetYourTurn(ps.connectionToClient);
    }

    [ClientRpc] private void RpcTurnChanged(int index) { }
    [TargetRpc] private void TargetYourTurn(NetworkConnection target) { }

    [Server]
    public void Server_EndTurn(PlayerState requester)
    {
        if (!gameStarted || turnOrder.Count == 0) return;
        if (turnOrder[currentTurnIndex] != requester) return;

        // ▼ NEW: Give resources to the player who just ended their turn (for their NEXT turn)
        requester.Server_IncreaseMaxChipsAndRefill(chipCap, 1);
        requester.gold += goldPerTurn;
        requester.Server_Draw(cardsPerTurn);

        // Advance to next player
        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    // === Upgrades: ONLY off-turn ===
    [Server]
    public void Server_UpgradeCard(PlayerState ps, int handIndex)
    {
        if (!gameStarted || ps == null) return;
        if (IsPlayersTurn(ps)) { TargetUpgradeDenied(ps.connectionToClient, handIndex, "TURN"); return; }  // must be off-turn
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

        ps.gold -= cost;
        byte newLevel = (byte)(currentLvl + 1);
        ps.upgradeLevels[cardId] = newLevel;
        ps.Server_PropagateUpgradeToAllCopies(cardId);

        TargetUpgradeOk(ps.connectionToClient, handIndex, newLevel, cost);
    }

    [TargetRpc]
    private void TargetUpgradeOk(NetworkConnection target, int handIndex, int newLevel, int cost)
        => Debug.Log("[Upgrade] OK: hand[" + handIndex + "] -> L" + newLevel + " (spent " + cost + "g)");

    [TargetRpc]
    private void TargetUpgradeDenied(NetworkConnection target, int handIndex, string reason)
    {
        if (reason == "GOLD") Debug.LogWarning("[Upgrade] Not enough gold to upgrade hand[" + handIndex + "]");
        else if (reason == "MAX") Debug.LogWarning("[Upgrade] Already at max level for hand[" + handIndex + "]");
        else if (reason == "TURN") Debug.LogWarning("[Upgrade] You can only upgrade OFF-turn.");
        else Debug.LogWarning("[Upgrade] Denied for hand[" + handIndex + "]: " + reason);
    }

    // Works on BOTH server and client
    public bool IsPlayersTurn(PlayerState ps)
    {
        if (!gameStarted || ps == null) return false;
        if (isServer) return turnOrder.Count > 0 && turnOrder[currentTurnIndex] == ps;
        return ps.netId == currentTurnNetId;
    }

    // helpers for effect resolver (unchanged)
    [Server] public void Server_HealAll(int amount) { /* ... */ }
    [Server] public void Server_ChainArc(PlayerState caster, PlayerState startTarget, int amount, int arcs) { /* ... */ }
}
