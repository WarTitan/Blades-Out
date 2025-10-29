// FILE: PlayerItemTrays.cs
// FULL REPLACEMENT (ASCII)
// - On consume, broadcast RpcClearConsumeVisuals() so ALL clients immediately delete models,
//   avoiding SyncList update ordering races.
// - Keeps duplicate-merge (sum durations) when applying effects.

using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;

[AddComponentMenu("Gameplay/Items/Player Item Trays")]
public class PlayerItemTrays : NetworkBehaviour
{
    public const int MaxSlots = 8;

    public class SyncListIntLite : SyncList<int> { }

    [Header("Seat Auto-Assign")]
    public float seatAssignMaxDistance = 6f; // soft radius
    public float softAssignSeconds = 2f;     // after this, ignore distance
    public float hardAssignSeconds = 10f;    // hard fallback

    [SyncVar(hook = nameof(OnSeatChanged))]
    public int seatIndex1Based = 0;

    public SyncListIntLite inventory = new SyncListIntLite();
    public SyncListIntLite consume = new SyncListIntLite();

    private Transform[] invAnchors;
    private Transform[] conAnchors;

    private readonly List<GameObject> invVisuals = new List<GameObject>();
    private readonly List<GameObject> conVisuals = new List<GameObject>();

    private void Start()
    {
        if (isServer) StartCoroutine(Server_AutoSeatAssignLoop());

        ResolveAnchors();
        inventory.Callback += OnInvChanged;
        consume.Callback += OnConChanged;
        RebuildAllVisuals();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveAnchors();
        RebuildAllVisuals();
    }

    private void OnSeatChanged(int oldVal, int newVal)
    {
        ResolveAnchors();
        RebuildAllVisuals();
    }

    private void ResolveAnchors()
    {
        invAnchors = null; conAnchors = null;

        if (ItemTrayService.Instance != null && seatIndex1Based > 0)
        {
            bool ok = ItemTrayService.Instance.TryGetAnchors(seatIndex1Based, out invAnchors, out conAnchors);
            if (!ok || invAnchors == null || conAnchors == null)
            {
                Debug.LogWarning("[PlayerItemTrays] Seat" + seatIndex1Based +
                                 " missing anchors. Ensure TraysRoot/Seat" + seatIndex1Based +
                                 "/Inventory(8) and /Consume(8).");
            }
        }
    }

    // ----- Server: seat assignment by proximity (with hard fallback) -----
    [Server]
    private IEnumerator Server_AutoSeatAssignLoop()
    {
        float t = 0f;
        while (seatIndex1Based <= 0 && t < hardAssignSeconds)
        {
            int guess; float distSq;
            Server_GuessNearestSeatIndex(out guess, out distSq);

            if (guess > 0)
            {
                bool withinSoft = distSq <= (seatAssignMaxDistance * seatAssignMaxDistance);
                bool afterSoft = t >= softAssignSeconds;

                if (withinSoft || afterSoft)
                {
                    seatIndex1Based = guess;
                    yield break;
                }
            }

            yield return new WaitForSeconds(0.25f);
            t += 0.25f;
        }

        if (seatIndex1Based <= 0)
        {
            int guess; float distSq;
            Server_GuessNearestSeatIndex(out guess, out distSq);
            if (guess > 0) seatIndex1Based = guess;
        }
    }

    [Server]
    private void Server_GuessNearestSeatIndex(out int bestIdx, out float bestDistSq)
    {
        bestIdx = 0; bestDistSq = float.MaxValue;
        if (ItemTrayService.Instance == null || ItemTrayService.Instance.traysRoot == null) return;

        var root = ItemTrayService.Instance.traysRoot;
        for (int s = 1; s <= 5; s++)
        {
            var seat = root.Find("Seat" + s);
            if (seat == null) continue;
            float d = (seat.position - transform.position).sqrMagnitude;
            if (d < bestDistSq) { bestDistSq = d; bestIdx = s; }
        }
    }

