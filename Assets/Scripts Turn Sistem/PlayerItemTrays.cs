// FILE: PlayerItemTrays.cs
// (unchanged behavior, plus: RpcRefreshTrays already present;
//  now adds Target_GiftRejected and uses it on all early rejects)

using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.Rendering;

[AddComponentMenu("Gameplay/Items/Player Item Trays")]
public class PlayerItemTrays : NetworkBehaviour
{
    public const int MaxSlots = 8;

    [Header("Seating")]
    [SyncVar(hook = nameof(OnSeatChanged))]
    public int seatIndex1Based = 0; // 0 = not seated / lobby, 1..5 = real seats

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

    public override void OnStartServer()
    {
        base.OnStartServer();

        var tm = TurnManagerNet.Instance;
        if (tm != null)
        {
            tm.Server_OnPlayerTraysSpawned(this);
        }
    }

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

        if (seatIndex1Based <= 0)
            return; // no visuals in lobby / no seat

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

        if (seatIndex1Based <= 0)
            return;

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

        ItemDeck deck;
#if UNITY_2023_1_OR_NEWER
        deck = Object.FindFirstObjectByType<ItemDeck>();
#else
#pragma warning disable CS0618
        deck = Object.FindObjectOfType<ItemDeck>();
#pragma warning restore CS0618
#endif

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

    [Server]
    public void Server_ConsumeAllNow()
    {
        if (consume.Count == 0) return;

        List<int> toApply = new List<int>(consume.Count);
        for (int i = 0; i < consume.Count; i++)
            toApply.Add(consume[i]);

        Debug.Log("[PlayerItemTrays] Server_ConsumeAllNow seat=" + seatIndex1Based +
                  " count=" + toApply.Count);

        while (consume.Count > 0)
            consume.RemoveAt(consume.Count - 1);

        RpcClearConsumeVisuals();

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

    [Command]
    public void Cmd_GiveItemToPlayer(uint targetNetId, int fromSlotIndex)
    {
        var senderId = GetComponent<NetworkIdentity>();
        if (senderId == null) return;

        var tm = TurnManagerNet.Instance;
        if (tm == null) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "No TurnManager"); return; }
        if (!tm.CanTradeNow(senderId)) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "Not in Crafting"); return; }

        if (fromSlotIndex < 0 || fromSlotIndex >= inventory.Count) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "Bad slot"); return; }
        if (targetNetId == 0) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "No target"); return; }

        NetworkIdentity targetIdentity = FindNetIdentity(targetNetId);
        if (targetIdentity == null) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "Target missing"); return; }

        var targetTrays = targetIdentity.GetComponent<PlayerItemTrays>();
        if (targetTrays == null) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "No target trays"); return; }

        if (!tm.Server_OnGift(senderId.netId, targetNetId)) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "Server_OnGift rejected"); return; }

        if (targetTrays.consume.Count >= MaxSlots) { if (senderId.connectionToClient != null) Target_GiftRejected(senderId.connectionToClient, "Target consume full"); return; }

        int itemId = inventory[fromSlotIndex];
        inventory.RemoveAt(fromSlotIndex);
        targetTrays.consume.Add(itemId);

        Debug.Log("[PlayerItemTrays] Gift seat " + seatIndex1Based +
                  " -> netId " + targetNetId +
                  " item " + itemId + " (into target.consume)");

        // Force all clients to rebuild visuals for both trays
        RpcRefreshTrays();             // giver's inventory visuals
        targetTrays.RpcRefreshTrays(); // target's consume visuals
    }

    [Server]
    private NetworkIdentity FindNetIdentity(uint netId)
    {
        if (netId == 0) return null;
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
        var all = GameObject.FindObjectsOfType<NetworkIdentity>();
#pragma warning restore CS0618
#endif
        for (int i = 0; i < all.Length; i++)
            if (all[i].netId == netId) return all[i];
        return null;
    }

    // -------- RPCs & effect spawning --------

    [ClientRpc]
    private void RpcClearConsumeVisuals()
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

    [ClientRpc]
    private void RpcRefreshTrays()
    {
        RebuildInventoryVisuals();
        RebuildConsumeVisuals();
    }

    // NEW: tell the giver client a gift was rejected so visuals pop back immediately
    [TargetRpc]
    private void Target_GiftRejected(NetworkConnectionToClient conn, string reason)
    {
        Debug.LogWarning("[PlayerItemTrays] Gift rejected: " + reason);
        RebuildInventoryVisuals();
        RebuildConsumeVisuals();
    }

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

        // Look up deck on client
        ItemDeck deck;
