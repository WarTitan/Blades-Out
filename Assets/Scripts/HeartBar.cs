using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeartBar : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Ordered left->right. Size can be > max; extra slots will be hidden.")]
    public List<Image> slots = new List<Image>();

    [Tooltip("Sprite for a filled heart/shield.")]
    public Sprite fullSprite;

    [Tooltip("Sprite for an empty heart/shield (only used if Hide Empty Slots is OFF).")]
    public Sprite emptySprite;

    [Header("Behavior")]
    [Tooltip("If ON, slots at/above current value are deactivated (no empty icons).")]
    public bool hideEmptySlots = true;

    [Header("Auto-fill (optional)")]
    [Tooltip("Parent transform that holds your Image children in order. Use context menu to populate.")]
    public Transform slotsContainer;

    [ContextMenu("Populate Slots From Children")]
    public void PopulateSlotsFromChildren()
    {
        if (slotsContainer == null)
        {
            Debug.LogWarning("[HeartBar] Set 'slotsContainer' (the parent with your Image children) first.");
            return;
        }
        slots.Clear();
        for (int i = 0; i < slotsContainer.childCount; i++)
        {
            var img = slotsContainer.GetChild(i).GetComponent<Image>();
            if (img != null) slots.Add(img);
        }
        Debug.Log("[HeartBar] Slots populated: " + slots.Count);
    }

    [Header("Billboard (optional)")]
    public Transform followTarget;   // e.g., seat/anchor
    public Camera lookAtCamera;      // Camera.main typically
    public Transform upReference;    // not used by default, but available

    [Tooltip("Vertical offset applied on top of followTarget.position.y (meters).")]
    public float yOffset = 0f;

    [Tooltip("Extra tilt toward camera around X (degrees). Small values like -10..-30.")]
    public float extraTiltX = -15f;

    // cache last values to avoid unnecessary work
    private int lastCurrent = -999;
    private int lastMax = -999;

    public void Initialize(Transform follow, Camera cam, Transform upRef)
    {
        followTarget = follow;
        lookAtCamera = cam;
        upReference = upRef;
    }

    /// <summary>
    /// Update the bar. current = value you have (e.g., hp), max = maximum (e.g., MaxHP).
    /// </summary>
    public void SetHearts(int current, int max)
    {
        // clamp for safety
        if (current < 0) current = 0;
        if (max < 0) max = 0;
        if (current > max) current = max;

        if (current == lastCurrent && max == lastMax) return;
        lastCurrent = current;
        lastMax = max;

        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            var img = slots[i];
            if (img == null) continue;

            // Hide any slot beyond max (so changing max later is safe)
            if (i >= max)
            {
                img.gameObject.SetActive(false);
                continue;
            }

            // For all slots within max:
            if (i < current)
            {
                // filled
                img.gameObject.SetActive(true);
                if (fullSprite != null) img.sprite = fullSprite;
            }
            else
            {
                // empty or hidden
                if (hideEmptySlots)
                {
                    img.gameObject.SetActive(false);
                }
                else
                {
                    img.gameObject.SetActive(true);
                    if (emptySprite != null) img.sprite = emptySprite;
                }
            }
        }
    }

    void LateUpdate()
    {
        // Billboard + follow (optional)
        if (followTarget != null)
        {
            Vector3 p = followTarget.position;
            p.y += yOffset;
            transform.position = p;
        }

        if (lookAtCamera != null)
        {
            // Face camera while keeping a gentle tilt
            var camPos = lookAtCamera.transform.position;
            Vector3 toCam = camPos - transform.position;
            toCam.y = 0f; // yaw-only facing; keeps the bar upright in world Y
            if (toCam.sqrMagnitude > 0.0001f)
            {
                Quaternion yawOnly = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                transform.rotation = yawOnly * Quaternion.Euler(extraTiltX, 0f, 0f);
            }
        }
    }
}
