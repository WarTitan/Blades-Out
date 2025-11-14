// FILE: TurnManagerNet.cs
// Darts test phase + immediate draw when returning to table (press 3).
// Adds skipNextCraftDraw so the next Crafting start won't double-draw after the immediate grant.

using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Turn Manager Net")]
public class TurnManagerNet : NetworkBehaviour
{
    public static TurnManagerNet Instance { get; private set; }

    public enum Phase
    {
        Waiting,
        Crafting,
        Turn,
        Darts
    }

    [Header("Turn Order (seats 1..5)")]
    public int[] seatTurnOrder = new int[] { 1, 2, 3, 4, 5 };

    [Header("Draw Counts")]
    public int initialDraw = 4;
    public int drawAfterTurn = 2;

    [Header("Timing")]
    public float craftingSeconds = 20f;
    public float memorySeconds = 45f;

    [Header("Trading Safety")]
    public float giftGraceSeconds = 0.25f;

    [Header("Gating")]
    public bool ignoreLobbyForTesting = false;

    // HUD / phase sync
    [SyncVar] public Phase phase = Phase.Waiting;
    [SyncVar] public int currentSeat = 0;
    [SyncVar] public uint currentTurnNetId = 0;
    [SyncVar] public double turnEndTime = 0;

    // Memory HUD sync
    [SyncVar] public bool memoryActive = false;
    [SyncVar] public double memoryEndTime = 0;

    // Darts test
    [SyncVar] public bool dartsTestActive = false;

    // Internal
    private readonly HashSet<uint> seededThisCycle = new HashSet<uint>();
    private bool loopStarted = false;
    private float nextWaitLog = 0f;
    private bool hasHadAtLeastOneCraftPhase = false;
    private double lastCraftingEndTime = 0;

    // Avoid double-draw after we grant items immediately when returning from darts
    private bool skipNextCraftDraw = false;

