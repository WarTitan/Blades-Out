using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [Header("Game Rules (Inspector)")]
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private int startingGold = 5;
    [SerializeField] private int startingHandSize = 3;
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

    void OnTurnNetIdChanged(uint oldV, uint newV) { /* optional UI hook */ }

    // ---------------- Registration / start ----------------

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

        // reset deaths before new game
        foreach (var p in turnOrder)
        {
            if (p == null) continue;
            p.isDead = false;
        }

        for (int i = 0; i < turnOrder.Count; i++)
            turnOrder[i].Server_Init(database, defaultDeckSize, startingGold, startingHandSize);

        gameStarted = true;
        Server_StartTurnFrom(0);
    }

    // ---------------- Turn flow ----------------

    [Server]
    public void Server_EndTurn(PlayerState requester)
    {
        if (!gameStarted || turnOrder.Count == 0) return;
        if (turnOrder[currentTurnIndex] != requester) return;

        // Rewards for the player who ended their turn (applies to their next turn)
        // Rewards for the player who ended their turn (applies to their next turn)
        requester.Server_IncreaseMaxChipsAndRefill(chipCap, 1);
        requester.gold += goldPerTurn;

        // start a draft instead of auto draw
        var draft = requester.GetComponent<DraftDrawNet>();
        if (draft != null)
            draft.Server_StartDraft(cardsPerTurn); // usually 1
        else
            requester.Server_Draw(cardsPerTurn);   // fallback if the component is missing


        int next = NextIndex(currentTurnIndex);
        Server_StartTurnFrom(next);
    }

    [Server]
    public void Server_SkipTurn(PlayerState who)
    {
        if (!gameStarted || turnOrder.Count == 0) return;
        if (!IsPlayersTurn(who)) return;
        int next = NextIndex(currentTurnIndex);
        Server_StartTurnFrom(next);
    }

    // Resolve directives BEFORE locking the turn owner.
    [Server]
    private void Server_StartTurnFrom(int startIndex)
    {
        if (!gameStarted || turnOrder.Count == 0) return;

        int idx = Mathf.Abs(startIndex) % turnOrder.Count;

        // Loop to resolve chained skips / deaths / turtles, with a safety cap
        for (int safety = 0; safety < 32; safety++)
        {
            var ps = turnOrder[idx];
            if (ps == null)
            {
                idx = NextIndex(idx);
                continue;
            }

            // Dead players cannot take a turn (unless auto-revive happens inside)
            var directive = ps.Server_OnTurnStart_ProcessStatuses(); // handles death & phoenix & turtle

            if (directive == PlayerState.TurnStartDirective.SkipNoRewards)
            {
                // still dead or instructed to skip w/o rewards
                idx = NextIndex(idx);
                continue;
            }

            if (directive == PlayerState.TurnStartDirective.AutoEndWithRewardsAndPass)
            {
                // Lock owner so UIs flip correctly
                currentTurnIndex = idx;
                currentTurnNetId = ps.netId;
                RpcTurnChanged(currentTurnIndex);
                TargetYourTurn(ps.connectionToClient);

                // Pass turtle to a random other player
                Server_PassTurtleFrom(ps);

                // Immediately auto-end WITH rewards
                Server_EndTurn(ps);
                return;
            }

            // Normal owner
            currentTurnIndex = idx;
            currentTurnNetId = ps.netId;
            RpcTurnChanged(currentTurnIndex);
            TargetYourTurn(ps.connectionToClient);
            return;
        }

        // Failsafe: lock anyway
        currentTurnIndex = idx;
        var owner = turnOrder[idx];
        currentTurnNetId = owner ? owner.netId : 0;
        RpcTurnChanged(currentTurnIndex);
        if (owner) TargetYourTurn(owner.connectionToClient);
    }

    [ClientRpc] private void RpcTurnChanged(int index) { }
    [TargetRpc] private void TargetYourTurn(NetworkConnection target) { }

    private int NextIndex(int i) => (turnOrder.Count == 0) ? 0 : (i + 1) % turnOrder.Count;

    // Works on BOTH server and client
    public bool IsPlayersTurn(PlayerState ps)
    {
        if (!gameStarted || ps == null) return false;
        if (isServer) return turnOrder.Count > 0 && turnOrder[currentTurnIndex] == ps;
        return ps.netId == currentTurnNetId;
    }

    // ---------------- Upgrades ----------------

    [Server]
    public void Server_UpgradeCard(PlayerState ps, int handIndex)
    {
        if (!gameStarted || ps == null) return;
        if (ps.isDead) { TargetUpgradeDenied(ps.connectionToClient, handIndex, "DEAD"); return; }
        if (IsPlayersTurn(ps)) { TargetUpgradeDenied(ps.connectionToClient, handIndex, "TURN"); return; }
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
        else if (reason == "DEAD") Debug.LogWarning("[Upgrade] You are dead and cannot upgrade.");
        else Debug.LogWarning("[Upgrade] Denied for hand[" + handIndex + "]: " + reason);
    }

    // ---------------- Turtle pass ----------------

    [Server]
    PlayerState PickRandomOther(PlayerState exclude)
    {
        if (turnOrder.Count <= 1) return null;
        List<int> candidates = new List<int>();
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var p = turnOrder[i];
            if (p != null && p != exclude) candidates.Add(i);
        }
        if (candidates.Count == 0) return null;
        int pick = candidates[Random.Range(0, candidates.Count)];
        return turnOrder[pick];
    }

    [Server]
    void Server_PassTurtleFrom(PlayerState from)
    {
        var to = PickRandomOther(from);
        if (to != null)
            to.Server_AddStatus_TurtleSkipNext();
    }

    // ---------------- End game ----------------

    [Server]
    public int CountAlivePlayers()
    {
        int alive = 0;
        foreach (var p in turnOrder)
            if (p != null && !p.isDead) alive++;
        return alive;
    }

    [Server]
    public PlayerState GetLastAlivePlayer()
    {
        PlayerState last = null;
        foreach (var p in turnOrder)
            if (p != null && !p.isDead) last = p;
        return last;
    }

    [Server]
    public void Server_CheckForEndGame()
    {
        if (!gameStarted) return;
        int alive = CountAlivePlayers();
        if (alive <= 1)
        {
            var winner = GetLastAlivePlayer();
            int winnerSeat = winner ? winner.seatIndex : -1;
            RpcMatchEnded(winnerSeat);

            // Stop game; wait for user to press "3" to start again
            gameStarted = false;
            currentTurnIndex = -1;
            currentTurnNetId = 0;
        }
    }

    [ClientRpc]
    void RpcMatchEnded(int winnerSeat)
    {
        if (winnerSeat >= 0)
            Debug.Log("[Match] Winner: seat " + winnerSeat + ". Press 3 to start a new game.");
        else
            Debug.Log("[Match] No winner (all dead?). Press 3 to start a new game.");
    }

    // Optional stubs
    [Server] public void Server_HealAll(int amount) { }
    [Server] public void Server_ChainArc(PlayerState caster, PlayerState startTarget, int amount, int arcs) { }
}