#if UNITY_2023_1_OR_NEWER
        deck = Object.FindFirstObjectByType<ItemDeck>();
#else
#pragma warning disable CS0618
        deck = Object.FindObjectOfType<ItemDeck>();
#pragma warning restore CS0618
#endif

        if (deck == null)
        {
            Debug.LogError("[PlayerItemTrays] No ItemDeck on client; cannot resolve effects.");
            return;
        }

        // Aggregate by effect prefab: sum lifetimes, max intensities
        Dictionary<GameObject, EffectAggregate> agg = new Dictionary<GameObject, EffectAggregate>(16);

        for (int i = 0; i < itemIds.Length; i++)
        {
            int id = itemIds[i];
            var def = deck.Get(id);
            if (def == null || def.effectPrefab == null) continue;

            GameObject key = def.effectPrefab;

            EffectAggregate ea;
            if (!agg.TryGetValue(key, out ea))
            {
                ea.effectPrefab = def.effectPrefab;
                ea.totalLifetime = Mathf.Max(0f, def.effectLifetime);
                ea.maxIntensity = Mathf.Max(0f, def.effectIntensity);
                ea.displayName = string.IsNullOrEmpty(def.itemName)
                                   ? def.effectPrefab.name
                                   : def.itemName;
            }
            else
            {
                ea.totalLifetime += Mathf.Max(0f, def.effectLifetime);
                float thisIntensity = Mathf.Max(0f, def.effectIntensity);
                if (thisIntensity > ea.maxIntensity)
                    ea.maxIntensity = thisIntensity;
            }

            agg[key] = ea;
        }

        var hudMgr = GetComponent<PsychoactiveEffectsManager>();

        foreach (var kv in agg)
        {
            var e = kv.Value;
            if (e.effectPrefab == null) continue;

            float lifetime = e.totalLifetime > 0f ? e.totalLifetime : 5f;
            float intensity = e.maxIntensity > 0f ? e.maxIntensity : 1f;
            intensity = Mathf.Clamp01(intensity);

            Debug.Log("[PlayerItemTrays] Spawning effect prefab '" + e.effectPrefab.name +
                      "' lifetime=" + lifetime +
                      " intensity(norm)=" + intensity);

            GameObject go = Object.Instantiate(e.effectPrefab);
            go.name = "Effect_" + e.displayName;
            go.transform.SetParent(cam.transform, false);

            if (hudMgr != null && lifetime > 0f)
            {
                hudMgr.RegisterEffect(e.displayName, lifetime);
            }

            var itemEffect = go.GetComponent<IItemEffect>();
            if (itemEffect == null)
                itemEffect = go.GetComponentInChildren<IItemEffect>();

            if (itemEffect != null)
            {
                itemEffect.Play(lifetime, intensity);
            }
            else
            {
                if (lifetime > 0f)
                    Object.Destroy(go, lifetime);
            }
        }
    }

    private struct EffectAggregate
    {
        public GameObject effectPrefab;
        public float totalLifetime;
        public float maxIntensity;
        public string displayName;
    }

    // -------- Visual helpers --------

    private GameObject SpawnItemVisual(int itemId, Transform anchor, bool isInventory, int slotIndex)
    {
        GameObject prefab = null;

        ItemDeck deck;
#if UNITY_2023_1_OR_NEWER
        deck = Object.FindFirstObjectByType<ItemDeck>();
#else
#pragma warning disable CS0618
        deck = Object.FindObjectOfType<ItemDeck>();
#pragma warning restore CS0618
#endif

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

        RebindAnchors();

        if (anchors == null || anchors.Length < expected)
        {
            if (seatIndex1Based <= 0)
                return;

            var root = new GameObject(forInventory ? "InvAnchors_Auto" : "ConAnchors_Auto");
            root.transform.SetParent(transform, false);
            anchors = new Transform[expected];
            for (int i = 0; i < expected; i++)
            {
                var child = new GameObject((forInventory ? "Inv_" : "Con_") + i);
                child.transform.SetParent(root.transform, false);
                anchors[i] = child.transform;
            }
        }
    }
}
