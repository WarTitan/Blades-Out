// FILE: TurnManagerNet.cs
// FULL FILE (ASCII only)
//
// Trade window removed: after memory ends, the current player can trade indefinitely
// until they manually end their turn via Server_EndCurrentTurnBy(requesterNetId).
//
// Flow per seated, non-eliminated player:
//   1) Auto-consume their CONSUME list
//   2) Play memory minigame (strike on fail, eliminate at 3)
//   3) Free trading period (no timer) -> Wait until player ends turn
//   4) Draw after turn
//
// Notes:
// - CanTradeNow() now ONLY checks: phase==Turn, who.netId==currentTurnNetId, !memoryActive, not eliminated.
// - HUD fields like turnEndTime remain but are not used (set to 0).
// - Seeding still happens: initialDraw at cycle start (if empty), drawAfterTurn at step 4.
// - Target_StartMemoryGame sends seat index so client can spawn board at the correct seat.

using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Turn Manager Net")]
public class TurnManagerNet : NetworkBehaviour
{
    public static TurnManagerNet Instance { get; private set; }

    public enum Phase { Waiting, Turn }

    [Header("Turn Order (seats 1..5)")]
    public int[] seatTurnOrder = new int[] { 1, 2, 3, 4, 5 };

    [Header("Draw Counts")]
    public int initialDraw = 4;
    public int drawAfterTurn = 2;

    [Header("Timing")]
    public float memorySeconds = 45f;
    // trade timer removed; keeping the field for compatibility but unused
    public float turnSeconds = 0f;

    [Header("Gating")]
    public bool ignoreLobbyForTesting = false;
    public bool autoSeatOnServer = false;

    // HUD sync
    [SyncVar] public Phase phase = Phase.Waiting;
    [SyncVar] public int currentSeat = 0;
    [SyncVar] public uint currentTurnNetId = 0;
    [SyncVar] public double turnEndTime = 0; // unused now (kept for HUD compatibility)

    // Memory HUD sync
    [SyncVar] public bool memoryActive = false;
    [SyncVar] public double memoryEndTime = 0;

    // Internal
    private readonly HashSet<uint> seededThisCycle = new HashSet<uint>();
    private bool loopStarted = false;
    private float nextWaitLog = 0f;

    // Memory state
    private int memoryToken = 0;
    private uint memoryPlayerNetId = 0;
    private bool memoryResultArrived = false;

    // Manual end-turn flag
    private bool turnForceEnd = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
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

    // Called by hotkey/UI on the current player to finish their turn
    [Server]
    public void Server_EndCurrentTurnBy(uint requesterNetId)
    {
        if (phase != Phase.Turn) return;
        if (requesterNetId == 0) return;
        if (requesterNetId != currentTurnNetId) return;
        turnForceEnd = true;
    }

    // Allow trading when it is THIS player's turn, memory is not running, and the player is not eliminated.
    [Server]
    public bool CanTradeNow(NetworkIdentity who)
    {
        if (phase != Phase.Turn) return false;
        if (who == null) return false;
        if (who.netId != currentTurnNetId) return false;
        if (memoryActive) return false;
        var e = MemoryStrikeTracker.FindForNetId(who.netId);
        if (e != null && e.eliminated) return false;
        // No time gating anymore
        return true;
    }

    // ---------------- Main Loop ----------------

