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

    [SyncVar] private int currentTurnIndex = -1;
    [SyncVar] private bool gameStarted = false;

    private readonly List<PlayerState> turnOrder = new List<PlayerState>();

    public override void OnStartServer() { Instance = this; }

    [Server]
    public void RegisterPlayer(PlayerState ps)
    {
        if (turnOrder.Contains(ps)) return;
        turnOrder.Add(ps);
        ps.turnManager = this;
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
        if (!gameStarted) return;

        // Start-of-turn status processing (poison, countdowns later)
        ps.Server_OnTurnStart_ProcessStatuses();

        // Draw and gold
        ps.Server_Draw(cardsPerTurn);
        ps.gold += goldPerTurn;

        RpcTurnChanged(currentTurnIndex);
        TargetYourTurn(ps.connectionToClient);
    }

    [ClientRpc] private void RpcTurnChanged(int index) { /* global UI hook */ }
    [TargetRpc] private void TargetYourTurn(NetworkConnection target) { /* local UI hook */ }

    [Server]
    public void Server_EndTurn(PlayerState requester)
    {
        if (!gameStarted || turnOrder.Count == 0) return;
        if (turnOrder[currentTurnIndex] != requester) return;

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    [Server]
    public void Server_UpgradeCard(PlayerState ps, int handIndex)
    {
        if (!gameStarted || ps == null) return;
        if (handIndex < 0 || handIndex >= ps.handIds.Count) return;

        var def = database?.Get(ps.handIds[handIndex]);
        if (def == null) return;
        int lvl = ps.handLvls[handIndex];
        if (lvl >= def.MaxLevel) return;

        var next = def.GetTier(lvl + 1);
        if (ps.gold < next.costGold) return;

        ps.gold -= next.costGold;
        ps.handLvls[handIndex] = (byte)(lvl + 1);
    }

    // === Helpers used by CardEffectResolver ===
    [Server] public bool IsPlayersTurn(PlayerState ps) => gameStarted && turnOrder.Count > 0 && turnOrder[currentTurnIndex] == ps;

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

        int hits = Mathf.Max(1, arcs + 1); // first + bounces
        for (int i = 0; i < hits; i++)
        {
            var t = turnOrder[(idx + i) % turnOrder.Count];
            if (t != null) t.Server_ApplyDamage(caster, amount);
        }
    }
}
