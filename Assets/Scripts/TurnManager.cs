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
    [SerializeField] private int minPlayersToStart = 2;      // you can change in Inspector
    [SerializeField] private bool autoStartOnFull = false;   // set true if you want old behavior

    [SyncVar] private int currentTurnIndex = -1;
    [SyncVar] private bool gameStarted = false;

    private readonly List<PlayerState> turnOrder = new List<PlayerState>();

    public override void OnStartServer()
    {
        Instance = this;
    }

    [Server]
    public void RegisterPlayer(PlayerState ps)
    {
        if (turnOrder.Contains(ps)) return;
        turnOrder.Add(ps);
        ps.turnManager = this;

        if (autoStartOnFull && turnOrder.Count == maxPlayers && !gameStarted)
        {
            Server_AttemptStartGame();
        }
    }

    [Server]
    public void Server_AttemptStartGame()
    {
        if (gameStarted) return;
        if (turnOrder.Count < minPlayersToStart) return;

        // init all players
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var p = turnOrder[i];
            p.Server_Init(database, defaultDeckSize, startingGold, startingHandSize);
        }

        currentTurnIndex = 0;
        gameStarted = true;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    [Server]
    private void Server_StartTurn(PlayerState ps)
    {
        if (!gameStarted) return;

        ps.Server_Draw(cardsPerTurn);
        ps.gold += goldPerTurn;

        RpcTurnChanged(currentTurnIndex);
        TargetYourTurn(ps.connectionToClient);
    }

    [ClientRpc]
    private void RpcTurnChanged(int index)
    {
        // TODO: global UI hook
    }

    [TargetRpc]
    private void TargetYourTurn(NetworkConnection target)
    {
        // TODO: local UI hook
    }

    [Server]
    public void Server_EndTurn(PlayerState requester)
    {
        if (!gameStarted) return;
        if (turnOrder.Count == 0) return;
        if (turnOrder[currentTurnIndex] != requester) return;

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        Server_StartTurn(turnOrder[currentTurnIndex]);
    }

    [Server]
    public void Server_UpgradeCard(PlayerState ps, int handIndex)
    {
        if (!gameStarted) return;
        if (ps == null) return;
        if (handIndex < 0 || handIndex >= ps.handIds.Count) return;

        int id = ps.handIds[handIndex];
        int lvl = ps.handLvls[handIndex];
        var def = (database != null) ? database.Get(id) : null;
        if (def == null) return;
        if (lvl >= def.MaxLevel) return;

        var nextTier = def.GetTier(lvl + 1);
        if (ps.gold < nextTier.costGold) return;

        ps.gold -= nextTier.costGold;
        ps.handLvls[handIndex] = (byte)(lvl + 1);
    }
}
