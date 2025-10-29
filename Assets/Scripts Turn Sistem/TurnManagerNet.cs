// FILE: TurnManagerNet.cs
// FULL REPLACEMENT (ASCII)
// Fixes: late seat assignment missing initial 4 items.
// - Seeds at PreTrade start (as before)
// - Also seeds right after PreTrade ends (entering Turn)
// - Also seeds at the start of a player's first turn if still unseeded

using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Turn Manager Net")]
public class TurnManagerNet : NetworkBehaviour
{
    public static TurnManagerNet Instance { get; private set; }

    public enum Phase { Waiting, PreTrade, Turn }

    public int[] seatTurnOrder = new int[] { 1, 2, 3, 4, 5 };

    [Header("Counts")]
    public int initialDraw = 4;
    public int drawAfterTurn = 2;

    [Header("Timing")]
    public float preTradeSeconds = 30f;
    public float turnSeconds = 30f;

    [SyncVar] public Phase phase = Phase.Waiting;
    [SyncVar] public double phaseEndTime = 0;
    [SyncVar] public int currentSeat = 0;
    [SyncVar] public uint currentTurnNetId = 0;
    [SyncVar] public double turnEndTime = 0;

    // track who already got initialDraw
    private readonly HashSet<uint> seeded = new HashSet<uint>();
    private bool turnForceEnd = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(ServerMainLoop());
    }

    [Server]
    public void Server_EndCurrentTurnBy(uint requesterNetId)
    {
        if (phase != Phase.Turn) return;
        if (requesterNetId == 0) return;
        if (requesterNetId != currentTurnNetId) return;
        turnForceEnd = true;
    }

    [Server]
    public bool CanTradeNow(NetworkIdentity who)
    {
        if (phase != Phase.Turn) return false;
        if (who == null) return false;
        if (who.netId != currentTurnNetId) return false;
        if (NetworkTime.time >= turnEndTime) return false;
        return true;
    }

    private IEnumerator ServerMainLoop()
    {
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            var order = BuildCurrentOrder();
            if (order.Count == 0)
            {
                phase = Phase.Waiting;
                currentSeat = 0;
                currentTurnNetId = 0;
                turnEndTime = 0;
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // ----- PRE-TRADE -----
            phase = Phase.PreTrade;
            currentSeat = 0;
            currentTurnNetId = 0;
            turnEndTime = 0;
            phaseEndTime = NetworkTime.time + preTradeSeconds;

            SeedUnseededIfEmpty(order); // initial grant at the start

            // also handle late seat joins during pre-trade
            while (NetworkTime.time < phaseEndTime)
            {
                order = BuildCurrentOrder();
                SeedUnseededIfEmpty(order);
                yield return new WaitForSeconds(0.25f);
            }

            // Before turns start, one more safety seed (covers teleports that finished right at the end)
            order = BuildCurrentOrder();
            SeedUnseededIfEmpty(order);

            // ----- TURN PHASE -----
            phase = Phase.Turn;

            while (true)
            {
                order = BuildCurrentOrder();
                if (order.Count == 0) break;

                for (int i = 0; i < seatTurnOrder.Length; i++)
                {
                    var p = ResolveBySeat(order, seatTurnOrder[i]);
                    if (p == null) continue;

                    var id = p.GetComponent<NetworkIdentity>();
                    currentSeat = p.seatIndex1Based;
                    currentTurnNetId = (id != null) ? id.netId : 0;

                    // final safety: if this player somehow never got seeded and is empty, seed now
                    if (id != null && !seeded.Contains(id.netId) && p.inventory.Count == 0)
                    {
                        p.Server_AddSeveralToInventoryClamped(initialDraw);
                        seeded.Add(id.netId);
                    }

                    // 1) Auto-consume
                    p.Server_ConsumeAllNow();

                    // 2) (minigame placeholder spot)

                    // 3) trading window for this player
                    turnForceEnd = false;
                    turnEndTime = NetworkTime.time + turnSeconds;

                    while (NetworkTime.time < turnEndTime && !turnForceEnd)
                        yield return null;

                    // 4) end-of-turn grant
                    p.Server_AddSeveralToInventoryClamped(drawAfterTurn);

                    yield return new WaitForSeconds(0.25f);
                }
            }
        }
    }

    // seed all seated players that are empty and not yet seeded
    [Server]
    private void SeedUnseededIfEmpty(List<PlayerItemTrays> trays)
    {
        for (int i = 0; i < trays.Count; i++)
        {
            var p = trays[i];
            var id = p.GetComponent<NetworkIdentity>();
            if (id == null) continue;
            if (seeded.Contains(id.netId)) continue;
            if (p.inventory.Count > 0) continue;

            p.Server_AddSeveralToInventoryClamped(initialDraw);
            seeded.Add(id.netId);
        }
    }

    private List<PlayerItemTrays> BuildCurrentOrder()
    {
        var traysAll = FindObjectsOfType<PlayerItemTrays>();
        Dictionary<int, PlayerItemTrays> bySeat = new Dictionary<int, PlayerItemTrays>();
        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t.seatIndex1Based <= 0) continue;
            if (!bySeat.ContainsKey(t.seatIndex1Based))
                bySeat[t.seatIndex1Based] = t;
        }

        var result = new List<PlayerItemTrays>(seatTurnOrder.Length);
        for (int i = 0; i < seatTurnOrder.Length; i++)
        {
            int seat = seatTurnOrder[i];
            PlayerItemTrays t;
            if (bySeat.TryGetValue(seat, out t))
                result.Add(t);
        }
        return result;
    }

    private PlayerItemTrays ResolveBySeat(List<PlayerItemTrays> order, int seat)
    {
        for (int i = 0; i < order.Count; i++)
            if (order[i].seatIndex1Based == seat)
                return order[i];
        return null;
    }
}
