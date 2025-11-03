// FILE: ItemTrayService.cs
// FULL FILE (ASCII only)
// Source of truth for item anchors using a single TraysRoot in the scene.
//
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
//   Inventory: "Inventory", "Inv", "Items"
//   Consume:   "Consume", "Consumables", "Cons", "Use"
//
// Notes:
// - Caches results per seat after first lookup.
// - Includes a compatibility alias TryGetAnchorsForSeat(...) for older callers.

using UnityEngine;

[AddComponentMenu("Gameplay/Items/Item Tray Service")]
public class ItemTrayService : MonoBehaviour
{
    public static ItemTrayService Instance { get; private set; }

    [Header("Root with Seat1..Seat5")]
    public Transform traysRoot;

    // cache per seat index 1..5
    private Transform[][] cachedInv = new Transform[6][];
    private Transform[][] cachedCon = new Transform[6][];
    private bool[] warned = new bool[6];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable() { Invalidate(); }
    private void OnDisable() { Invalidate(); }
    private void OnValidate() { Invalidate(); }

    private void Invalidate()
    {
        for (int i = 0; i < cachedInv.Length; i++) { cachedInv[i] = null; cachedCon[i] = null; }
        for (int i = 0; i < warned.Length; i++) warned[i] = false;
    }

    // Primary API
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

        // cache hit
        if (cachedInv[seatIndex1Based] != null && cachedCon[seatIndex1Based] != null)
        {
            inventorySlots = cachedInv[seatIndex1Based];
            consumeSlots = cachedCon[seatIndex1Based];
            return inventorySlots.Length > 0 && consumeSlots.Length > 0;
        }

        // locate seat
        Transform seat = traysRoot.Find("Seat" + seatIndex1Based);
        if (seat == null) seat = traysRoot.Find("Seat " + seatIndex1Based);
        if (seat == null)
        {
            if (!warned[seatIndex1Based])
            {
                warned[seatIndex1Based] = true;
                Debug.LogWarning("[ItemTrayService] Could not find child Seat" + seatIndex1Based + " under " + traysRoot.name);
            }
            return false;
        }

        // locate roots
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

    // Back-compat alias for older code paths
    public bool TryGetAnchorsForSeat(int seatIndex1Based, out Transform[] inventorySlots, out Transform[] consumeSlots)
    {
        return TryGetAnchors(seatIndex1Based, out inventorySlots, out consumeSlots);
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

        // exact
        for (int i = 0; i < n; i++)
        {
            var c = parent.GetChild(i);
            var cn = c.name.ToLowerInvariant();
            for (int j = 0; j < names.Length; j++)
            {
                if (cn == names[j].ToLowerInvariant()) return c;
            }
        }
        // contains
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
