// FILE: ItemInteraction.cs
// NEW FILE (ASCII only)
// Local-only click flow: click YOUR inventory item to select, then click a player to give it.
// Requires colliders on item prefabs and a collider on some part of each player root.

using UnityEngine;
using Mirror;

[AddComponentMenu("Gameplay/Items/Item Interaction (Local)")]
public class ItemInteraction : NetworkBehaviour
{
    public LayerMask raycastMask = ~0;
    public float maxDistance = 200f;

    private Camera cam;
    private PlayerItemTrays myTrays;
    private int selectedInvSlot = -1;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        cam = GetComponentInChildren<Camera>(true);
        myTrays = GetComponent<PlayerItemTrays>();
    }

    void Update()
    {
        if (!isLocalPlayer || cam == null || myTrays == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray r = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(r, out hit, maxDistance, raycastMask, QueryTriggerInteraction.Ignore))
            {
                // If nothing selected yet, try select an inventory item you own
                if (selectedInvSlot < 0)
                {
                    var inst = hit.collider.GetComponentInParent<ItemInstance>();
                    if (inst != null && inst.owner == myTrays && inst.isInventorySlot)
                    {
                        selectedInvSlot = inst.slotIndex;
                        // debug select feedback here if you want
                    }
                }
                else
                {
                    // We have an item selected: try to click a player to give it
                    var targetRoot = hit.collider.GetComponentInParent<PlayerItemTrays>();
                    if (targetRoot != null)
                    {
                        myTrays.Cmd_GiveItemToPlayer(selectedInvSlot, targetRoot.netIdentity);
                        selectedInvSlot = -1;
                    }
                    else
                    {
                        // clicked something else -> cancel selection
                        selectedInvSlot = -1;
                    }
                }
            }
            else
            {
                selectedInvSlot = -1;
            }
        }
    }
}
