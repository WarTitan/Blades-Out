using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeartBar : MonoBehaviour
{
    [Header("UI")]
    public List<Image> slots = new List<Image>();
    public Sprite fullSprite;
    public Sprite emptySprite;

    [Header("Behavior")]
    public bool hideEmptySlots = true;

    [Header("Auto-fill (optional)")]
    public Transform slotsContainer;

    [ContextMenu("Populate Slots From Children")]
    public void PopulateSlotsFromChildren()
    {
        if (slotsContainer == null)
        {
            Debug.LogWarning("[HeartBar] Set 'slotsContainer' first.");
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

    [Header("Follow / Facing")]
    public Transform followTarget;     // set by spawner
    public Camera lookAtCamera;      // leave null to disable billboard
    public Transform upReference;

    [Tooltip("Meters added on top of followTarget Y. Use 0 if you have separate anchors.")]
    public float yOffset = 0f;

    [Tooltip("Extra pitch tilt in degrees (negative to lean toward camera/front).")]
    public float extraTiltX = -15f;

    [Tooltip("If true, bar matches the followTarget yaw (spins with player).")]
    public bool followRotation = false;

    [Tooltip("Extra yaw relative to followTarget when followRotation is ON.")]
    public float yawNudge = 0f;

    private int lastCurrent = -999;
    private int lastMax = -999;

    public void Initialize(Transform follow, Camera cam, Transform upRef)
    {
        followTarget = follow;
        lookAtCamera = cam;
        upReference = upRef;
    }

    public void SetHearts(int current, int max)
    {
        if (current < 0) current = 0;
        if (max < 0) max = 0;
        if (current > max) current = max;

        if (current == lastCurrent && max == lastMax) return;
        lastCurrent = current; lastMax = max;

        for (int i = 0; i < slots.Count; i++)
        {
            var img = slots[i];
            if (img == null) continue;

            if (i >= max) { img.gameObject.SetActive(false); continue; }

            if (i < current)
            {
                img.gameObject.SetActive(true);
                if (fullSprite != null) img.sprite = fullSprite;
            }
            else
            {
                if (hideEmptySlots) { img.gameObject.SetActive(false); }
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
        // Position
        if (followTarget != null)
        {
            Vector3 p = followTarget.position;
            p.y += yOffset;
            transform.position = p;
        }

        // Rotation
        if (followRotation && followTarget != null)
        {
            // Spin with player: match ONLY yaw and add a gentle pitch
            float yaw = followTarget.eulerAngles.y + yawNudge;
            transform.rotation = Quaternion.Euler(extraTiltX, yaw, 0f);
        }
        else if (lookAtCamera != null)
        {
            // Billboard (yaw-only), if you prefer camera-facing
            var camPos = lookAtCamera.transform.position;
            Vector3 toCam = camPos - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                Quaternion yawOnly = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                transform.rotation = yawOnly * Quaternion.Euler(extraTiltX, 0f, 0f);
            }
        }
    }
}
