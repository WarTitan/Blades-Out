// FILE: DartsHitbox.cs
// Put this on each collider that represents a scoring region.
// Example: one collider for 20 (scoreValue=20), another for 5, another for bull (25 or 50), etc.
// Set boardIndex1Based to the board this hitbox belongs to (1..5).

using UnityEngine;

[AddComponentMenu("Minigames/Darts Hitbox")]
public class DartsHitbox : MonoBehaviour
{
    [Range(1, 5)] public int boardIndex1Based = 1;
    [Tooltip("Points awarded (subtracted from board score) when hit.")]
    public int scoreValue = 20;
}
