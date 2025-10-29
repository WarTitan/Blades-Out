// FILE: ItemTrayService.cs
// NEW FILE (ASCII only)
// Finds per-seat tray anchors in the scene. One root, named children:
// TraysRoot/Seat{seat}/Inventory (8 children) and TraysRoot/Seat{seat}/Consume (8 children).

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Items/Item Tray Service")]
public class ItemTrayService : MonoBehaviour
{
    public static ItemTrayService Instance { get; private set; }

    [Tooltip("Root that contains children like Seat1/Inventory and Seat1/Consume with 8 children each.")]
    public Transform traysRoot;

    private class SeatCache
    {
        public Transform[] inv;
        public Transform[] con;
    }

    private Dictionary<int, SeatCache> cache = new Dictionary<int, SeatCache>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public bool TryGetAnchors(int seatIndex1Based, out Transform[] inventorySlots, out Transform[] consumeSlots)
    {
        inventorySlots = null;
        consumeSlots = null;
        if (traysRoot == null) return false;

        SeatCache sc;
        if (cache.TryGetValue(seatIndex1Based, out sc))
        {
            inventorySlots = sc.inv;
            consumeSlots = sc.con;
            return (sc.inv != null && sc.con != null);
        }

        string seatName = "Seat" + seatIndex1Based;
        var seat = traysRoot.Find(seatName);
        if (seat == null) return false;

        var inv = seat.Find("Inventory");
        var con = seat.Find("Consume");
        if (inv == null || con == null) return false;

        Transform[] invArr = CollectChildren(inv, 8);
        Transform[] conArr = CollectChildren(con, 8);

        sc = new SeatCache { inv = invArr, con = conArr };
        cache[seatIndex1Based] = sc;

        inventorySlots = invArr;
        consumeSlots = conArr;
        return (invArr != null && conArr != null);
    }

    private static Transform[] CollectChildren(Transform parent, int maxExpected)
    {
        if (parent == null) return null;
        int n = parent.childCount;
        Transform[] arr = new Transform[n];
        for (int i = 0; i < n; i++) arr[i] = parent.GetChild(i);
        return arr;
    }
}
