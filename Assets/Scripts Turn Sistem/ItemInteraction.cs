// FILE: ItemInteraction.cs
// FULL FILE (ASCII only)
//
// Local-player component for selecting an inventory item with the crosshair and
// gifting it to another player by clicking them. This version NEVER calls any
// [Server] methods locally. All gating/validation happens on the server in
// PlayerItemTrays.Cmd_GiveItemToPlayer.
//
// Flow:
//   - Left click #1: raycast hits your own inventory item (ItemInstance.owner == this trays, isInventorySlot==true)
//                    -> remember its slot index.
//   - Left click #2: raycast (RaycastAll) finds closest PlayerItemTrays that is not yours
//                    -> send Cmd_GiveItemToPlayer(target.netId, selectedSlot)
//   - Right click / Esc: cancel selection.
//
// Notes:
//   - Uses camera-forward ray from the active player camera (crosshair).
//   - rayMask should include the layers for items and for the other players' colliders.
//   - UI clicks are ignored if an EventSystem exists.
//   - All permission/turn checks are performed on the SERVER in Cmd_GiveItemToPlayer.
//
// Requirements:
//   - PlayerItemTrays on each player; it spawns ItemInstance on visuals.
//   - NetworkIdentity on the same GameObject as this script.

using UnityEngine;
using Mirror;
using UnityEngine.EventSystems;

[AddComponentMenu("Gameplay/Items/Item Interaction")]
public class ItemInteraction : NetworkBehaviour
{
    [Header("Refs (auto if left empty)")]
    public PlayerItemTrays trays;
    public Camera playerCamera;

    [Header("Ray")]
    public LayerMask rayMask = ~0;
    public float rayDistance = 100f;

    [Header("Debug")]
    public bool drawDebugRay = true;

    // current selected inventory slot on this local player (-1 means none)
    private int selectedInvSlot = -1;

    public override void OnStartLocalPlayer()
    {
        if (trays == null) trays = GetComponent<PlayerItemTrays>();
        ResolveCamera();
        Debug.Log("[ItemInteraction] Local ready. cam=" + (playerCamera ? playerCamera.name : "null") + " mask=" + rayMask.value);
    }

    private void ResolveCamera()
    {
        if (playerCamera != null) return;

        // If you have a LocalCameraController that exposes playerCamera, prefer it
        var lcc = GetComponent<LocalCameraController>();
        if (lcc != null && lcc.playerCamera != null) playerCamera = lcc.playerCamera;

        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main;

        if (playerCamera == null)
        {
#pragma warning disable CS0618
            var cams = GameObject.FindObjectsOfType<Camera>();
#pragma warning disable CS0618
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i].enabled && cams[i].gameObject.activeInHierarchy) { playerCamera = cams[i]; break; }
            }
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // ignore UI clicks if using a cursor at times
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            ClearSelection("cancel");
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (selectedInvSlot < 0)
            {
                TrySelectOwnInventoryItemUnderCrosshair();
            }
            else
            {
                TryGiftToPlayerUnderCrosshair();
            }
        }

        if (drawDebugRay && playerCamera != null)
        {
            Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * 2.0f, Color.cyan);
        }
    }

    private void TrySelectOwnInventoryItemUnderCrosshair()
    {
        if (trays == null) { Debug.LogWarning("[ItemInteraction] No PlayerItemTrays on player."); return; }

        Ray ray = MakeCenterRay();
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, rayDistance, rayMask, QueryTriggerInteraction.Collide))
        {
            var inst = hit.collider.GetComponentInParent<ItemInstance>();
            if (inst == null)
            {
                Debug.Log("[ItemInteraction] First click hit " + hit.collider.name + " but no ItemInstance.");
                return;
            }

            // Must be YOUR item and in INVENTORY, not consume
            if (!inst.isInventorySlot || inst.owner == null || inst.owner != trays)
            {
                Debug.Log("[ItemInteraction] Item is not in your INVENTORY (owner mismatch or consume side).");
                return;
            }

            if (inst.slotIndex < 0 || inst.slotIndex >= trays.inventory.Count)
            {
                Debug.LogWarning("[ItemInteraction] Bad slotIndex on item: " + inst.slotIndex);
                return;
            }

            selectedInvSlot = inst.slotIndex;
            Debug.Log("[ItemInteraction] Selected inventory slot " + selectedInvSlot + " (itemId " + inst.itemId + ").");
        }
        else
        {
            Debug.Log("[ItemInteraction] First click raycast hit nothing.");
        }
    }

    private void TryGiftToPlayerUnderCrosshair()
    {
        if (trays == null)
        {
            Debug.LogWarning("[ItemInteraction] No PlayerItemTrays on local player.");
            ClearSelection("missing-trays");
            return;
        }

        if (selectedInvSlot < 0 || selectedInvSlot >= trays.inventory.Count)
        {
            Debug.LogWarning("[ItemInteraction] Selected slot invalid now (maybe item moved).");
            ClearSelection("invalid-slot");
            return;
        }

        Ray ray = MakeCenterRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, rayMask, QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[ItemInteraction] Second click raycast hit nothing.");
            return;
        }

        PlayerItemTrays targetTrays = null;
        RaycastHit chosen = hits[0];
        float best = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            // Find the closest transform that has PlayerItemTrays in its parents and is not me
            var t = hits[i].collider.GetComponentInParent<PlayerItemTrays>();
            if (t != null && t != trays)
            {
                float d = hits[i].distance;
                if (d < best) { best = d; chosen = hits[i]; targetTrays = t; }
            }
        }

        if (targetTrays == null)
        {
            Debug.Log("[ItemInteraction] No PlayerItemTrays in ray hits (closest was " + hits[0].collider.name + ").");
            ClearSelection("bad-target");
            return;
        }

        var targetId = targetTrays.GetComponent<NetworkIdentity>();
        if (targetId == null)
        {
            Debug.LogWarning("[ItemInteraction] Target has no NetworkIdentity.");
            ClearSelection("no-netid");
            return;
        }

        // Send to SERVER. Server will validate (turn, memory, capacity) inside Cmd_GiveItemToPlayer.
        trays.Cmd_GiveItemToPlayer(targetId.netId, selectedInvSlot);
        Debug.Log("[ItemInteraction] Requested gift: fromSlot " + selectedInvSlot + " -> netId " + targetId.netId +
                  " (hit " + chosen.collider.name + ")");

        ClearSelection("sent");
    }

    private Ray MakeCenterRay()
    {
        ResolveCamera();
        Vector3 origin;
        Vector3 dir;
        if (playerCamera != null)
        {
            origin = playerCamera.transform.position;
            dir = playerCamera.transform.forward;
        }
        else
        {
            origin = transform.position + Vector3.up * 1.6f;
            dir = transform.forward;
        }
        return new Ray(origin, dir);
    }

    private void ClearSelection(string why)
    {
        if (selectedInvSlot >= 0)
            Debug.Log("[ItemInteraction] Cleared selection (" + why + ").");
        selectedInvSlot = -1;
    }
}
