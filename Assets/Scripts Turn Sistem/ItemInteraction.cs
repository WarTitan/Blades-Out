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

    [Header("Drag Settings")]
    [Tooltip("If true, drag ray uses mouse position. If false, uses center of screen (camera forward).")]
    public bool useMousePosition = false;

    [Tooltip("How much to lift the ghost above the hit point when you pick it up.")]
    public float dragHeightOffset = 0.4f;

    [Tooltip("How fast the ghost rises from the tray to dragHeightOffset (seconds).")]
    public float heightAnimDuration = 0.12f;

    [Header("Hover")]
    [Tooltip("Scale multiplier when hovering an inventory item.")]
    public float hoverScaleMultiplier = 1.12f;

    [Header("Debug")]
    public bool drawDebugRay = true;

    // Drag state
    private bool isDragging = false;
    private int draggingSlotIndex = -1;
    private ItemInstance dragSourceInstance;
    private GameObject dragGhost;

    // Original visual reference (so we can hide/show it)
    private GameObject sourceVisual;

    // Horizontal drag plane (Y constant, animated from start height to target)
    private Plane dragPlane;
    private bool dragPlaneValid = false;
    private float dragPlaneY;
    private float dragPlaneTargetY;
    private bool heightAnimating = false;

    // Hover state
    private ItemInstance hoveredInstance;
    private Vector3 hoveredOriginalScale;

    public override void OnStartLocalPlayer()
    {
        if (trays == null) trays = GetComponent<PlayerItemTrays>();
        ResolveCamera();
        Debug.Log("[ItemInteraction] Local ready. cam=" +
                  (playerCamera ? playerCamera.name : "null") +
                  " mask=" + rayMask.value);
    }

    private void ResolveCamera()
    {
        if (playerCamera != null) return;

        var lcc = GetComponent<LocalCameraController>();
        if (lcc != null && lcc.playerCamera != null)
            playerCamera = lcc.playerCamera;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        if (playerCamera == null)
        {
#if UNITY_2023_1_OR_NEWER
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
            var cams = GameObject.FindObjectsOfType<Camera>();
#pragma warning restore CS0618
#endif
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                {
                    playerCamera = cams[i];
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        bool crafting = IsCraftingPhase();

        // If the phase ended while dragging, cancel the drag.
        if (!crafting && isDragging)
        {
            CancelDrag("phase-ended");
        }

        // Ignore if clicking over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            UpdateHoverVisual(crafting: false);
            return;
        }

        // Hover highlight (only on own inventory items, mainly during crafting)
        UpdateHoverVisual(crafting);

        // Cancel drag via Esc / Right click
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelDrag("cancel-key");
        }

        // Start drag (only in crafting)
        if (Input.GetMouseButtonDown(0))
        {
            if (!isDragging)
            {
                TryBeginDrag();
            }
        }

        // Update ghost while dragging
        if (isDragging)
        {
            UpdateDragVisual();

            if (Input.GetMouseButtonUp(0))
            {
                TryDropDraggedItem();
            }
        }

        if (drawDebugRay && playerCamera != null)
        {
            Debug.DrawRay(playerCamera.transform.position,
                          playerCamera.transform.forward * 2.0f,
                          Color.cyan);
        }
    }

    // ---------- Hover highlight ----------

    private void UpdateHoverVisual(bool crafting)
    {
        // Don't hover while dragging
        if (isDragging)
        {
            ClearCurrentHover();
            return;
        }

        // If not crafting, you can still SEE your cards,
        // but let's turn off the hover "I can interact" feeling.
        if (!crafting)
        {
            ClearCurrentHover();
            return;
        }

        if (trays == null || playerCamera == null)
        {
            ClearCurrentHover();
            return;
        }

        Ray ray = MakeDragRay();
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, rayDistance, rayMask, QueryTriggerInteraction.Collide))
        {
            ClearCurrentHover();
            return;
        }

        var inst = hit.collider.GetComponentInParent<ItemInstance>();
        if (inst == null)
        {
            ClearCurrentHover();
            return;
        }

        // Only hover your own INVENTORY items
        if (!inst.isInventorySlot || inst.owner == null || inst.owner != trays)
        {
            ClearCurrentHover();
            return;
        }

        if (inst == hoveredInstance)
            return; // already highlighted

        // New hover target
        ClearCurrentHover();

        hoveredInstance = inst;
        var t = hoveredInstance.transform;
        hoveredOriginalScale = t.localScale;
        t.localScale = hoveredOriginalScale * hoverScaleMultiplier;
    }

    private void ClearCurrentHover()
    {
        if (hoveredInstance != null)
        {
            if (hoveredInstance.transform != null)
            {
                hoveredInstance.transform.localScale = hoveredOriginalScale;
            }
            hoveredInstance = null;
        }
    }

    // ---------- Drag start ----------

    private void TryBeginDrag()
    {
        if (!IsCraftingPhase())
        {
            // Not crafting -> no interaction
            return;
        }

        if (trays == null)
        {
            Debug.LogWarning("[ItemInteraction] No PlayerItemTrays on player.");
            return;
        }

        Ray ray = MakeDragRay();
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, rayDistance, rayMask, QueryTriggerInteraction.Collide))
        {
            Debug.Log("[ItemInteraction] Drag start raycast hit nothing.");
            return;
        }

        var inst = hit.collider.GetComponentInParent<ItemInstance>();
        if (inst == null)
        {
            Debug.Log("[ItemInteraction] Drag start hit " + hit.collider.name + " but no ItemInstance.");
            return;
        }

        // Must be your OWN INVENTORY item
        if (!inst.isInventorySlot || inst.owner == null || inst.owner != trays)
        {
            Debug.Log("[ItemInteraction] Drag start item is not in your INVENTORY.");
            return;
        }

        if (inst.slotIndex < 0 || inst.slotIndex >= trays.inventory.Count)
        {
            Debug.LogWarning("[ItemInteraction] Drag start: bad slotIndex " + inst.slotIndex);
            return;
        }

        // Clear hover scaling when we start dragging this item
        ClearCurrentHover();

        isDragging = true;
        draggingSlotIndex = inst.slotIndex;
        dragSourceInstance = inst;

        // Hide the original visual while dragging
        sourceVisual = inst.gameObject;
        if (sourceVisual != null)
            sourceVisual.SetActive(false);

        // Start position: same as original, then we animate plane up
        Vector3 startPos = sourceVisual != null ? sourceVisual.transform.position : hit.point;
        dragPlaneY = startPos.y;
        dragPlaneTargetY = startPos.y + dragHeightOffset;
        heightAnimating = true;

        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneY, 0f));
        dragPlaneValid = true;

        CreateDragGhostFromItem(inst, startPos);

        Debug.Log("[ItemInteraction] Begin drag from slot " + draggingSlotIndex +
                  " (itemId " + inst.itemId + ").");
    }

    private void CreateDragGhostFromItem(ItemInstance inst, Vector3 startPos)
    {
        if (dragGhost != null)
            Destroy(dragGhost);

        // Default: ghost prefab from ItemDeck
        GameObject ghostPrefab = null;

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
            var def = deck.Get(inst.itemId);
            if (def != null && def.visualPrefab != null)
                ghostPrefab = def.visualPrefab;
        }

        if (ghostPrefab != null)
        {
            dragGhost = Instantiate(ghostPrefab, startPos, Quaternion.identity);
        }
        else
        {
            dragGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dragGhost.transform.position = startPos;
            var r = dragGhost.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(r.sharedMaterial);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", new Color(0.9f, 0.9f, 1f));
                r.material = m;
            }
        }

        dragGhost.name = "DragGhost_Item" + inst.itemId;

        // Remove any network / item components so it is purely cosmetic
        var ni = dragGhost.GetComponent<NetworkIdentity>();
        if (ni != null) Destroy(ni);

        var instComp = dragGhost.GetComponent<ItemInstance>();
        if (instComp != null) Destroy(instComp);
    }

    // ---------- Drag update (XZ plane only, with height animation) ----------

    private void UpdateDragVisual()
    {
        if (!isDragging || dragGhost == null || !dragPlaneValid) return;

        // Animate plane height from start Y to target Y
        if (heightAnimating)
        {
            if (heightAnimDuration <= 0.0001f)
            {
                dragPlaneY = dragPlaneTargetY;
                heightAnimating = false;
            }
            else
            {
                float step = Time.deltaTime / heightAnimDuration;
                dragPlaneY = Mathf.Lerp(dragPlaneY, dragPlaneTargetY, step);

                if (Mathf.Abs(dragPlaneY - dragPlaneTargetY) < 0.001f)
                {
                    dragPlaneY = dragPlaneTargetY;
                    heightAnimating = false;
                }
            }
        }

        // Rebuild plane at the current animated height
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneY, 0f));

        Ray ray = MakeDragRay();
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 p = ray.GetPoint(enter);
            dragGhost.transform.position = p;
        }
    }

    // ---------- Drop / gift ----------

    private void TryDropDraggedItem()
    {
        if (!isDragging)
        {
            CancelDrag("not-dragging");
            return;
        }

        if (trays == null)
        {
            CancelDrag("no-trays");
            return;
        }

        if (draggingSlotIndex < 0 || draggingSlotIndex >= trays.inventory.Count)
        {
            CancelDrag("slot-invalid");
            return;
        }

        // If phase ended right before mouse up, treat as cancel
        if (!IsCraftingPhase())
        {
            CancelDrag("phase-ended-on-drop");
            return;
        }

        Ray ray = MakeDragRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, rayMask, QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[ItemInteraction] Drop raycast hit nothing, canceling.");
            CancelDrag("no-hit");
            return;
        }

        PlayerItemTrays targetTrays = null;
        RaycastHit chosen = hits[0];
        float best = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            // Skip hits on the drag ghost itself
            if (dragGhost != null && hits[i].collider != null &&
                hits[i].collider.transform.IsChildOf(dragGhost.transform))
            {
                continue;
            }

            var t = hits[i].collider.GetComponentInParent<PlayerItemTrays>();
            if (t != null && t != trays)
            {
                float d = hits[i].distance;
                if (d < best)
                {
                    best = d;
                    chosen = hits[i];
                    targetTrays = t;
                }
            }
        }

        if (targetTrays == null)
        {
            Debug.Log("[ItemInteraction] Drop: no PlayerItemTrays in ray hits, cancel.");
            CancelDrag("bad-target");
            return;
        }

        var targetId = targetTrays.GetComponent<NetworkIdentity>();
        if (targetId == null)
        {
            Debug.LogWarning("[ItemInteraction] Drop: target has no NetworkIdentity.");
            CancelDrag("no-netid");
            return;
        }

        // Send to SERVER. Server validates (phase, capacity, etc.).
        trays.Cmd_GiveItemToPlayer(targetId.netId, draggingSlotIndex);
        Debug.Log("[ItemInteraction] Drag-drop gift: fromSlot " + draggingSlotIndex +
                  " -> netId " + targetId.netId +
                  " (hit " + chosen.collider.name + ")");

        // Successful gift: keep the original hidden (server will update inventory)
        FinishDrag();
    }

    // ---------- Helpers ----------

    private Ray MakeDragRay()
    {
        ResolveCamera();

        if (playerCamera != null)
        {
            if (useMousePosition)
            {
                return playerCamera.ScreenPointToRay(Input.mousePosition);
            }
            else
            {
                return new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            }
        }

        // Fallback if no camera assigned
        Vector3 origin = transform.position + Vector3.up * 1.6f;
        Vector3 dir = transform.forward;
        return new Ray(origin, dir);
    }

    private bool IsCraftingPhase()
    {
        var tm = TurnManagerNet.Instance;
        if (tm == null) return false;
        return tm.phase == TurnManagerNet.Phase.Crafting;
    }

    private void CancelDrag(string why)
    {
        if (isDragging)
            Debug.Log("[ItemInteraction] Cancel drag (" + why + ").");
        CleanupDragState(restoreOriginal: true);
    }

    private void FinishDrag()
    {
        // Gift sent -> do NOT restore original visual; inventory will change via server
        CleanupDragState(restoreOriginal: false);
    }

    private void CleanupDragState(bool restoreOriginal)
    {
        isDragging = false;
        draggingSlotIndex = -1;
        dragSourceInstance = null;
        dragPlaneValid = false;
        heightAnimating = false;

        if (restoreOriginal && sourceVisual != null)
            sourceVisual.SetActive(true);

        sourceVisual = null;

        if (dragGhost != null)
            Destroy(dragGhost);

        dragGhost = null;
    }
}
