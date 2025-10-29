// FILE: ItemInstance.cs
// NEW FILE (ASCII only)
// Lightweight component on spawned 3D items in trays. Holds ids and owner/slot hints.

using UnityEngine;

[AddComponentMenu("Gameplay/Items/Item Instance")]
public class ItemInstance : MonoBehaviour
{
    public int itemId = -1;
    public bool isInventorySlot = true;
    public int slotIndex = -1;
    public PlayerItemTrays owner; // who currently owns this item (inventory or consume tray)
}
