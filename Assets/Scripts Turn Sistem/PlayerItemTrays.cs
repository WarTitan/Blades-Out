// FILE: PlayerItemTrays.cs
// FULL REPLACEMENT (ASCII only)
// Per-player trays with 8 inventory and 8 consume slots.
// On consume, effects are spawned client-side from ItemDeck and begun on the player's camera.

using UnityEngine;
using Mirror;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Items/Player Item Trays")]
public class PlayerItemTrays : NetworkBehaviour
{
    public const int MaxSlots = 8;

    public class SyncListIntLite : SyncList<int> { }

    [SyncVar] public int seatIndex1Based = 0;

    public SyncListIntLite inventory = new SyncListIntLite();
    public SyncListIntLite consume = new SyncListIntLite();

    private Transform[] invAnchors;
    private Transform[] conAnchors;

    private readonly List<GameObject> invVisuals = new List<GameObject>();
    private readonly List<GameObject> conVisuals = new List<GameObject>();

    private void Start()
    {
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

    private void ResolveAnchors()
    {
        if (ItemTrayService.Instance == null) return;
        ItemTrayService.Instance.TryGetAnchors(seatIndex1Based, out invAnchors, out conAnchors);
    }

    // ---------- SERVER API ----------

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
        for (int i = 0; i < count; i++)
        {
            if (inventory.Count >= MaxSlots) break;
            int draw = (ItemDeck.Instance != null) ? ItemDeck.Instance.DrawRandomId() : -1;
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
        consume.Clear();

        Target_ApplyEffects(connectionToClient, toApply.ToArray());
    }

    // NOTE: We do NOT depend on PsychoactiveEffectsManager APIs.
    // We instantiate the effect prefab directly and call Begin(cam, duration, intensity).
    [TargetRpc]
    private void Target_ApplyEffects(NetworkConnectionToClient conn, int[] itemIds)
    {
        if (itemIds == null || itemIds.Length == 0) return;

        // Find the player's camera (prefer LocalCameraController)
        Camera cam = null;
        var lcc = GetComponent<LocalCameraController>();
        if (lcc != null && lcc.playerCamera != null) cam = lcc.playerCamera;
        if (cam == null) cam = GetComponentInChildren<Camera>(true);

        if (cam == null)
        {
            Debug.LogWarning("[PlayerItemTrays] No camera found on local player; cannot apply effects.");
            return;
        }

        for (int i = 0; i < itemIds.Length; i++)
        {
            var def = (ItemDeck.Instance != null) ? ItemDeck.Instance.Get(itemIds[i]) : null;
            if (def == null || def.effectPrefab == null) continue;

            // Instantiate the effect prefab as a child of the camera so it can find mixers easily.
            PsychoactiveEffectBase eff = Instantiate(def.effectPrefab);
            eff.name = "Effect_" + (string.IsNullOrEmpty(def.itemName) ? ("Item_" + itemIds[i]) : def.itemName);
            eff.transform.SetParent(cam.transform, false);

            // Kick it off
            eff.Begin(cam, def.durationSeconds, def.intensity);
        }
    }

    [Command]
    public void Cmd_GiveItemToPlayer(int inventorySlotIndex, NetworkIdentity targetPlayer)
    {
        if (!isServer) return;
        if (inventorySlotIndex < 0 || inventorySlotIndex >= inventory.Count) return;
        if (targetPlayer == null) return;

        var targetTrays = targetPlayer.GetComponent<PlayerItemTrays>();
        if (targetTrays == null) return;
        if (targetTrays.consume.Count >= MaxSlots) return;

        int itemId = inventory[inventorySlotIndex];
        inventory.RemoveAt(inventorySlotIndex);
        targetTrays.consume.Add(itemId);
    }

    // ---------- CLIENT VISUALS ----------

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
            if (go != null) Destroy(go);
        }
        list.Clear();
    }

    private void RebuildInventoryVisuals()
    {
        ClearList(invVisuals);
        if (invAnchors == null || ItemDeck.Instance == null) return;

        int n = Mathf.Min(inventory.Count, invAnchors.Length);
        for (int i = 0; i < n; i++)
        {
            var def = ItemDeck.Instance.Get(inventory[i]);
            if (def == null || def.visualPrefab == null) continue;

            var slot = invAnchors[i];
            var go = Instantiate(def.visualPrefab);
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

        int n = Mathf.Min(consume.Count, conAnchors.Length);
        for (int i = 0; i < n; i++)
        {
            var def = ItemDeck.Instance.Get(consume[i]);
            if (def == null || def.visualPrefab == null) continue;

            var slot = conAnchors[i];
            var go = Instantiate(def.visualPrefab);
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
