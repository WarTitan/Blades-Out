// FILE: PlayerItemTrays.cs
// FULL FILE (ASCII only)
//
// Server authoritative inventory + consume SyncLists,
// trading Command used by ItemInteraction,
// and simple client visuals (falls back to cubes if no prefab).
//
// Seat is NOT handled here (SeatIndexAuthority sets seatIndex1Based).

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

    public class IntList : SyncList<int> { }
    public readonly IntList inventory = new IntList();
    public readonly IntList consume = new IntList();


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
        RebindAnchors();
        RebuildInventoryVisuals();
        RebuildConsumeVisuals();
    }

    private void RebindAnchors()
    {
        invAnchors = null;
        conAnchors = null;
        var svc = ItemTrayService.Instance;
        if (svc != null && seatIndex1Based > 0)
        {
            Transform[] inv, con;
            if (svc.TryGetAnchors(seatIndex1Based, out inv, out con))
            {
                invAnchors = inv;
                conAnchors = con;
            }
        }
    }

    // -------- SyncList callbacks -> visuals --------

    private void OnInventoryChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        RebuildInventoryVisuals();
    }

    private void OnConsumeChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
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
        }
    }

    private void ClearVisuals(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var go = list[i];
            if (go != null) Object.Destroy(go);
        }
        list.Clear();
    }

    // -------- Server API (draw/consume) --------

    [Server]
    public int Server_AddSeveralToInventoryClamped(int count)
    {
        if (count <= 0) return 0;

        int added = 0;
#pragma warning disable CS0618
        var deck = Object.FindObjectOfType<ItemDeck>();
#pragma warning disable CS0618
        if (deck == null)
        {
            Debug.LogError("[PlayerItemTrays] No ItemDeck in scene. Cannot draw items.");
            return 0;
        }
        if (deck.Count <= 0)
        {
            Debug.LogError("[PlayerItemTrays] ItemDeck has 0 entries. Populate items first.");
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

    // Called by TurnManagerNet at the start of this player's turn.
    // Consumes everything in 'consume', clears the tray everywhere,
    // then spawns effect prefabs on this player's local camera.
    [Server]
    public void Server_ConsumeAllNow()
    {
        if (consume.Count == 0) return;

        // Copy the IDs we are about to consume.
        List<int> toApply = new List<int>(consume.Count);
        for (int i = 0; i < consume.Count; i++)
            toApply.Add(consume[i]);

        Debug.Log("[PlayerItemTrays] Server_ConsumeAllNow seat=" + seatIndex1Based +
                  " count=" + toApply.Count);

        // Clear the SyncList (propagates to all clients) and explicitly clear visuals.
        consume.Clear();
        RpcClearConsumeVisuals();

        // Tell owning client to spawn effects.
        if (connectionToClient != null && toApply.Count > 0)
        {
            Target_ApplyEffects(connectionToClient, toApply.ToArray());
        }
        else
        {
            Debug.LogWarning("[PlayerItemTrays] No connectionToClient on consume; cannot apply effects.");
        }
    }

    // -------- Trading (Command used by ItemInteraction) --------
    //
    // NOTE: important:
    //  - fromSlotIndex indexes *this* inventory.
    //  - targetNetId is the other player's NetworkIdentity.netId.
    //  - the item moves from this.inventory[fromSlotIndex] to target.consume.

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

        // Gift goes into CONSUME, not inventory.
        if (targetTrays.consume.Count >= MaxSlots) return;

        int itemId = inventory[fromSlotIndex];
        inventory.RemoveAt(fromSlotIndex);
        targetTrays.consume.Add(itemId);

        Debug.Log("[PlayerItemTrays] Trade seat " + seatIndex1Based +
                  " -> netId " + targetNetId +
                  " item " + itemId + " (into target.consume)");
    }

    [Server]
    private NetworkIdentity FindNetIdentity(uint netId)
    {
        if (netId == 0) return null;
        var all = Object.FindObjectsOfType<NetworkIdentity>();
        for (int i = 0; i < all.Length; i++)
            if (all[i].netId == netId) return all[i];
        return null;
    }

    // -------- RPCs & effect spawning --------

    [ClientRpc]
    private void RpcClearConsumeVisuals()
    {
        // Clear tracked consume visuals
        ClearVisuals(conVisuals);

        // Extra safety: also destroy ANY children under consume anchors
        // in case something slipped past our conVisuals list on this client.
        if (conAnchors != null)
        {
            for (int i = 0; i < conAnchors.Length; i++)
            {
                var anchor = conAnchors[i];
                if (anchor == null) continue;

                for (int c = anchor.childCount - 1; c >= 0; c--)
                {
                    var child = anchor.GetChild(c);
                    if (child != null)
                        Destroy(child.gameObject);
                }
            }
        }
    }

    // When this player consumes, the server sends the itemIds we drank.
    // We look them up in ItemDeck, group by itemId and spawn one effect prefab
    // per type, with lifetime equal to sum of effectLifetime values.
    [TargetRpc]
    private void Target_ApplyEffects(NetworkConnectionToClient conn, int[] itemIds)
    {
        if (itemIds == null || itemIds.Length == 0) return;

        // Find this player's camera.
        Camera cam = null;
        var lcc = GetComponent<LocalCameraController>();
        if (lcc != null && lcc.playerCamera != null)
            cam = lcc.playerCamera;
        if (cam == null)
            cam = GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            Debug.LogWarning("[PlayerItemTrays] No camera found to apply effects.");
            return;
        }

        var deck = Object.FindObjectOfType<ItemDeck>();
        if (deck == null)
        {
            Debug.LogError("[PlayerItemTrays] No ItemDeck on client; cannot resolve effects.");
            return;
        }

        // Aggregate by itemId.
        Dictionary<int, EffectAggregate> agg = new Dictionary<int, EffectAggregate>(16);
        for (int i = 0; i < itemIds.Length; i++)
        {
            int id = itemIds[i];
            var def = deck.Get(id);
            if (def == null || def.effectPrefab == null) continue;

            EffectAggregate ea;
            if (!agg.TryGetValue(id, out ea))
            {
                ea.itemId = id;
                ea.totalLifetime = Mathf.Max(0f, def.effectLifetime);
                ea.displayName = string.IsNullOrEmpty(def.itemName)
                    ? def.effectPrefab.name
                    : def.itemName;
                ea.effectPrefab = def.effectPrefab;
                agg[id] = ea;
            }
            else
            {
                ea.totalLifetime += Mathf.Max(0f, def.effectLifetime);
                agg[id] = ea;
            }
        }

        foreach (var kv in agg)
        {
            var e = kv.Value;
            if (e.effectPrefab == null) continue;

            GameObject go = Object.Instantiate(e.effectPrefab);
            go.name = "Effect_" + e.displayName;
            go.transform.SetParent(cam.transform, false);

            float lifetime = e.totalLifetime > 0f ? e.totalLifetime : 5f;

            // If the effect prefab has a PsychoactiveEffectBase, use its BeginNamed API.
            var effectComp = go.GetComponent<PsychoactiveEffectBase>();
            if (effectComp == null)
                effectComp = go.GetComponentInChildren<PsychoactiveEffectBase>();

            if (effectComp != null)
            {
                // Intensity fixed to 1 for now.
                effectComp.BeginNamed(cam, lifetime, 1f, e.displayName);
            }
            else
            {
                // No psychoactive script, just destroy after lifetime.
                if (lifetime > 0f)
                    Object.Destroy(go, lifetime);
            }
        }
    }

    private struct EffectAggregate
    {
        public int itemId;
        public float totalLifetime;
        public string displayName;
        public GameObject effectPrefab;
    }

    // -------- Visual helpers --------

    private GameObject SpawnItemVisual(int itemId, Transform anchor, bool isInventory, int slotIndex)
    {
        GameObject prefab = null;
        var deck = Object.FindObjectOfType<ItemDeck>();
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
                var m = new Material(r.sharedMaterial);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", isInventory ? new Color(0.6f, 0.8f, 1f) : new Color(1f, 0.8f, 0.4f));
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

        // Ask service again (maybe seat changed)
        RebindAnchors();

        if (anchors == null || anchors.Length < expected)
        {
            var root = new GameObject(forInventory ? "InvAnchors" : "UseAnchors").transform;
            root.SetParent(transform, false);
            anchors = new Transform[expected];
            for (int i = 0; i < expected; i++)
            {
                var a = new GameObject((forInventory ? "Inv" : "Use") + "_Slot_" + i).transform;
                a.SetParent(root, false);
                a.localPosition = new Vector3(i * 0.25f, 0f, 0f);
                anchors[i] = a;
            }
        }
    }

    // -------- Extra safety: client-side desync guard --------

    private void Update()
    {
        if (!isClient) return;

        // If the SyncList says no items in consume, but we still have visuals,
        // nuke them. This catches any weird ordering/late RPC issues.
        if (consume.Count == 0 && conVisuals.Count > 0)
        {
            ClearVisuals(conVisuals);

            if (conAnchors != null)
            {
                for (int i = 0; i < conAnchors.Length; i++)
                {
                    var anchor = conAnchors[i];
                    if (anchor == null) continue;

                    for (int c = anchor.childCount - 1; c >= 0; c--)
                    {
                        var child = anchor.GetChild(c);
                        if (child != null)
                            Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}