    // Memory flow
    private int memoryToken = 0;
    private uint memoryPlayerNetId = 0;
    private bool memoryResultArrived = false;

#pragma warning disable CS0618
    private bool turnForceEnd = false;
#pragma warning restore CS0618

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        StartCoroutine(ServerLoopGuard());
    }

    private IEnumerator ServerLoopGuard()
    {
        while (!loopStarted)
        {
            if (NetworkServer.active)
            {
                loopStarted = true;
                StartCoroutine(ServerMainLoop());
                Debug.Log("[TurnManagerNet] Server loop started.");
                yield break;
            }
            yield return null;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!loopStarted)
        {
            loopStarted = true;
            StartCoroutine(ServerMainLoop());
            Debug.Log("[TurnManagerNet] Server loop started (OnStartServer).");
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        loopStarted = false;
        StopAllCoroutines();
        SetWaiting();
        Debug.Log("[TurnManagerNet] Server loop stopped.");
    }

    // Legacy no-op kept for compatibility
    [Server]
    public void Server_EndCurrentTurnBy(uint requesterNetId)
    {
        if (!NetworkServer.active) return;
        Debug.Log("[TurnManagerNet] Server_EndCurrentTurnBy called by netId " + requesterNetId + " (no-op).");
    }

    // Called when trays spawn
    [Server]
    public void Server_OnPlayerTraysSpawned(PlayerItemTrays trays)
    {
        if (trays == null) return;
        if (trays.inventory.Count > 0) return;
        int added = trays.Server_AddSeveralToInventoryClamped(initialDraw);
        Debug.Log("[TurnManagerNet] Spawn seed: seat=" + trays.seatIndex1Based + " added=" + added);
    }

    // Darts control
    [Server]
    public void Server_BeginDartsTest()
    {
        dartsTestActive = true;
        phase = Phase.Darts;            // <-- original HUD should show "Playing Darts" when phase == Darts
        currentSeat = 0;
        currentTurnNetId = 0;
        memoryActive = false;
        memoryEndTime = 0;
        turnEndTime = 0;
        Debug.Log("[TurnManagerNet] Darts test ACTIVE. Main loop paused.");
    }

    [Server]
    public void Server_EndDartsTest()
    {
        dartsTestActive = false;
        Debug.Log("[TurnManagerNet] Darts test ENDED. Main loop will resume.");
    }

    // Grant immediate per-turn draw on return from darts; skip the next Crafting-start draw once.
    [Server]
    public void Server_GrantReturnToTableDraw()
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
        if (traysAll == null) return;

        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t == null || t.seatIndex1Based <= 0) continue;
            var id = t.GetComponent<NetworkIdentity>();
            if (id == null) continue;
            var e = MemoryStrikeTracker.FindForNetId(id.netId);
            if (e != null && e.eliminated) continue;

            int added = t.Server_AddSeveralToInventoryClamped(drawAfterTurn);
            if (added > 0)
                Debug.Log("[TurnManagerNet] Return-to-table draw: Seat " + t.seatIndex1Based + " added=" + added);
        }

        // Avoid double grant at the very next crafting start.
        skipNextCraftDraw = true;
    }

    // Trade windows
    [Server]
    public bool CanTradeNow(NetworkIdentity who)
    {
        if (who == null) return false;
        var e = MemoryStrikeTracker.FindForNetId(who.netId);
        if (e != null && e.eliminated) return false;
        if (phase == Phase.Crafting) return true;
        if (giftGraceSeconds > 0f && (NetworkTime.time - lastCraftingEndTime) <= giftGraceSeconds) return true;
        return false;
    }

    [Server]
    public bool Server_OnGift(uint giverNetId, uint targetNetId)
    {
        if (giverNetId == 0 || targetNetId == 0) return false;
        var giver = MemoryStrikeTracker.FindForNetId(giverNetId);
        if (giver != null && giver.eliminated) return false;
        var target = MemoryStrikeTracker.FindForNetId(targetNetId);
        if (target != null && target.eliminated) return false;
        if (phase == Phase.Crafting) return true;
        if (giftGraceSeconds > 0f && (NetworkTime.time - lastCraftingEndTime) <= giftGraceSeconds) return true;
        return false;
    }

    // ---------------- Main Loop ----------------
    private IEnumerator ServerMainLoop()
    {
        yield return new WaitForSeconds(0.25f);

        while (true)
        {
            // Park in Darts
            if (dartsTestActive)
            {
                phase = Phase.Darts;
                currentSeat = 0;
                currentTurnNetId = 0;
                memoryActive = false;
                memoryEndTime = 0;
                turnEndTime = 0;
                yield return null;
                continue;
            }

            if (!HasAnyPlayers()) { LogWaiting("no players found"); SetWaiting(); yield return new WaitForSeconds(0.25f); continue; }
            if (IsLobbyBlockingStart()) { LogWaiting("waiting for lobby / game start"); SetWaiting(); yield return new WaitForSeconds(0.25f); continue; }
            if (!HasAnySeatedPlayer()) { LogWaiting("no seated players (seatIndex1Based == 0)"); SetWaiting(); yield return new WaitForSeconds(0.25f); continue; }
            if (!HasSeatIndexAuthority()) { LogWaiting("no SeatIndexAuthority"); SetWaiting(); yield return new WaitForSeconds(0.25f); continue; }

            // -------- Crafting (All) --------
            phase = Phase.Crafting;
            currentSeat = 0;
            currentTurnNetId = 0;
            memoryActive = false;
            memoryEndTime = 0;

            seededThisCycle.Clear();
            Server_LogSeatMap("pre-crafting");

            if (!hasHadAtLeastOneCraftPhase)
                Server_SeedSeatedIfEmpty("first crafting start");

            if (hasHadAtLeastOneCraftPhase)
            {
                if (!skipNextCraftDraw)
                {
                    Server_DrawAtCraftStartForAll("crafting start");
                }
                else
                {
                    skipNextCraftDraw = false; // consume the skip
                }
            }

            double craftEnd = NetworkTime.time + craftingSeconds;
            turnEndTime = craftEnd;

            Debug.Log("[TurnManagerNet] Crafting phase started for ALL players (" + craftingSeconds + "s).");

            while (NetworkTime.time < craftEnd)
            {
                if (dartsTestActive) break;
                yield return null;
            }

            lastCraftingEndTime = craftEnd;
            hasHadAtLeastOneCraftPhase = true;
            if (dartsTestActive) continue;

            // -------- Memory/Delivery per seat --------
            phase = Phase.Turn;
            turnEndTime = 0;
            currentSeat = 0;
            currentTurnNetId = 0;

            var order = BuildOrderSkippingEliminated();
            if (order.Count == 0) { LogWaiting("no active players for delivery"); SetWaiting(); yield return new WaitForSeconds(0.25f); continue; }

            for (int i = 0; i < seatTurnOrder.Length; i++)
            {
                if (dartsTestActive) break;

                var trays = ResolveBySeat(order, seatTurnOrder[i]);
                if (trays == null) continue;

                var id = trays.GetComponent<NetworkIdentity>();
                if (id == null) continue;
                if (IsEliminated(id.netId)) continue;

                int seat = trays.seatIndex1Based;
                if (seat <= 0) continue;

                int pending = trays.consume.Count;
                if (pending <= 0)
                {
                    Debug.Log("[TurnManagerNet] Seat " + seat + " has no items in CONSUME; skipping memory.");
                    continue;
                }

                currentSeat = seat;
                currentTurnNetId = id.netId;

                Debug.Log("[TurnManagerNet] Delivery: Seat " + seat +
                          " consumes " + pending + " item(s) from CONSUME and plays memory. netId " + id.netId);

                trays.Server_ConsumeAllNow();

                yield return Server_PlayMemory(id, seat);

                currentSeat = 0;
                currentTurnNetId = 0;

                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private void SetWaiting()
    {
        phase = Phase.Waiting;
        currentSeat = 0;
        currentTurnNetId = 0;
        turnEndTime = 0;
        memoryActive = false;
        memoryEndTime = 0;
    }

    private void LogWaiting(string why)
    {
        if (Time.time >= nextWaitLog)
        {
            nextWaitLog = Time.time + 3f;
            Debug.Log("[TurnManagerNet] Waiting: " + why);
        }
    }

    // -------- Memory --------
    [Server]
    private IEnumerator Server_PlayMemory(NetworkIdentity playerId, int seatIndex1Based)
    {
        if (playerId == null) yield break;

        memoryResultArrived = false;
        memoryToken++;
        memoryPlayerNetId = playerId.netId;

        memoryActive = true;
        memoryEndTime = NetworkTime.time + memorySeconds;

        Debug.Log("[TurnManagerNet] Start memory for netId=" + playerId.netId + " seat=" + seatIndex1Based);

        if (playerId.connectionToClient != null)
            Target_StartMemoryGame(playerId.connectionToClient, memoryToken, memorySeconds, seatIndex1Based);

        while (!memoryResultArrived && NetworkTime.time < memoryEndTime)
        {
            if (dartsTestActive) break;
            yield return null;
        }

        if (!memoryResultArrived)
        {
            OnMemoryFail(memoryPlayerNetId);
            if (MemoryLevelTracker.Instance != null)
                MemoryLevelTracker.Instance.Server_OnMemoryResultUpdate(memoryPlayerNetId, false);
        }

        memoryActive = false;
        memoryEndTime = 0;
    }

    [TargetRpc]
    private void Target_StartMemoryGame(NetworkConnectionToClient conn, int token, float seconds, int seatIndex1Based)
    {
        MemorySequenceUI.BeginStatic(token, seconds, seatIndex1Based);
    }

    [Server]
    public void Server_OnMemoryResult(uint playerNetId, int token, int best)
    {
        if (!memoryActive) return;
        if (playerNetId != memoryPlayerNetId) return;
        if (token != memoryToken) return;

        bool success = best > 0;
        if (!success) OnMemoryFail(playerNetId);

        if (MemoryLevelTracker.Instance != null)
            MemoryLevelTracker.Instance.Server_OnMemoryResultUpdate(playerNetId, success);

        memoryResultArrived = true;
    }

    [Server]
    private void OnMemoryFail(uint netId)
    {
        if (MemoryStrikeTracker.Instance != null)
        {
            MemoryStrikeTracker.Instance.Server_AddStrike(netId);
            var e = MemoryStrikeTracker.FindForNetId(netId);
            Debug.Log("[TurnManagerNet] Memory FAIL -> strikes " +
                      (e != null ? e.strikes : 0) + "/" + MemoryStrikeTracker.MaxStrikes +
                      " (netId " + netId + ")");
        }
        else
        {
            Debug.LogWarning("[TurnManagerNet] MemoryStrikeTracker missing; cannot add strike.");
        }
    }

    // -------- Helpers --------
    private bool IsLobbyBlockingStart()
    {
        if (ignoreLobbyForTesting) return false;
        var ls = LobbyStage.Instance;
        if (ls == null) return false;
        return ls.lobbyActive;
    }

#pragma warning disable CS0618
    private bool HasAnyPlayers()
    {
        var trays = GameObject.FindObjectsOfType<PlayerItemTrays>();
        return trays != null && trays.Length > 0;
    }

    private bool HasAnySeatedPlayer()
    {
        var trays = GameObject.FindObjectsOfType<PlayerItemTrays>();
        for (int i = 0; i < trays.Length; i++)
            if (trays[i].seatIndex1Based > 0) return true;
        return false;
    }
#pragma warning restore CS0618

    private bool HasSeatIndexAuthority()
    {
#pragma warning disable CS0618
        var seats = GameObject.FindObjectsOfType<SeatIndexAuthority>();
#pragma warning restore CS0618
        return seats != null && seats.Length > 0;
    }

    [Server]
    private void Server_SeedSeatedIfEmpty(string label)
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
        var deck = Object.FindObjectOfType<ItemDeck>();
#pragma warning restore CS0618

        if (traysAll == null || traysAll.Length == 0) return;

        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t == null) continue;
            if (t.seatIndex1Based <= 0) continue;

            var id = t.GetComponent<NetworkIdentity>();
            if (id == null) continue;
            if (seededThisCycle.Contains(id.netId)) continue;
            if (t.inventory.Count > 0) continue;

            int added = t.Server_AddSeveralToInventoryClamped(initialDraw);
            seededThisCycle.Add(id.netId);
            Debug.Log("[TurnManagerNet] Seed " + label + ": Seat " + t.seatIndex1Based +
                      " netId=" + (id != null ? id.netId.ToString() : "0") + " added=" + added);
        }
    }

    [Server]
    private void Server_DrawAtCraftStartForAll(string label)
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
        if (traysAll == null || traysAll.Length == 0) return;

        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t == null) continue;
            if (t.seatIndex1Based <= 0) continue;

            var id = t.GetComponent<NetworkIdentity>();
            if (id == null) continue;
            if (IsEliminated(id.netId)) continue;

            int added = t.Server_AddSeveralToInventoryClamped(drawAfterTurn);
            if (added > 0)
            {
                Debug.Log("[TurnManagerNet] Draw " + label + ": Seat " + t.seatIndex1Based +
                          " netId=" + id.netId + " added=" + added + " (drawAfterTurn=" + drawAfterTurn + ")");
            }
        }
    }

    [Server]
    private void Server_LogSeatMap(string tag)
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
        if (traysAll == null || traysAll.Length == 0)
        {
            Debug.Log("[TurnManagerNet] SeatMap(" + tag + "): no trays found.");
            return;
        }

        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            var id = t.GetComponent<NetworkIdentity>();
            uint nid = (id != null) ? id.netId : 0;
            Debug.Log("[TurnManagerNet] SeatMap(" + tag + "): netId=" + nid +
                      " seat=" + t.seatIndex1Based +
                      " inv=" + t.inventory.Count);
        }
    }

    private bool IsEliminated(uint netId)
    {
        if (netId == 0) return true;
        var e = MemoryStrikeTracker.FindForNetId(netId);
        return e != null && e.eliminated;
    }

    private List<PlayerItemTrays> BuildOrderSkippingEliminated()
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
        Dictionary<int, PlayerItemTrays> bySeat = new Dictionary<int, PlayerItemTrays>();
        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t.seatIndex1Based <= 0) continue;
            var id = t.GetComponent<NetworkIdentity>();
            if (id == null) continue;
            if (IsEliminated(id.netId)) continue;
            if (!bySeat.ContainsKey(t.seatIndex1Based)) bySeat[t.seatIndex1Based] = t;
        }

        var result = new List<PlayerItemTrays>(seatTurnOrder.Length);
        for (int i = 0; i < seatTurnOrder.Length; i++)
        {
            int seat = seatTurnOrder[i];
            PlayerItemTrays t;
            if (bySeat.TryGetValue(seat, out t)) result.Add(t);
        }
        return result;
    }

    private PlayerItemTrays ResolveBySeat(List<PlayerItemTrays> order, int seat)
    {
        for (int i = 0; i < order.Count; i++)
            if (order[i].seatIndex1Based == seat) return order[i];
        return null;
    }
}