    // ----- Server tray API -----
    [Server]
    public bool Server_AddItemToInventory(int itemId)
    {
        if (itemId < 0) return false;
        if (inventory.Count >= MaxSlots) return false;
        inventory.Add(itemId);
        return true;
    }

    [Server]
    public int Server_AddSeveralToInventoryClamped(int count)
    {
        int added = 0;
        if (ItemDeck.Instance == null) return 0;
        for (int i = 0; i < count; i++)
        {
            if (inventory.Count >= MaxSlots) break;
            int draw = ItemDeck.Instance.DrawRandomId();
            if (draw < 0) break;
            inventory.Add(draw);
            added++;
        }
        return added;
    }

    [Server]
    public void Server_ConsumeAllNow()
    {
        if (consume.Count == 0) return;

        List<int> toApply = new List<int>(consume.Count);
        for (int i = 0; i < consume.Count; i++) toApply.Add(consume[i]);

        // Clear and force-clear visuals everywhere (prevents RPC/SyncList ordering glitches)
        consume.Clear();
        RpcClearConsumeVisuals();

        // Apply effects on the owner client
        Target_ApplyEffects(connectionToClient, toApply.ToArray());
    }

    [ClientRpc]
    private void RpcClearConsumeVisuals()
    {
        // Destroy consume models immediately on all clients.
        for (int i = 0; i < conVisuals.Count; i++)
        {
            var go = conVisuals[i];
            if (go != null) Object.Destroy(go);
        }
        conVisuals.Clear();
    }

    // ----- Effects spawn: merge duplicates into one effect (sum duration, max intensity) -----
    [TargetRpc]
    private void Target_ApplyEffects(NetworkConnectionToClient conn, int[] itemIds)
    {
        if (itemIds == null || itemIds.Length == 0) return;

        Camera cam = null;
        var lcc = GetComponent<LocalCameraController>();
        if (lcc != null && lcc.playerCamera != null) cam = lcc.playerCamera;
        if (cam == null) cam = GetComponentInChildren<Camera>(true);
        if (cam == null) return;
        if (ItemDeck.Instance == null) return;

        Dictionary<int, Agg> map = new Dictionary<int, Agg>(16);
        for (int i = 0; i < itemIds.Length; i++)
        {
            int id = itemIds[i];
            var def = ItemDeck.Instance.Get(id);
            if (def == null || def.effectPrefab == null) continue;

            Agg a;
            if (!map.TryGetValue(id, out a))
            {
                a.itemId = id;
                a.totalDuration = Mathf.Max(0f, def.durationSeconds);
                a.intensity = def.intensity;
                a.displayName = string.IsNullOrEmpty(def.itemName) ? def.effectPrefab.GetType().Name : def.itemName;
                a.prefab = def.effectPrefab;
                map[id] = a;
            }
            else
            {
                a.totalDuration += Mathf.Max(0f, def.durationSeconds);
                if (def.intensity > a.intensity) a.intensity = def.intensity;
                map[id] = a;
            }
        }

        foreach (var kv in map)
        {
            var a = kv.Value;
            if (a.prefab == null) continue;

            PsychoactiveEffectBase eff = Object.Instantiate(a.prefab);
            eff.name = "Effect_" + a.displayName;
            eff.transform.SetParent(cam.transform, false);
            eff.BeginNamed(cam, Mathf.Max(0.01f, a.totalDuration), Mathf.Clamp01(a.intensity), a.displayName);
        }
    }

    private struct Agg
    {
        public int itemId;
        public float totalDuration;
        public float intensity;
        public string displayName;
        public PsychoactiveEffectBase prefab;
    }

