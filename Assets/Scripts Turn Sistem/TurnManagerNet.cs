// FILE: TurnManagerNet.cs
// DROP-IN REPLACEMENT (ASCII only)
//
// Loop:
//   1) CRAFTING for ALL players (global timer).
//      - Any non-eliminated player can gift items.
//      - When you gift, the item moves immediately from your INVENTORY
//        into the TARGET's CONSUME tray.
//      - TurnManager remembers WHICH target each giver used this round.
//   2) DELIVERY / MEMORY phase:
//      - For seats 1..5:
//          * If that seat chose a target, and the target has items in CONSUME:
//                - Target consumes everything
//                - Target plays memory minigame
//      - After seat 5, go back to step 1 (new crafting phase).
//
// New in this version:
//   - Server_OnPlayerTraysSpawned(PlayerItemTrays) is called whenever a
//     PlayerItemTrays appears on server. If that player has 0 items,
//     they are given 'initialDraw' items immediately (even in lobby).
//     => fixes the "clone sometimes gets 0 items if I start too fast" bug.
//   - LogWaiting() now logs at most once every 3 seconds, so lobby log
//     spam is reduced.
//
// Requires:
//   - PlayerItemTrays
//   - ItemDeck
//   - MemoryStrikeTracker
//   - MemoryLevelTracker
//   - LobbyStage

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
        Turn    // Delivery / Memory
    }

    [Header("Turn Order (seats 1..5)")]
    public int[] seatTurnOrder = new int[] { 1, 2, 3, 4, 5 };

    [Header("Draw Counts")]
    [Tooltip("How many items a player gets if they start with 0 items.")]
    public int initialDraw = 4;

    [Tooltip("Currently unused, kept for compatibility.")]
    public int drawAfterTurn = 0;

    [Header("Timing")]
    [Tooltip("Duration of global crafting phase where all players can give items.")]
    public float craftingSeconds = 20f;

    [Tooltip("How long the memory minigame lasts for the target player.")]
    public float memorySeconds = 45f;

    [Header("Gating")]
    public bool ignoreLobbyForTesting = false;
    public bool autoSeatOnServer = false;

    // HUD sync
    [SyncVar] public Phase phase = Phase.Waiting;
    [SyncVar] public int currentSeat = 0;          // seat of player currently in memory
    [SyncVar] public uint currentTurnNetId = 0;    // netId of player currently in memory
    [SyncVar] public double turnEndTime = 0;       // crafting end time

    // Memory HUD sync
    [SyncVar] public bool memoryActive = false;
    [SyncVar] public double memoryEndTime = 0;

    // Internal state
    private readonly HashSet<uint> seededThisCycle = new HashSet<uint>();          // who got seeded this crafting cycle
    private readonly Dictionary<uint, uint> giftTargets = new Dictionary<uint, uint>(); // giverNetId -> targetNetId

    private bool loopStarted = false;
    private float nextWaitLog = 0f;

    // Memory flow
    private int memoryToken = 0;
    private uint memoryPlayerNetId = 0;
    private bool memoryResultArrived = false;

    // Kept for compatibility with TurnInput (no behavior now)
#pragma warning disable CS0618
    private bool turnForceEnd = false;
