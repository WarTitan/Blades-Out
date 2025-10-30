// FILE: ItemInteraction.cs
// FULL FILE (ASCII only)
//
// Purpose:
// - Client-side helper to give an inventory item to another player.
// - Calls PlayerItemTrays.Cmd_GiveItemToPlayer(uint targetNetId, int fromSlotIndex)
//   with the correct argument order and types.
//
// Usage patterns provided:
//
// 1) Crosshair targeting:
//    - Call GiveFromInventorySlotViaCrosshair(int fromSlotIndex).
//      It raycasts from the local player's camera center to find a PlayerItemTrays
//      on another player and uses that player's NetworkIdentity.netId.
//
// 2) Seat targeting:
//    - Call GiveFromInventorySlotToSeat(int seatIndex1Based, int fromSlotIndex).
//
// Requirements:
// - This component should live on the LOCAL player's object (has authority).
// - The same GameObject should have PlayerItemTrays and a Camera reference
//   (or a LocalCameraController that exposes playerCamera).
// - PlayerItemTrays.cs must include Cmd_GiveItemToPlayer(uint, int) as provided.
//
// Notes:
// - No automatic input here; call these public methods from your UI or hotkeys.
// - All arguments are range-checked. If anything is invalid, the call is ignored.

using UnityEngine;
using Mirror;

[AddComponentMenu("Gameplay/Items/Item Interaction")]
public class ItemInteraction : NetworkBehaviour
{
    [Header("References")]
    public PlayerItemTrays trays;         // owner trays (on this player)
    public Camera playerCamera;           // local camera for crosshair ray
    public LayerMask rayMask = ~0;        // optional filter for raycast

    [Header("Raycast")]
    public float rayDistance = 100f;

    private void Awake()
    {
        if (trays == null) trays = GetComponent<PlayerItemTrays>();

        if (playerCamera == null)
        {
            // Try common patterns
            var lcc = GetComponent<LocalCameraController>();
            if (lcc != null && lcc.playerCamera != null) playerCamera = lcc.playerCamera;
            if (playerCamera == null && Camera.main != null) playerCamera = Camera.main;
            if (playerCamera == null)
            {
                var anyCam = GetComponentInChildren<Camera>(true);
                if (anyCam != null) playerCamera = anyCam;
            }
        }
    }

    // -------- Public API: call these from UI/hotkeys --------

    // Give an item from our INVENTORY slot to the player under the crosshair.
    public void GiveFromInventorySlotViaCrosshair(int fromSlotIndex)
    {
        if (!isLocalPlayer) return;
        if (trays == null) return;

        if (!IsValidInventorySlot(fromSlotIndex)) return;

        var targetId = RaycastTargetIdentity();
        if (targetId == null) return;
        if (targetId == trays.GetComponent<NetworkIdentity>()) return; // cannot give to self

        // Correct arg order and types:
        //   (uint targetNetId, int fromSlotIndex)
        trays.Cmd_GiveItemToPlayer(targetId.netId, fromSlotIndex);
    }

    // Give an item from our INVENTORY slot to the player seated at the given seat.
    public void GiveFromInventorySlotToSeat(int seatIndex1Based, int fromSlotIndex)
    {
        if (!isLocalPlayer) return;
        if (trays == null) return;

        if (seatIndex1Based <= 0) return;
        if (!IsValidInventorySlot(fromSlotIndex)) return;

        var targetTrays = FindTraysBySeat(seatIndex1Based);
        if (targetTrays == null) return;

        var myId = trays.GetComponent<NetworkIdentity>();
        var targetId = targetTrays.GetComponent<NetworkIdentity>();
        if (targetId == null) return;
        if (myId != null && targetId == myId) return; // cannot give to self

        trays.Cmd_GiveItemToPlayer(targetId.netId, fromSlotIndex);
    }

    // -------- Helpers --------

    private bool IsValidInventorySlot(int index)
    {
        if (index < 0) return false;
        if (trays.inventory == null) return false;
        if (index >= trays.inventory.Count) return false;
        return true;
    }

    private NetworkIdentity RaycastTargetIdentity()
    {
        if (playerCamera == null) return null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, rayDistance, rayMask, QueryTriggerInteraction.Ignore))
        {
            var traysHit = hit.collider.GetComponentInParent<PlayerItemTrays>();
            if (traysHit != null)
            {
                return traysHit.GetComponent<NetworkIdentity>();
            }
        }
        return null;
    }

    private PlayerItemTrays FindTraysBySeat(int seatIndex1Based)
    {
        var all = GameObject.FindObjectsOfType<PlayerItemTrays>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].seatIndex1Based == seatIndex1Based)
                return all[i];
        }
        return null;
    }
}