    private IEnumerator ServerMainLoop()
    {
        yield return new WaitForSeconds(0.25f);

        while (true)
        {
            if (!HasAnyPlayers())
            {
                LogWaiting("no players found");
                SetWaiting();
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            // IMPORTANT: do NOT auto-seat if SeatIndexAuthority is present.
            // That script assigns seats by nearest chair after lobby.
            if (autoSeatOnServer && !HasSeatIndexAuthority())
            {
                Server_AutoSeatAllPlayersIfNeeded();
            }

            if (IsLobbyBlockingStart())
            {
                LogWaiting("lobbyActive == true");
                SetWaiting();
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            if (!HasAnySeatedPlayer())
            {
                LogWaiting("no seated players (seatIndex1Based == 0)");
                SetWaiting();
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            // ---- ENTER TURN PHASE ----
            phase = Phase.Turn;
            seededThisCycle.Clear();

            Server_LogSeatMap("pre-seed");
            Server_SeedSeatedIfEmpty("cycle start");

            while (true)
            {
                var order = BuildOrderSkippingEliminated();
                if (order.Count == 0)
                {
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

                    if (IsEliminated(currentTurnNetId)) continue;

                    // Safety: if still empty, seed now
                    if (id != null && p.inventory.Count == 0 && !seededThisCycle.Contains(id.netId))
                    {
                        int added = p.Server_AddSeveralToInventoryClamped(initialDraw);
                        seededThisCycle.Add(id.netId);
                        Debug.Log("[TurnManagerNet] Safety seed Seat " + p.seatIndex1Based + " netId=" + id.netId + " added=" + added);
                    }

                    // 1) Auto-consume
                    p.Server_ConsumeAllNow();

                    // 2) Memory (send seat in RPC)
                    if (id != null)
                        yield return Server_PlayMemory(id, p.seatIndex1Based);

                    // Player might be eliminated by strikes
                    if (IsEliminated(currentTurnNetId))
                    {
                        Debug.Log("[TurnManagerNet] Eliminated after memory. Skipping trade.");
                        yield return new WaitForSeconds(0.1f);
                        continue;
                    }

                    // 3) Trading period (NO TIMER) — wait for manual end
                    turnForceEnd = false;
                    turnEndTime = 0; // not used anymore
                    Debug.Log("[TurnManagerNet] Trading open for Seat " + p.seatIndex1Based + " (manual end required).");
                    while (!turnForceEnd) yield return null;

                    // 4) Draw after turn
                    p.Server_AddSeveralToInventoryClamped(drawAfterTurn);

                    yield return new WaitForSeconds(0.1f);
                }
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
            nextWaitLog = Time.time + 1f;
            Debug.Log("[TurnManagerNet] Waiting: " + why);
        }
    }

    // ---------------- Memory flow ----------------

    [Server]
    private IEnumerator Server_PlayMemory(NetworkIdentity playerId, int seatIndex1Based)
    {
        memoryPlayerNetId = playerId.netId;
        memoryToken = Random.Range(100000, 999999);
        memoryResultArrived = false;

        memoryActive = true;
        memoryEndTime = NetworkTime.time + memorySeconds;

        Debug.Log("[TurnManagerNet] Start memory for netId=" + playerId.netId + " seat=" + seatIndex1Based);

        if (playerId.connectionToClient != null)
            Target_StartMemoryGame(playerId.connectionToClient, memoryToken, memorySeconds, seatIndex1Based);

        while (!memoryResultArrived && NetworkTime.time < memoryEndTime) yield return null;

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
            Debug.Log("[TurnManagerNet] Memory FAIL -> strike " + (e != null ? e.strikes : 0) + "/" + MemoryStrikeTracker.MaxStrikes + " (netId " + netId + ")");
        }
        else
        {
            Debug.LogWarning("[TurnManagerNet] MemoryStrikeTracker missing; cannot add strike.");
        }
    }

    // ---------------- Helpers ----------------

    private bool IsLobbyBlockingStart()
    {
        if (ignoreLobbyForTesting) return false;
        var ls = LobbyStage.Instance;
        if (ls == null) return true;
        return ls.lobbyActive;
    }

    private bool HasAnyPlayers()
    {
#pragma warning disable CS0618
        var trays = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
        return trays != null && trays.Length > 0;
    }

#pragma warning disable CS0618
    private bool HasAnySeatedPlayer()
    {
        var trays = GameObject.FindObjectsOfType<PlayerItemTrays>();
        for (int i = 0; i < trays.Length; i++)
            if (trays[i].seatIndex1Based > 0) return true;
        return false;
    }
#pragma warning restore CS0618

    // NEW: detect if SeatIndexAuthority exists in this scene.
    // If it does, we do NOT auto-seat from TurnManagerNet.
    private bool HasSeatIndexAuthority()
    {
#pragma warning disable CS0618
        var seats = GameObject.FindObjectsOfType<SeatIndexAuthority>();
#pragma warning restore CS0618
        return seats != null && seats.Length > 0;
    }

    [Server]
    private void Server_AutoSeatAllPlayersIfNeeded()
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
        if (traysAll == null || traysAll.Length == 0) return;

        bool[] used = new bool[6]; // 1..5
        for (int i = 0; i < traysAll.Length; i++)
        {
            int s = traysAll[i].seatIndex1Based;
            if (s >= 1 && s <= 5) used[s] = true;
        }

        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t.seatIndex1Based > 0) continue;

            int free = FindFirstFreeSeat(used);
            if (free == 0) break;
            t.seatIndex1Based = free;
            used[free] = true;
            Debug.Log("[TurnManagerNet] Auto-seated player at Seat " + free);
        }
    }

    private int FindFirstFreeSeat(bool[] used)
    {
        for (int s = 1; s <= 5; s++)
            if (!used[s]) return s;
        return 0;
    }

    [Server]
    private void Server_SeedSeatedIfEmpty(string label)
    {
#pragma warning disable CS0618
        var traysAll = GameObject.FindObjectsOfType<PlayerItemTrays>();
        var deck = Object.FindObjectOfType<ItemDeck>();
#pragma warning restore CS0618

        if (deck == null)
        {
            Debug.LogError("[TurnManagerNet] No ItemDeck found in scene. Seeding will add 0 items.");
        }
        else if (deck.Count <= 0)
        {
            Debug.LogError("[TurnManagerNet] ItemDeck has 0 entries. Populate items or enable auto-fill.");
        }

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
            Debug.Log("[TurnManagerNet] SeatMap(" + tag + "): netId=" + nid + " seat=" + t.seatIndex1Based + " inv=" + t.inventory.Count);
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
