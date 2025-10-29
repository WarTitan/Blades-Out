// FILE: ItemTrayService.cs
// FULL REPLACEMENT (ASCII only)
// More tolerant seat/anchor lookup:
// - Finds SeatN even if named "Seat N" or nested deep under traysRoot
// - Accepts "Inventory"/"Inv"/"Items" and "Consume"/"Consumables"/"Cons"/"ToConsume"/"Use"
// - Detailed logs showing what was found when anchors are missing

using UnityEngine;
using System.Collections.Generic;
using System.Text;

[AddComponentMenu("Gameplay/Items/Item Tray Service")]
public class ItemTrayService : MonoBehaviour
{
    public static ItemTrayService Instance { get; private set; }

    [Tooltip("Root that contains seat transforms (Seat1..Seat5). They may be nested; deep search is used.")]
    public Transform traysRoot;

    private class SeatCache
    {
        public Transform seat;
        public Transform invRoot;
        public Transform conRoot;
        public Transform[] invSlots;
        public Transform[] conSlots;
    }

    private readonly Dictionary<int, SeatCache> cache = new Dictionary<int, SeatCache>();

    // Accepted names (case-insensitive)
    private static readonly string[] InvNames = new string[] { "Inventory", "Inv", "Items" };
    private static readonly string[] ConNames = new string[] { "Consume", "Consumables", "Cons", "ToConsume", "Use" };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void ClearCache()
    {
        cache.Clear();
    }

    public bool TryGetAnchors(int seatIndex1Based, out Transform[] inventorySlots, out Transform[] consumeSlots)
    {
        inventorySlots = null;
        consumeSlots = null;

        if (traysRoot == null)
        {
            Debug.LogWarning("[ItemTrayService] traysRoot is null.");
            return false;
        }
        if (seatIndex1Based <= 0)
        {
            Debug.Log("[ItemTrayService] Requested seat <= 0; not resolving yet.");
            return false;
        }

        SeatCache sc;
        if (!cache.TryGetValue(seatIndex1Based, out sc))
        {
            sc = BuildSeatCache(seatIndex1Based);
            if (sc != null) cache[seatIndex1Based] = sc;
        }

        if (sc == null || sc.invRoot == null || sc.conRoot == null)
        {
            DebugMissingForSeat(seatIndex1Based, sc);
            return false;
        }

        inventorySlots = sc.invSlots;
        consumeSlots = sc.conSlots;
        if ((inventorySlots == null || inventorySlots.Length == 0) ||
            (consumeSlots == null || consumeSlots.Length == 0))
        {
            DebugLogSlotsEmpty(seatIndex1Based, sc);
        }
        return (inventorySlots != null && consumeSlots != null);
    }

    private SeatCache BuildSeatCache(int seatIndex)
    {
        var seat = FindSeatDeep(traysRoot, seatIndex);
        if (seat == null)
        {
            Debug.LogWarning("[ItemTrayService] Seat" + seatIndex + " not found under traysRoot (deep search).");
            return null;
        }

        var invRoot = FindChildByNamesDeep(seat, InvNames);
        var conRoot = FindChildByNamesDeep(seat, ConNames);

        var invSlots = CollectChildren(invRoot);
        var conSlots = CollectChildren(conRoot);

        return new SeatCache
        {
            seat = seat,
            invRoot = invRoot,
            conRoot = conRoot,
            invSlots = invSlots,
            conSlots = conSlots
        };
    }

    private static Transform[] CollectChildren(Transform root)
    {
        if (root == null) return null;
        int n = root.childCount;
        Transform[] arr = new Transform[n];
        for (int i = 0; i < n; i++) arr[i] = root.GetChild(i);
        return arr;
    }

    // Deep search for "SeatX" or "Seat X", case-insensitive
    private static Transform FindSeatDeep(Transform root, int seatIndex)
    {
        if (root == null) return null;
        string wantA = "seat" + seatIndex;
        string wantB = "seat " + seatIndex;

        Queue<Transform> q = new Queue<Transform>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            var t = q.Dequeue();
            var nm = t.name.Trim();
            var nmLower = nm.ToLowerInvariant();
            if (nmLower == wantA || nmLower == wantB)
                return t;

            for (int i = 0; i < t.childCount; i++)
                q.Enqueue(t.GetChild(i));
        }
        return null;
    }

    // Deep search for any of the accepted names (case-insensitive)
    private static Transform FindChildByNamesDeep(Transform parent, string[] names)
    {
        if (parent == null) return null;

        HashSet<string> set = new HashSet<string>();
        for (int i = 0; i < names.Length; i++) set.Add(names[i].ToLowerInvariant());

        Queue<Transform> q = new Queue<Transform>();
        q.Enqueue(parent);
        while (q.Count > 0)
        {
            var t = q.Dequeue();
            string nm = t.name.Trim().ToLowerInvariant();
            if (set.Contains(nm)) return t;

            for (int i = 0; i < t.childCount; i++)
                q.Enqueue(t.GetChild(i));
        }
        return null;
    }

    private void DebugMissingForSeat(int seatIndex, SeatCache sc)
    {
        var sb = new StringBuilder();
        sb.Append("[ItemTrayService] No anchors for Seat").Append(seatIndex).Append(". ");

        var seat = (sc != null) ? sc.seat : FindSeatDeep(traysRoot, seatIndex);
        if (seat == null)
        {
            sb.Append("Seat not found. Children under traysRoot: ");
            ListChildrenNames(traysRoot, sb, maxPerParent: 20);
            Debug.LogWarning(sb.ToString());
            return;
        }

        sb.Append("Found seat: ").Append(seat.name).Append(". ");
        if (sc == null || sc.invRoot == null) sb.Append("Missing Inventory root. ");
        if (sc == null || sc.conRoot == null) sb.Append("Missing Consume root. ");

        sb.Append("Direct children of seat: ");
        ListDirectChildrenNames(seat, sb, max: 20);

        Debug.LogWarning(sb.ToString());
    }

    private void DebugLogSlotsEmpty(int seatIndex, SeatCache sc)
    {
        var sb = new StringBuilder();
        sb.Append("[ItemTrayService] Seat").Append(seatIndex).Append(": ");
        if (sc.invRoot != null)
        {
            sb.Append("Inventory children=").Append(sc.invRoot.childCount).Append(". ");
        }
        if (sc.conRoot != null)
        {
            sb.Append("Consume children=").Append(sc.conRoot.childCount).Append(". ");
        }
        Debug.Log(sb.ToString());
    }

    private static void ListDirectChildrenNames(Transform t, StringBuilder sb, int max)
    {
        if (t == null) return;
        int n = Mathf.Min(max, t.childCount);
        for (int i = 0; i < n; i++)
        {
            if (i == 0) sb.Append("[");
            sb.Append(t.GetChild(i).name);
            if (i < n - 1) sb.Append(", ");
            if (i == n - 1) sb.Append("]");
        }
    }

    private static void ListChildrenNames(Transform t, StringBuilder sb, int maxPerParent)
    {
        if (t == null) return;
        sb.Append(t.name).Append(": ");
        ListDirectChildrenNames(t, sb, maxPerParent);
    }
}
