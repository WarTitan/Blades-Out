// FILE: SeatAnchorTag.cs
// FULL FILE (ASCII only)
//
// Put this on each chair anchor and set seatIndex1Based to the REAL seat number (1..5).
// This number is the single source of truth for seats.

using UnityEngine;

[AddComponentMenu("Gameplay/Seats/Seat Anchor Tag")]
public class SeatAnchorTag : MonoBehaviour
{
    [Range(1, 5)]
    public int seatIndex1Based = 1;
}
