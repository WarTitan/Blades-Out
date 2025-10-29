// FILE: TurnManagerNet.cs
// FULL REPLACEMENT (ASCII only)
// Server-authoritative turn loop. Seat order is 1 -> 5.
// With your spawn mapping, that results in player order:
// seat1=Player4, seat2=Player2, seat3=Player1, seat4=Player3, seat5=Player5.

using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Turn Manager Net")]
public class TurnManagerNet : NetworkBehaviour
{
    public static TurnManagerNet Instance { get; private set; }

    // Seat indices (1-based) in the order turns should proceed.
    public int[] seatTurnOrder = new int[] { 1, 2, 3, 4, 5 };

    [Header("Draw Counts")]
    public int drawAtTurnStart = 4;   // clamped to tray capacity
    public int drawAfterMinigame = 2;

    [Header("Timing")]
    public float preConsumeDelay = 0.4f;
    public float postConsumeDelay = 0.4f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(ServerTurnLoop());
    }

    private IEnumerator ServerTurnLoop()
    {
        // small settle time for spawns/seat assignment
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            List<PlayerItemTrays> order = BuildCurrentOrder();
            if (order.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            for (int i = 0; i < order.Count; i++)
            {
                var p = order[i];
                if (p == null || !p.isActiveAndEnabled) continue;

                // Turn start: draw up to capacity
                p.Server_AddSeveralToInventoryClamped(drawAtTurnStart);

                yield return new WaitForSeconds(preConsumeDelay);

                // Auto-consume anything in the consume tray
                p.Server_ConsumeAllNow();

                // TODO: minigame here. For now, just delay to simulate.
                yield return new WaitForSeconds(1.0f);

                // After minigame: grant extra items
                p.Server_AddSeveralToInventoryClamped(drawAfterMinigame);

                yield return new WaitForSeconds(postConsumeDelay);
            }
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
                result.Add(t); // skips missing seats automatically
        }
        return result;
    }
}
