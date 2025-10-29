// FILE: TurnManagerNet.cs
// FULL REPLACEMENT (ASCII)
// - Waits for lobby to end AND at least one seated player before starting Pre-Trade.
// - Seeds only SEATED, EMPTY, UNSEEDED players (seatIndex1Based > 0).
// - Keeps repeated seeding during Pre-Trade and a safety seed before first Turn.
// - Round resets seeded set at each Pre-Trade.

using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Turn Manager Net")]
public class TurnManagerNet : NetworkBehaviour
{
    public static TurnManagerNet Instance { get; private set; }

    public enum Phase { Waiting, PreTrade, Turn }

    // Seat order used during the Turn phase (must correspond to TraysRoot/Seat1..Seat5)
    public int[] seatTurnOrder = new int[] { 1, 2, 3, 4, 5 };

    [Header("Counts")]
    public int initialDraw = 4;
    public int drawAfterTurn = 2;

    [Header("Timing")]
    public float preTradeSeconds = 30f;
    public float turnSeconds = 30f;

    // HUD sync
    [SyncVar] public Phase phase = Phase.Waiting;
    [SyncVar] public double phaseEndTime = 0;
    [SyncVar] public int currentSeat = 0;
    [SyncVar] public uint currentTurnNetId = 0;
    [SyncVar] public double turnEndTime = 0;

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
        yield return new WaitForSeconds(0.25f);

        while (true)
        {
            // Stay in Waiting while lobby is active or no one is seated yet.
            if (IsLobbyActive() || !HasAnySeatedPlayer())
            {
                phase = Phase.Waiting;
                currentSeat = 0;
                currentTurnNetId = 0;
                turnEndTime = 0;
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            // ----- PRE-TRADE (craft window) -----
            phase = Phase.PreTrade;
            currentSeat = 0;
            currentTurnNetId = 0;
            turnEndTime = 0;
            phaseEndTime = NetworkTime.time + preTradeSeconds;

            // New round: clear seeded tracker
            seeded.Clear();

            // Seed seated, empty players at the start
            Server_SeedSeatedUnseededIfEmpty("pre-trade start");

            // Keep seeding during pre-trade for seats that appear late
            while (NetworkTime.time < phaseEndTime)
            {
                Server_SeedSeatedUnseededIfEmpty("pre-trade loop");
                yield return new WaitForSeconds(0.25f);
            }

            // Safety seed just before turns start
            Server_SeedSeatedUnseededIfEmpty("pre-turn safety");

            // ----- TURN PHASE -----
            phase = Phase.Turn;

            while (true)
            {
                var order = BuildCurrentOrder();
                if (order.Count == 0)
                {
                    // If table emptied (e.g., everyone left), go back to Waiting
                    yield return new WaitForSeconds(0.25f);
                    break;
                }

                for (int i = 0; i < seatTurnOrder.Length; i++)
                {
                    var p = ResolveBySeat(order, seatTurnOrder[i]);
                    if (p == null) continue;

                    var id = p.GetComponent<NetworkIdentity>();
                    currentSeat = p.seatIndex1Based;
                    currentTurnNetId = (id != null) ? id.netId : 0;

                    // Final safety: if never seeded and still empty, seed now
                    if (id != null && !seeded.Contains(id.netId) && p.inventory.Count == 0)
                    {
                        int added = p.Server_AddSeveralToInventoryClamped(initialDraw);
                        seeded.Add(id.netId);
                        Debug.Log("[TurnManagerNet] Seed on first turn: Seat" + p.seatIndex1Based +
                                  " netId=" + id.netId + " added=" + added);
                    }

                    // 1) Auto-consume at start of the turn
                    p.Server_ConsumeAllNow();

                    // 2) (Minigame placeholder)

                    // 3) Trading window
                    turnForceEnd = false;
                    turnEndTime = NetworkTime.time + turnSeconds;
                    while (NetworkTime.time < turnEndTime && !turnForceEnd)
                        yield return null;

                    // 4) End-of-turn grant
                    p.Server_AddSeveralToInventoryClamped(drawAfterTurn);

                    yield return new WaitForSeconds(0.25f);
                }
            }
        }
    }

    // Helpers

    private bool IsLobbyActive()
    {
        var ls = LobbyStage.Instance;
        if (ls == null) return true; // be conservative: if we cannot confirm, assume lobby
        return ls.lobbyActive;
    }

    private bool HasAnySeatedPlayer()
    {
        var traysAll = FindObjectsOfType<PlayerItemTrays>();
        for (int i = 0; i < traysAll.Length; i++)
            if (traysAll[i].seatIndex1Based > 0) return true;
        return false;
    }

    // Only seed players who are SEATED, EMPTY, and not yet seeded.
    [Server]
    private void Server_SeedSeatedUnseededIfEmpty(string label)
    {
        var traysAll = FindObjectsOfType<PlayerItemTrays>();
        int seededCount = 0;

        for (int i = 0; i < traysAll.Length; i++)
        {
            var p = traysAll[i];
            if (p == null) continue;
            if (p.seatIndex1Based <= 0) continue; // only after they are at the table

            var id = p.GetComponent<NetworkIdentity>();
            if (id == null) continue;
            if (seeded.Contains(id.netId)) continue;
            if (p.inventory.Count > 0) continue;

            int added = p.Server_AddSeveralToInventoryClamped(initialDraw);
            seeded.Add(id.netId);
            seededCount++;

            Debug.Log("[TurnManagerNet] Seed " + label + ": Seat" + p.seatIndex1Based +
                      " netId=" + id.netId + " added=" + added);
        }

        // Uncomment for verbose:
        // if (seededCount == 0) Debug.Log("[TurnManagerNet] Seed " + label + ": none");
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
