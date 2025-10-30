// FILE: ItemTrayService.cs
// FULL FILE (ASCII only)
// Resolves inventory/consume anchors for each seat (1..5) reliably.
// Expected layout under traysRoot:
//   Seat1
//     Inventory   (children are slots 0..N-1)
//     Consume     (children are slots 0..N-1)
//   Seat2
//     Inventory
//     Consume
//   ...
//
// Variants supported for child names (case-insensitive):
//   "Inventory" OR "Inv" OR "Items"
//   "Consume"   OR "Consumables" OR "Cons" OR "Use"
//
// If something is missing, we log clear warnings once per seat.

using UnityEngine;

[AddComponentMenu("Gameplay/Items/Item Tray Service")]
public class ItemTrayService : MonoBehaviour
{
    public static ItemTrayService Instance { get; private set; }

    [Header("Root with Seat1..Seat5")]
    public Transform traysRoot;

    // simple cache per seat
    private Transform[][] cachedInv = new Transform[6][];
    private Transform[][] cachedCon = new Transform[6][];
    private bool[] warned = new bool[6];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public bool TryGetAnchors(int seatIndex1Based, out Transform[] inventorySlots, out Transform[] consumeSlots)
    {
        inventorySlots = null;
        consumeSlots = null;

        if (seatIndex1Based < 1 || seatIndex1Based > 5)
        {
            if (!warnedSafe(seatIndex1Based))
                Debug.LogWarning("[ItemTrayService] Bad seat index: " + seatIndex1Based);
            return false;
        }

        if (traysRoot == null)
        {
            if (!warned[0])
            {
                warned[0] = true;
                Debug.LogWarning("[ItemTrayService] traysRoot is NULL.");
            }
            return false;
        }

        // return from cache if present
        if (cachedInv[seatIndex1Based] != null && cachedCon[seatIndex1Based] != null)
        {
            inventorySlots = cachedInv[seatIndex1Based];
            consumeSlots = cachedCon[seatIndex1Based];
            return inventorySlots.Length > 0 && consumeSlots.Length > 0;
        }

        // find seat transform
        Transform seat = traysRoot.Find("Seat" + seatIndex1Based);
        if (seat == null)
        {
            // try "Seat 3" (with space)
            seat = traysRoot.Find("Seat " + seatIndex1Based);
        }
        if (seat == null)
        {
            if (!warned[seatIndex1Based])
            {
                warned[seatIndex1Based] = true;
                Debug.LogWarning("[ItemTrayService] Could not find child Seat" + seatIndex1Based + " under " + traysRoot.name);
            }
            return false;
        }

        // find inventory and consume parents (case-insensitive variants)
        Transform invRoot = FindChildCI(seat, "Inventory", "Inv", "Items");
        Transform conRoot = FindChildCI(seat, "Consume", "Consumables", "Cons", "Use");

        if (invRoot == null || conRoot == null)
        {
            if (!warned[seatIndex1Based])
            {
                warned[seatIndex1Based] = true;
                Debug.LogWarning("[ItemTrayService] Seat" + seatIndex1Based + " missing roots. inv=" + (invRoot ? invRoot.name : "null") +
                                 " con=" + (conRoot ? conRoot.name : "null"));
            }
            return false;
        }

        var inv = CollectChildren(invRoot);
        var con = CollectChildren(conRoot);

        if ((inv == null || inv.Length == 0) || (con == null || con.Length == 0))
        {
            if (!warned[seatIndex1Based])
            {
                warned[seatIndex1Based] = true;
                Debug.LogWarning("[ItemTrayService] Seat" + seatIndex1Based + " has empty slots. inv=" + (inv != null ? inv.Length : 0) +
                                 " con=" + (con != null ? con.Length : 0));
            }
            return false;
        }

        cachedInv[seatIndex1Based] = inv;
        cachedCon[seatIndex1Based] = con;

        inventorySlots = inv;
        consumeSlots = con;
        return true;
    }

    private Transform[] CollectChildren(Transform root)
    {
        int n = root.childCount;
        Transform[] arr = new Transform[n];
        for (int i = 0; i < n; i++) arr[i] = root.GetChild(i);
        return arr;
    }

    private Transform FindChildCI(Transform parent, params string[] names)
    {
        if (parent == null) return null;
        int n = parent.childCount;
        for (int i = 0; i < n; i++)
        {
            var c = parent.GetChild(i);
            var cn = c.name.ToLowerInvariant();
            for (int j = 0; j < names.Length; j++)
            {
                if (cn == names[j].ToLowerInvariant()) return c;
            }
        }
        // second pass: partial contains
        for (int i = 0; i < n; i++)
        {
            var c = parent.GetChild(i);
            var cn = c.name.ToLowerInvariant();
            for (int j = 0; j < names.Length; j++)
            {
                if (cn.Contains(names[j].ToLowerInvariant())) return c;
            }
        }
        return null;
    }

    private bool warnedSafe(int seat)
    {
        if (seat < 0 || seat >= warned.Length) return warned[warned.Length - 1];
        return warned[seat];
    }
}
