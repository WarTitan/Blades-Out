using UnityEngine;
using Mirror;

public class StatusBarsForPlayer : NetworkBehaviour
{
    [Header("Prefabs (World-space)")]
    public GameObject healthBarPrefab;   // your HeartBarCanvas prefab (hearts)
    public GameObject armorBarPrefab;    // duplicate of HeartBarCanvas with shield sprites

    [Header("Offsets")]
    public float healthYOffset = 1.10f;  // height above HandAnchor for health
    public float armorYOffset = 1.35f;  // height above HandAnchor for armor (stacked)

    private PlayerState ps;
    private HeartBar healthBar;
    private HeartBar armorBar;
    private Transform handAnchor;        // TableSeatAnchors anchor for this seat

    void Awake()
    {
        ps = GetComponent<PlayerState>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 1) Resolve this player's HandAnchor from seatIndex (same as HandVisualizer)
        if (TableSeatAnchors.Instance != null && ps != null)
        {
            handAnchor = TableSeatAnchors.Instance.GetHandAnchor(ps.seatIndex);
        }

        // 2) Find the local camera (each client faces their own camera)
        Camera cam = Camera.main;

        // 3) Spawn health + armor bars (world-space canvases)
        if (healthBarPrefab != null)
        {
            var go = Instantiate(healthBarPrefab);
            healthBar = go.GetComponent<HeartBar>();
            if (healthBar != null)
            {
                healthBar.Initialize(handAnchor, cam, handAnchor);
            }
            // per-bar height handled in LateUpdate via child offset helper below
            AddYOffset(go.transform, healthYOffset);
        }

        if (armorBarPrefab != null)
        {
            var go = Instantiate(armorBarPrefab);
            armorBar = go.GetComponent<HeartBar>();
            if (armorBar != null)
            {
                armorBar.Initialize(handAnchor, cam, handAnchor);
            }
            AddYOffset(go.transform, armorYOffset);
        }
    }

    void Update()
    {
        if (ps == null) return;

        // Update heart/shield counts every frame (cheap & simple). You can Optimize with change events later.
        if (healthBar != null)
        {
            healthBar.SetHearts(ps.hp, ps.MaxHP);
        }
        if (armorBar != null)
        {
            armorBar.SetHearts(ps.armor, ps.MaxArmor);
        }
    }

    private static void AddYOffset(Transform t, float y)
    {
        // Create a parent that offsets the bar upward from the anchor; keeps HeartBar's own logic intact.
        var parent = new GameObject("BarOffset").transform;
        parent.position = t.position;
        parent.rotation = t.rotation;
        t.SetParent(parent, true);
        parent.position += Vector3.up * y;
    }
}
