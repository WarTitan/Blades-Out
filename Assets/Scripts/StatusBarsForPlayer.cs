using UnityEngine;
using Mirror;

public class StatusBarsForPlayer : NetworkBehaviour
{
    [Header("Prefabs (World-space canvases)")]
    public GameObject healthBarPrefab;   // HeartBarCanvas prefab
    public GameObject armorBarPrefab;    // ArmorBarCanvas prefab

    [Header("Follow Targets (set these to the two child anchors)")]
    public Transform barFollowTargetHealth; // e.g., playercamera/BarAnchorHelt
    public Transform barFollowTargetArmor;  // e.g., playercamera/BarAnchorArmor

    [Header("Optional: auto-find by name if fields are empty")]
    public string healthAnchorName = "BarAnchorHelt";
    public string armorAnchorName = "BarAnchorArmor";

    private PlayerState ps;
    [HideInInspector] public HeartBar healthBar;
    [HideInInspector] public HeartBar armorBar;

    void Awake()
    {
        ps = GetComponent<PlayerState>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Auto-find anchors if not assigned
        if (barFollowTargetHealth == null)
        {
            var t = transform.GetComponentsInChildren<Transform>(true);
            foreach (var x in t) if (x.name == healthAnchorName) { barFollowTargetHealth = x; break; }
            if (barFollowTargetHealth == null) barFollowTargetHealth = transform; // fallback
        }
        if (barFollowTargetArmor == null)
        {
            var t = transform.GetComponentsInChildren<Transform>(true);
            foreach (var x in t) if (x.name == armorAnchorName) { barFollowTargetArmor = x; break; }
            if (barFollowTargetArmor == null) barFollowTargetArmor = transform; // fallback
        }

        // Spawn bars
        if (healthBarPrefab != null)
        {
            var go = Instantiate(healthBarPrefab);
            healthBar = go.GetComponent<HeartBar>();
            if (healthBar != null)
            {
                // spin with player (no billboarding)
                healthBar.lookAtCamera = null;         // disable billboard
                healthBar.followTarget = barFollowTargetHealth;
                healthBar.followRotation = true;        // NEW: spin with anchor
                healthBar.yOffset = 0f;                 // we use separate anchors, so no extra offset
            }
        }

        if (armorBarPrefab != null)
        {
            var go = Instantiate(armorBarPrefab);
            armorBar = go.GetComponent<HeartBar>();
            if (armorBar != null)
            {
                armorBar.lookAtCamera = null;          // disable billboard
                armorBar.followTarget = barFollowTargetArmor;
                armorBar.followRotation = true;         // NEW: spin with anchor
                armorBar.yOffset = 0f;
            }
        }
    }

    void Update()
    {
        if (ps == null) return;

        if (healthBar != null) healthBar.SetHearts(ps.hp, ps.MaxHP);
        if (armorBar != null) armorBar.SetHearts(ps.armor, ps.MaxArmor);
    }
}
