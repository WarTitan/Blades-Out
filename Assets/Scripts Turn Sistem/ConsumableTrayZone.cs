using System.Collections.Generic;
using UnityEngine;
using Mirror;   // NEW: needed to detect local player via NetworkIdentity

[AddComponentMenu("Gameplay/Items/Consumable Tray Zone")]
public class ConsumableTrayZone : MonoBehaviour
{
    [Header("Seat")]
    [Tooltip("Seat index (1-based) this tray belongs to (1..5, etc.).")]
    public int seatIndex1Based = 1;

    [Header("Lines Setup")]
    [Tooltip("Root transform that contains the line segments. If left null, will try to find a child named 'lines'.")]
    public Transform linesRoot;

    [Header("Visuals")]
    public Color craftingColor = Color.white;
    public Color hoverColor = Color.green;

    [Tooltip("How fast the breathing (scale) effect plays.")]
    public float pulseSpeed = 1.0f;

    [Tooltip("Breathing amplitude (scale factor). 0.05 means ±5%.")]
    public float pulseScale = 0.05f;

    [Tooltip("Hide lines completely when not in crafting phase or seat inactive.")]
    public bool hideWhenNotCrafting = true;

    // Internal
    private readonly List<Renderer> lineRenderers = new();
    private readonly List<Vector3> baseScales = new();

    private bool isHovered = false;
    private bool seatActive = true;
    private bool isLocalSeat = false;   // NEW: true if this tray belongs to the local player on THIS client

    private float seatActiveCheckTimer = 0f;
    private const float SeatActiveCheckInterval = 1.0f; // seconds

    private void Awake()
    {
        if (linesRoot == null)
        {
            var child = transform.Find("lines");
            if (child != null)
                linesRoot = child;
        }

        if (linesRoot != null)
        {
            var renderers = linesRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                lineRenderers.Add(r);
                baseScales.Add(r.transform.localScale);
            }
        }

        if (lineRenderers.Count == 0)
        {
            Debug.LogWarning($"[ConsumableTrayZone] No line renderers found under {name}. " +
                             "Make sure you have a child 'lines' with meshes.");
        }
    }

    private void Update()
    {
        // 1) Check if seat is active and whether it belongs to the local player
        seatActiveCheckTimer -= Time.deltaTime;
        if (seatActiveCheckTimer <= 0f)
        {
            seatActiveCheckTimer = SeatActiveCheckInterval;
            UpdateSeatActiveAndLocal();
        }

        // 2) Check crafting phase
        bool crafting = false;
        var tm = TurnManagerNet.Instance;
        if (tm != null)
            crafting = (tm.phase == TurnManagerNet.Phase.Crafting);

        // 3) Visible only if:
        //    - crafting
        //    - seat has a player
        //    - this is NOT the local player's own seat
        bool visible = crafting && seatActive && !isLocalSeat;

        UpdateVisuals(visible);
    }

    /// <summary>
    /// Updates seatActive and isLocalSeat by scanning PlayerItemTrays in the scene.
    /// </summary>
    private void UpdateSeatActiveAndLocal()
    {
        seatActive = false;
        isLocalSeat = false;

        if (seatIndex1Based <= 0) return;

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<PlayerItemTrays>(FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
        var all = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
#endif
        if (all == null || all.Length == 0) return;

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;

            if (t.seatIndex1Based != seatIndex1Based)
                continue;

            seatActive = true;

            var ni = t.GetComponent<NetworkIdentity>();
            if (ni != null && ni.isLocalPlayer)
            {
                isLocalSeat = true;
            }

            // We found the seat we care about; no need to keep scanning.
            break;
        }
    }

    private void UpdateVisuals(bool visible)
    {
        if (lineRenderers.Count == 0) return;

        if (!visible && hideWhenNotCrafting)
        {
            // Hide completely
            for (int i = 0; i < lineRenderers.Count; i++)
            {
                if (lineRenderers[i] != null)
                {
                    lineRenderers[i].enabled = false;
                    // Reset scale
                    if (i < baseScales.Count && lineRenderers[i].transform != null)
                        lineRenderers[i].transform.localScale = baseScales[i];
                }
            }
            return;
        }

        // Visible -> enable renderers, update color and pulse
        float pulseFactor = 1f;
        if (visible)
        {
            float s = Mathf.Sin(Time.time * pulseSpeed);
            pulseFactor = 1f + s * pulseScale;
        }

        Color targetColor = isHovered ? hoverColor : craftingColor;

        for (int i = 0; i < lineRenderers.Count; i++)
        {
            var r = lineRenderers[i];
            if (r == null) continue;

            // Enable renderer
            r.enabled = visible; // if not hiding when not crafting, this still toggles

            // Scale (breathing)
            if (i < baseScales.Count && r.transform != null)
            {
                r.transform.localScale = baseScales[i] * (visible ? pulseFactor : 1f);
            }

            // Color (we clone the material to avoid affecting shared assets)
            if (visible)
            {
                var mat = r.material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mat.color = targetColor;
                }
            }
        }
    }

    /// <summary>
    /// Called by ItemInteraction when the player is dragging an item over this tray.
    /// </summary>
    public void SetHover(bool hovering)
    {
        isHovered = hovering;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Just to visualize the zone's collider in editor
        var box = GetComponent<Collider>() as BoxCollider;
        if (box == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
#endif
}
