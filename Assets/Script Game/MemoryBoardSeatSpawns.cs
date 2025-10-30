// FILE: MemoryBoardSeatSpawns.cs
// FULL FILE (ASCII only)
//
// Maps seat indices (1..5) to world anchors for the memory board.
// You can either:
//   A) Assign seatAnchors[1..5] in the inspector, OR
//   B) Leave them null and place Transforms named "SeatBoard_1..5" in the scene.
//
// Provides: GetSeatAnchor(int seat)
// Also exposes boardScale, offset and alignment options used by MemorySequenceUI.

using UnityEngine;

[AddComponentMenu("Gameplay/Memory/Memory Board Seat Spawns")]
public class MemoryBoardSeatSpawns : MonoBehaviour
{
    public static MemoryBoardSeatSpawns Instance;

    public enum OffsetMode { None, World, Local }

    [Header("Seat Anchors (index 1..5 used)")]
    public Transform[] seatAnchors = new Transform[6];

    [Header("Board Placement")]
    public float boardScale = 1.0f;
    public Vector3 offset = new Vector3(0f, 1.0f, 1.5f);
    public OffsetMode offsetMode = OffsetMode.Local;
    public bool alignWithSeatAnchorForward = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public Transform GetSeatAnchor(int seatIndex1Based)
    {
        if (seatIndex1Based < 1 || seatIndex1Based > 5) seatIndex1Based = 1;

        // Prefer explicitly assigned anchor
        var a = seatAnchors != null && seatAnchors.Length > seatIndex1Based
              ? seatAnchors[seatIndex1Based]
              : null;
        if (a != null) return a;

        // Fallback: find by name
        string name = "SeatBoard_" + seatIndex1Based;
        var go = GameObject.Find(name);
        if (go != null) return go.transform;

        // Not found
        return null;
    }
}