    // ----- Trading -----
    [Command]
    public void Cmd_GiveItemToPlayer(int inventorySlotIndex, NetworkIdentity targetPlayer)
    {
        if (!isServer) return;

        var requester = (connectionToClient != null) ? connectionToClient.identity : null;
        if (TurnManagerNet.Instance == null || !TurnManagerNet.Instance.CanTradeNow(requester))
            return;

        if (inventorySlotIndex < 0 || inventorySlotIndex >= inventory.Count) return;
        if (targetPlayer == null) return;

        var targetTrays = targetPlayer.GetComponent<PlayerItemTrays>();
        if (targetTrays == null) return;
        if (targetTrays.consume.Count >= MaxSlots) return;

        int itemId = inventory[inventorySlotIndex];
        inventory.RemoveAt(inventorySlotIndex);
        targetTrays.consume.Add(itemId);

        // Force quick visual refresh on both ends (owner + target)
        Target_PingRefresh(connectionToClient);
        var targetConn = targetPlayer.connectionToClient;
        if (targetConn != null) targetTrays.Target_PingRefresh(targetConn);
    }

    [TargetRpc]
    private void Target_PingRefresh(NetworkConnectionToClient conn)
    {
        RebuildAllVisuals();
    }

    // ----- SyncList-driven visuals -----
    private void OnInvChanged(SyncListIntLite.Operation op, int index, int oldItem, int newItem)
    {
        RebuildInventoryVisuals();
    }

    private void OnConChanged(SyncListIntLite.Operation op, int index, int oldItem, int newItem)
    {
        RebuildConsumeVisuals();
    }

    private void RebuildAllVisuals()
    {
        RebuildInventoryVisuals();
        RebuildConsumeVisuals();
    }

    private void ClearList(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var go = list[i];
            if (go != null) Object.Destroy(go);
        }
        list.Clear();
    }

    private void RebuildInventoryVisuals()
    {
        ClearList(invVisuals);
        if (invAnchors == null || ItemDeck.Instance == null) return;
        int n = Mathf.Min(inventory.Count, MaxSlots);
        n = (invAnchors != null) ? Mathf.Min(n, invAnchors.Length) : 0;

        for (int i = 0; i < n; i++)
        {
            var def = ItemDeck.Instance.Get(inventory[i]);
            if (def == null) continue;

            var slot = invAnchors[i];
            GameObject go = Object.Instantiate(def.visualPrefab != null ? def.visualPrefab : GameObject.CreatePrimitive(PrimitiveType.Cube));
            if (def.visualPrefab == null)
            {
                var col = go.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
                go.transform.localScale = Vector3.one * 0.18f;
                go.name = "Placeholder_" + (string.IsNullOrEmpty(def.itemName) ? "Item" : def.itemName);
            }

            go.transform.SetPositionAndRotation(slot.position, slot.rotation);
            go.transform.SetParent(slot, true);

            var inst = go.GetComponent<ItemInstance>();
            if (inst == null) inst = go.AddComponent<ItemInstance>();
            inst.itemId = inventory[i];
            inst.slotIndex = i;
            inst.isInventorySlot = true;
            inst.owner = this;

            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();

            invVisuals.Add(go);
        }
    }

    private void RebuildConsumeVisuals()
    {
        ClearList(conVisuals);
        if (conAnchors == null || ItemDeck.Instance == null) return;
        int n = Mathf.Min(consume.Count, MaxSlots);
        n = (conAnchors != null) ? Mathf.Min(n, conAnchors.Length) : 0;

        for (int i = 0; i < n; i++)
        {
            var def = ItemDeck.Instance.Get(consume[i]);
            if (def == null) continue;

            var slot = conAnchors[i];
            GameObject go = Object.Instantiate(def.visualPrefab != null ? def.visualPrefab : GameObject.CreatePrimitive(PrimitiveType.Cube));
            if (def.visualPrefab == null)
            {
                var col = go.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
                go.transform.localScale = Vector3.one * 0.18f;
                go.name = "Placeholder_" + (string.IsNullOrEmpty(def.itemName) ? "Item" : def.itemName);
            }

            go.transform.SetPositionAndRotation(slot.position, slot.rotation);
            go.transform.SetParent(slot, true);

            var inst = go.GetComponent<ItemInstance>();
            if (inst == null) inst = go.AddComponent<ItemInstance>();
            inst.itemId = consume[i];
            inst.slotIndex = i;
            inst.isInventorySlot = false;
            inst.owner = this;

            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();

            conVisuals.Add(go);
        }
    }
}
