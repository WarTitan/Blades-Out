// FILE: SeatIndexAuthority.cs
// FULL FILE (ASCII only)
//
// Server-authoritative seat assignment AFTER lobby exit.
//
// What it does:
// - Waits until LobbyStage is not active (you pressed 3 and teleported to table).
// - Then waits a few frames to let transforms settle.
// - Scans SeatAnchorTag objects (one per chair; seatIndex1Based = 1..5).
// - Picks the NEAREST FREE tagged seat and assigns PlayerItemTrays.seatIndex1Based once.
// - Never fights your seat afterwards.
//
// Requirements:
// - One SeatAnchorTag on each chair (Seat 1..5) with correct seatIndex1Based.
// - Remove any old seat scripts (SeatIndexAutoDetect, SeatIndexFromSpawnName).

using UnityEngine;
using Mirror;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Seats/Seat Index Authority (Server)")]
public class SeatIndexAuthority : NetworkBehaviour
{
    [Header("Timing")]
    public bool waitForLobbyExit = true;
    public int settleFramesAfterLobby = 8;
    public float retrySeconds = 0.25f;

    [Header("Logs")]
    public bool verboseLogs = true;

    private PlayerItemTrays trays;
    private bool assigned;

    public override void OnStartServer()
    {
        base.OnStartServer();
        trays = GetComponent<PlayerItemTrays>();
        if (trays == null)
        {
            enabled = false;
            return;
        }
        assigned = trays.seatIndex1Based > 0;
        if (!assigned) StartCoroutine(AssignRoutine());
    }

    private System.Collections.IEnumerator AssignRoutine()
    {
        // 1) Wait for lobby to end (only if desired and LobbyStage exists)
        if (waitForLobbyExit)
        {
            while (IsLobbyActive()) yield return null;
        }

        // 2) Give teleports a moment to settle
        for (int i = 0; i < settleFramesAfterLobby; i++) yield return null;

        // 3) Try until we assign
        while (isServer && !assigned && trays != null && trays.seatIndex1Based == 0)
        {
            var allTags = GameObject.FindObjectsOfType<SeatAnchorTag>();
            if (allTags == null || allTags.Length == 0)
            {
                if (verboseLogs) Debug.LogWarning("[SeatIndexAuthority] No SeatAnchorTag found. Retrying...");
                yield return new WaitForSeconds(retrySeconds);
                continue;
            }

            // Mark seats that are already taken
            bool[] taken = new bool[6]; // 1..5
            var everyone = GameObject.FindObjectsOfType<PlayerItemTrays>();
            for (int i = 0; i < everyone.Length; i++)
            {
                int s = everyone[i].seatIndex1Based;
                if (s >= 1 && s <= 5) taken[s] = true;
            }

            // Find nearest FREE tag to our current (post-teleport) position
            Transform best = null;
            int bestSeat = 0;
            float bestDist = float.MaxValue;
            Vector3 p = transform.position;

            for (int i = 0; i < allTags.Length; i++)
            {
                int seat = allTags[i].seatIndex1Based;
                if (seat < 1 || seat > 5) continue;
                if (taken[seat]) continue;

                float d = (allTags[i].transform.position - p).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestSeat = seat;
                    best = allTags[i].transform;
                }
            }

            if (bestSeat != 0)
            {
                trays.seatIndex1Based = bestSeat; // SyncVar -> replicates to clients
                assigned = true;
                if (verboseLogs)
                    Debug.Log("[SeatIndexAuthority] netId=" + netId + " assigned Seat " + bestSeat + " (nearest after lobby).");
                yield break;
            }

            if (verboseLogs) Debug.Log("[SeatIndexAuthority] No FREE seat found yet. Retrying...");
            yield return new WaitForSeconds(retrySeconds);
        }
    }

    private bool IsLobbyActive()
    {
        var ls = LobbyStage.Instance;
        if (ls == null) return false; // no lobby in this scene
        return ls.lobbyActive;
    }
}