#pragma warning restore CS0618

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
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

    // Called by TurnInput (hotkey 1) in older versions. Currently a no-op.
    [Server]
    public void Server_EndCurrentTurnBy(uint requesterNetId)
    {
        // If you ever want "skip to next seat" hotkey, implement here.
        // Left as no-op so existing TurnInput keeps compiling.
    }

    // Called from PlayerItemTrays.OnStartServer when a tray spawns on server.
    // Ensures a new player does NOT start with 0 items even if lobby ends quickly.
    [Server]
    public void Server_OnPlayerTraysSpawned(PlayerItemTrays trays)
    {
        if (trays == null) return;
        if (trays.inventory.Count > 0) return;

        int added = trays.Server_AddSeveralToInventoryClamped(initialDraw);
        Debug.Log("[TurnManagerNet] Spawn seed: seat=" + trays.seatIndex1Based +
                  " added=" + added + " items (initialDraw=" + initialDraw + ")");
    }

    // Allow trading ONLY during crafting, and only for non-eliminated players.
    [Server]
    public bool CanTradeNow(NetworkIdentity who)
    {
        if (who == null) return false;

        var e = MemoryStrikeTracker.FindForNetId(who.netId);
        if (e != null && e.eliminated) return false;

        if (phase == Phase.Crafting)
            return true;

        return false;
    }

    // Called by PlayerItemTrays.Cmd_GiveItemToPlayer when a gift happens.
    // Locks which target this giver is using for the current round.
    [Server]
    public bool Server_OnGift(uint giverNetId, uint targetNetId)
    {
        if (phase != Phase.Crafting) return false;
        if (giverNetId == 0 || targetNetId == 0) return false;

        uint existing;
        if (giftTargets.TryGetValue(giverNetId, out existing))
        {
            // Already chosen someone this round: must gift to the same person.
            if (existing != targetNetId)
            {
                Debug.LogWarning("[TurnManagerNet] Giver " + giverNetId +
                                 " tried to change target in same crafting phase. Gift rejected.");
                return false;
            }
        }
        else
        {
            giftTargets[giverNetId] = targetNetId;
            Debug.Log("[TurnManagerNet] Giver " + giverNetId + " locked target " + targetNetId + " for this round.");
        }

        return true;
    }

    // ---------------- Main Loop ----------------

    private IEnumerator ServerMainLoop()
    {
        yield return new WaitForSeconds(0.25f);

        while (true)
        {
            // WAITING FOR A VALID GAME STATE
            if (!HasAnyPlayers())
            {
                LogWaiting("no players found");
                SetWaiting();
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            // Only auto-seat if SeatIndexAuthority is NOT present.
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

            // ----------------------------------------------------
            // 1) CRAFTING PHASE FOR ALL PLAYERS
            // ----------------------------------------------------
            phase = Phase.Crafting;
            currentSeat = 0;
            currentTurnNetId = 0;
            memoryActive = false;
            memoryEndTime = 0;

            seededThisCycle.Clear();
            giftTargets.Clear();

            Server_LogSeatMap("pre-crafting");
            Server_SeedSeatedIfEmpty("crafting start");

            double craftEnd = NetworkTime.time + craftingSeconds;
            turnEndTime = craftEnd;

            Debug.Log("[TurnManagerNet] Crafting phase started for ALL players (" + craftingSeconds + "s).");

            while (NetworkTime.time < craftEnd)
                yield return null;

            // ----------------------------------------------------
            // 2) DELIVERY / MEMORY PHASE (SEATS 1..5)
            // ----------------------------------------------------
            phase = Phase.Turn;
            turnEndTime = 0;
            currentSeat = 0;
            currentTurnNetId = 0;

            var order = BuildOrderSkippingEliminated();
            if (order.Count == 0)
            {
                LogWaiting("no active players for delivery");
                SetWaiting();
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            for (int i = 0; i < seatTurnOrder.Length; i++)
            {
                var giver = ResolveBySeat(order, seatTurnOrder[i]);
                if (giver == null) continue;

                var giverId = giver.GetComponent<NetworkIdentity>();
                if (giverId == null) continue;
                if (IsEliminated(giverId.netId)) continue;

                uint targetNetId;
                if (!giftTargets.TryGetValue(giverId.netId, out targetNetId) || targetNetId == 0)
                {
                    // This seat did not give anything this round.
                    Debug.Log("[TurnManagerNet] Seat " + giver.seatIndex1Based + " had no target this round.");
                    continue;
                }

                var targetId = FindNetIdentity(targetNetId);
                if (targetId == null)
                {
                    Debug.LogWarning("[TurnManagerNet] Target netId " + targetNetId +
                                     " for giver netId " + giverId.netId + " not found. Skipping.");
                    continue;
                }

                var targetTrays = targetId.GetComponent<PlayerItemTrays>();
                if (targetTrays == null) continue;
                if (IsEliminated(targetId.netId))
                {
                    Debug.Log("[TurnManagerNet] Target netId " + targetId.netId +
                              " is eliminated; skipping delivery for this seat.");
                    continue;
                }

                int targetSeat = targetTrays.seatIndex1Based;
                if (targetSeat <= 0)
                {
                    Debug.Log("[TurnManagerNet] Target has no seat; skipping.");
                    continue;
                }

                // HUD: show the RECEIVING player
                currentSeat = targetSeat;
                currentTurnNetId = targetId.netId;

                Debug.Log("[TurnManagerNet] Delivery: Seat " + giver.seatIndex1Based +
                          " -> Seat " + targetSeat +
                          " (giver netId " + giverId.netId + " -> target netId " + targetId.netId + ")");

                int before = targetTrays.consume.Count;
                targetTrays.Server_ConsumeAllNow();

                if (before <= 0)
                {
                    Debug.Log("[TurnManagerNet] Target seat " + targetSeat +
                              " had no items to consume; skipping memory.");
                    currentSeat = 0;
                    currentTurnNetId = 0;
                    continue;
                }

                // Play memory minigame for the target.
                yield return Server_PlayMemory(targetId, targetSeat);

                currentSeat = 0;
                currentTurnNetId = 0;

                yield return new WaitForSeconds(0.1f);
            }

            // After seats 1..5, loop back to crafting.
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
            nextWaitLog = Time.time + 3f; // log at most once every 3 seconds
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
            Debug.Log("[TurnManagerNet] Memory FAIL -> strikes " +
                      (e != null ? e.strikes : 0) + "/" + MemoryStrikeTracker.MaxStrikes +
                      " (netId " + netId + ")");
        }
        else
        {
            Debug.LogWarning("[TurnManagerNet] MemoryStrikeTracker missing; cannot add strike.");
        }
    }

    // ---------------- Helper checks ----------------

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

    // Draw items for any seated player with 0 inventory at the start of crafting.
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
            Debug.LogError("[TurnManagerNet] ItemDeck has 0 entries. Populate items.");
        }

        for (int i = 0; i < traysAll.Length; i++)
        {
            var t = traysAll[i];
            if (t == null) continue;
            if (t.seatIndex1Based <= 0) continue; // only seated players

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

    [Server]
    private NetworkIdentity FindNetIdentity(uint netId)
    {
        if (netId == 0) return null;

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<NetworkIdentity>();
#endif
        for (int i = 0; i < all.Length; i++)
            if (all[i].netId == netId) return all[i];
        return null;
    }
}
