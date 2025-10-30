// FILE: PlayerItemTrays.cs
// FULL FILE (ASCII only)
//
// What this fixes:
// - If seat anchors are not found, it FALLS BACK to the SeatAnchorTag of your seat and
//   spawns local temp anchors there so items are still visible.
// - Adds loud logs on client when visuals are rebuilt (seat, counts, anchor source).
// - Keeps Cmd_GiveItemToPlayer so ItemInteraction.cs compiles.
// - Does not touch seat logic (SeatIndexAuthority sets seatIndex1Based).
//
// Note: Server logs show draw happened; this ensures clients always render something.

using UnityEngine;
using Mirror;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Items/Player Item Trays")]
public class PlayerItemTrays : NetworkBehaviour
{
    public const int MaxSlots = 8;

    [Header("Seating")]
    [SyncVar(hook = nameof(OnSeatChanged))]
    public int seatIndex1Based = 0; // 1..5

    [Header("Visuals")]
    public float itemScale = 0.9f;
    public Vector3 inventoryOffset = Vector3.zero;
    public Vector3 consumeOffset = Vector3.zero;

    [Header("Debug")]
    public bool debugLogs = true;

    public class IntList : SyncList<int> { }
    public IntList inventory = new IntList();
    public IntList consume = new IntList();

    private readonly List<GameObject> invVisuals = new List<GameObject>(MaxSlots);
    private readonly List<GameObject> conVisuals = new List<GameObject>(MaxSlots);

    private Transform[] invAnchors;
    private Transform[] conAnchors;

    // -------- Mirror lifecycle --------

    public override void OnStartClient()
    {
        base.OnStartClient();
        RebindAnchors();
        inventory.Callback += OnInventoryChanged;
        consume.Callback += OnConsumeChanged;

        // Initial build (in case items already present when we spawned)
        RebuildInventoryVisuals();
        RebuildConsumeVisuals();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        inventory.Callback -= OnInventoryChanged;
        consume.Callback -= OnConsumeChanged;
        ClearVisuals(invVisuals);
        ClearVisuals(conVisuals);
    }

    private void OnSeatChanged(int oldSeat, int newSeat)
    {
        if (debugLogs) Debug.Log("[PlayerItemTrays] Client seat changed " + oldSeat + " -> " + newSeat);
        RebindAnchors();
        RebuildInventoryVisuals();
        RebuildConsumeVisuals();
    }

    private void RebindAnchors()
    {
        invAnchors = null;
        conAnchors = null;

        // 1) Preferred: from ItemTrayService (scene layout)
        var svc = ItemTrayService.Instance;
        if (svc != null && seatIndex1Based > 0)
        {
            Transform[] inv, con;
            if (svc.TryGetAnchors(seatIndex1Based, out inv, out con))
            {
                invAnchors = inv;
                conAnchors = con;
                if (debugLogs) Debug.Log("[PlayerItemTrays] Seat " + seatIndex1Based + " bound anchors from ItemTrayService. inv=" +
                                         invAnchors.Length + " con=" + conAnchors.Length);
                return;
            }
        }

        // 2) Fallback: spawn temporary anchors at the SeatAnchorTag (world seat) so items are still visible
        var sat = FindSeatAnchorTag(seatIndex1Based);
        if (sat != null)
        {
            var invRoot = new GameObject("TempInvRoot_Seat" + seatIndex1Based).transform;
            invRoot.SetParent(sat.transform, false);
            var conRoot = new GameObject("TempConRoot_Seat" + seatIndex1Based).transform;
            conRoot.SetParent(sat.transform, false);

            int defaultSlots = 8;
            invAnchors = new Transform[defaultSlots];
            conAnchors = new Transform[defaultSlots];
            for (int i = 0; i < defaultSlots; i++)
            {
                var a = new GameObject("Inv_Slot_" + i).transform;
                a.SetParent(invRoot, false);
                a.localPosition = new Vector3(i * 0.25f, 0.0f, 0.0f);

                var b = new GameObject("Con_Slot_" + i).transform;
                b.SetParent(conRoot, false);
                b.localPosition = new Vector3(i * 0.25f, -0.25f, 0.0f);

                invAnchors[i] = a;
                conAnchors[i] = b;
            }

            if (debugLogs) Debug.Log("[PlayerItemTrays] Seat " + seatIndex1Based + " using FALLBACK anchors at SeatAnchorTag.");
            return;
        }

        if (debugLogs) Debug.LogWarning("[PlayerItemTrays] No anchors and no SeatAnchorTag found for seat " + seatIndex1Based + ". Will spawn under player object.");
    }

