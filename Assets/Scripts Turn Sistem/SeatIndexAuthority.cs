// FILE: SeatIndexAuthority.cs
// FULL REPLACEMENT (ASCII only)
//
// Server-side seat assignment for each player.
// - Waits until lobby is not active (players teleported to table).
// - Finds all SeatAnchorTag objects.
// - Chooses the NEAREST anchor to this player.
// - Writes trays.seatIndex1Based = anchor.seatIndex1Based ONCE.
//
// No global "used" array, no reliance on FindObjectsOfType order.
// TurnManagerNet.autoSeatOnServer should be OFF so only this script sets seats.

using UnityEngine;
using Mirror;
using System.Collections;

[AddComponentMenu("Gameplay/Turn System/Seat Index Authority")]
public class SeatIndexAuthority : NetworkBehaviour
{
    [Header("Timing")]
    [Tooltip("Extra delay after lobby deactivates to allow teleporting to table.")]
    public float postLobbyDelay = 0.2f;

    [Tooltip("If true, seat assignment will skip the lobby check (for testing).")]
    public bool ignoreLobbyForTesting = false;

    private PlayerItemTrays trays;
    private bool seatedOnce = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        trays = GetComponent<PlayerItemTrays>();
        if (trays == null)
        {
            Debug.LogWarning("[SeatIndexAuthority] No PlayerItemTrays on " + gameObject.name);
            return;
        }

        // Only the server assigns seats.
        StartCoroutine(ServerSeatRoutine());
    }

    private IEnumerator ServerSeatRoutine()
    {
        // Small initial delay so network spawning / teleport scripts can run.
        yield return null;
        yield return new WaitForSeconds(0.05f);

        // Wait until lobby is finished (unless testing flag is on).
        if (!ignoreLobbyForTesting)
        {
            while (LobbyStage.Instance != null && LobbyStage.Instance.lobbyActive)
            {
                yield return new WaitForSeconds(0.25f);
            }
        }

        // Extra delay so PlayerSpawnManager can teleport players to their seats.
        if (postLobbyDelay > 0f)
            yield return new WaitForSeconds(postLobbyDelay);

        ServerAssignSeatByNearestAnchor();
    }

    [Server]
    private void ServerAssignSeatByNearestAnchor()
    {
        if (seatedOnce) return; // do not overwrite seats once assigned

        if (trays == null)
            trays = GetComponent<PlayerItemTrays>();
        if (trays == null)
        {
            Debug.LogWarning("[SeatIndexAuthority] No PlayerItemTrays, cannot assign seat.");
            return;
        }

#pragma warning disable CS0618
        var anchors = GameObject.FindObjectsOfType<SeatAnchorTag>();
#pragma warning restore CS0618

        if (anchors == null || anchors.Length == 0)
        {
            Debug.LogWarning("[SeatIndexAuthority] No SeatAnchorTag objects found in scene.");
            return;
        }

        Transform root = trays.transform;
        Vector3 myPos = root.position;

        SeatAnchorTag best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < anchors.Length; i++)
        {
            var a = anchors[i];
            if (a == null) continue;
            if (a.seatIndex1Based <= 0) continue;

            float dsq = (a.transform.position - myPos).sqrMagnitude;
            if (dsq < bestDistSq)
            {
                bestDistSq = dsq;
                best = a;
            }
        }

        if (best == null)
        {
            Debug.LogWarning("[SeatIndexAuthority] Could not find a suitable seat for " +
                             gameObject.name + " at pos " + myPos);
            return;
        }

        trays.seatIndex1Based = best.seatIndex1Based;
        seatedOnce = true;

        var id = GetComponent<NetworkIdentity>();
        uint nid = (id != null ? id.netId : 0);

        Debug.Log("[SeatIndexAuthority] Assigned seat " + best.seatIndex1Based +
                  " to netId=" + nid + " (distSq=" + bestDistSq + ")");
    }
}