    private SeatAnchorTag FindSeatAnchorTag(int seat)
    {
        if (seat < 1) return null;
        var all = GameObject.FindObjectsOfType<SeatAnchorTag>();
        for (int i = 0; i < all.Length; i++)
            if (all[i].seatIndex1Based == seat) return all[i];
        return null;
    }

    // -------- SyncList callbacks -> visuals --------

    private void OnInventoryChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        if (debugLogs)
            Debug.Log("[PlayerItemTrays] Client inventory changed (seat " + seatIndex1Based + "): op=" + op + " count=" + inventory.Count);
        RebuildInventoryVisuals();
    }

    private void OnConsumeChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        if (debugLogs)
            Debug.Log("[PlayerItemTrays] Client consume changed (seat " + seatIndex1Based + "): op=" + op + " count=" + consume.Count);
        RebuildConsumeVisuals();
    }

    private void RebuildInventoryVisuals()
    {
        ClearVisuals(invVisuals);
        int count = Mathf.Min(inventory.Count, MaxSlots);
        EnsureAnchorArray(ref invAnchors, count, true);
        for (int i = 0; i < count; i++)
        {
            int itemId = inventory[i];
            var go = SpawnItemVisual(itemId, GetAnchor(invAnchors, i), true, i);
            invVisuals.Add(go);
            if (debugLogs && go != null)
            {
                var wp = go.transform.position;
                Debug.Log("[PlayerItemTrays] Spawned INV item " + itemId + " at " + wp.ToString("F3") + " seat=" + seatIndex1Based);
            }
        }
    }

    private void RebuildConsumeVisuals()
    {
        ClearVisuals(conVisuals);
        int count = Mathf.Min(consume.Count, MaxSlots);
        EnsureAnchorArray(ref conAnchors, count, false);
        for (int i = 0; i < count; i++)
        {
            int itemId = consume[i];
            var go = SpawnItemVisual(itemId, GetAnchor(conAnchors, i), false, i);
            conVisuals.Add(go);
            if (debugLogs && go != null)
            {
                var wp = go.transform.position;
                Debug.Log("[PlayerItemTrays] Spawned CON item " + itemId + " at " + wp.ToString("F3") + " seat=" + seatIndex1Based);
            }
        }
    }

    private void ClearVisuals(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }

    // -------- Server API (draw/consume) --------

    [Server]
    public int Server_AddSeveralToInventoryClamped(int count)
    {
        if (count <= 0) return 0;

        int added = 0;
        var deck = FindObjectOfType<ItemDeck>();
        if (deck == null)
        {
            Debug.LogError("[PlayerItemTrays] No ItemDeck in scene. Cannot draw items.");
            return 0;
        }
        if (deck.Count <= 0)
        {
            Debug.LogError("[PlayerItemTrays] ItemDeck has 0 entries. Populate items first (or enable auto-fill).");
            return 0;
        }

        List<int> justAdded = null;

        while (added < count && inventory.Count < MaxSlots)
        {
            int id = deck.DrawRandomId();
            if (id < 0) break;
            inventory.Add(id);
            if (justAdded == null) justAdded = new List<int>(count);
            justAdded.Add(id);
            added++;
        }

        if (added > 0)
        {
            string s = "[PlayerItemTrays] Seat " + seatIndex1Based + " drew " + added + " item(s)";
            if (justAdded != null) s += " ids=[" + string.Join(",", justAdded) + "]";
            Debug.Log(s);
        }

        return added;
    }

    [Server]
    public void Server_ConsumeAllNow()
    {
        if (consume.Count > 0)
            Debug.Log("[PlayerItemTrays] Server_ConsumeAllNow seat=" + seatIndex1Based + " count=" + consume.Count);

        consume.Clear();
        RpcClearConsumeVisuals();
    }

    // -------- Trading (Command used by ItemInteraction) --------

    [Command]
    public void Cmd_GiveItemToPlayer(uint targetNetId, int fromSlotIndex)
    {
        var senderId = GetComponent<NetworkIdentity>();
        if (senderId == null) return;

        var tm = TurnManagerNet.Instance;
        if (tm == null) return;
        if (!tm.CanTradeNow(senderId)) return;

        if (fromSlotIndex < 0 || fromSlotIndex >= inventory.Count) return;

        NetworkIdentity targetIdentity = FindNetIdentity(targetNetId);
        if (targetIdentity == null) return;

        var targetTrays = targetIdentity.GetComponent<PlayerItemTrays>();
        if (targetTrays == null) return;
        if (targetTrays.inventory.Count >= MaxSlots) return;

        int itemId = inventory[fromSlotIndex];
        inventory.RemoveAt(fromSlotIndex);
        targetTrays.inventory.Add(itemId);

        Debug.Log("[PlayerItemTrays] Trade seat " + seatIndex1Based + " -> netId " + targetNetId + " item " + itemId);
    }

    [Server]
    private NetworkIdentity FindNetIdentity(uint netId)
    {
        if (netId == 0) return null;
        var all = FindObjectsOfType<NetworkIdentity>();
        for (int i = 0; i < all.Length; i++)
            if (all[i].netId == netId) return all[i];
        return null;
    }

    // -------- RPCs --------

    [ClientRpc]
    private void RpcClearConsumeVisuals()
    {
        ClearVisuals(conVisuals);
    }

    // -------- Visual helpers --------

    private GameObject SpawnItemVisual(int itemId, Transform anchor, bool isInventory, int slotIndex)
    {
        GameObject prefab = null;
        var deck = FindObjectOfType<ItemDeck>();
        if (deck != null)
        {
            var entry = deck.Get(itemId);
            if (entry != null && entry.visualPrefab != null)
                prefab = entry.visualPrefab;
        }

        GameObject go;
        if (prefab != null) go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", isInventory ? new Color(0.6f, 0.8f, 1f) : new Color(1f, 0.8f, 0.4f));
                r.material = m;
            }
        }

        go.name = (isInventory ? "Inv_" : "Use_") + slotIndex + "_Item" + itemId;
        go.transform.SetParent(anchor != null ? anchor : transform, false);
        go.transform.localPosition = isInventory ? inventoryOffset : consumeOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * itemScale;

        if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();

        var inst = go.GetComponent<ItemInstance>();
        if (inst == null) inst = go.AddComponent<ItemInstance>();
        inst.itemId = itemId;
        inst.isInventorySlot = isInventory;
        inst.slotIndex = slotIndex;
        inst.owner = this;

        return go;
    }

    private static Transform GetAnchor(Transform[] anchors, int index)
    {
        if (anchors == null || anchors.Length == 0) return null;
        if (index < 0 || index >= anchors.Length)
            return anchors[Mathf.Clamp(index, 0, anchors.Length - 1)];
        return anchors[index];
    }

    private void EnsureAnchorArray(ref Transform[] anchors, int expected, bool forInventory)
    {
        if (anchors != null && anchors.Length >= expected) return;

        // Try to rebind from service once more
        var svc = ItemTrayService.Instance;
        if (svc != null && seatIndex1Based > 0)
        {
            Transform[] inv, con;
            if (svc.TryGetAnchors(seatIndex1Based, out inv, out con))
            {
                if (forInventory) anchors = inv; else anchors = con;
                return;
            }
        }

        // Otherwise keep current (fallback may already be in place).
        if (anchors == null || anchors.Length < expected)
        {
            int n = Mathf.Max(expected, 1);
            var root = new GameObject(forInventory ? "LocalInvAnchors" : "LocalConAnchors").transform;
            root.SetParent(transform, false);
            anchors = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                var a = new GameObject((forInventory ? "Inv" : "Con") + "_Slot_" + i).transform;
                a.SetParent(root, false);
                a.localPosition = new Vector3(i * 0.25f, forInventory ? 0.0f : -0.25f, 0.0f);
                anchors[i] = a;
            }
            if (debugLogs)
                Debug.Log("[PlayerItemTrays] Seat " + seatIndex1Based + " created LOCAL anchors (no service).");
        }
    }
}
