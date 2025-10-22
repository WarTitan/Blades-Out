using UnityEngine;
using Mirror;

[RequireComponent(typeof(PlayerState))]
[RequireComponent(typeof(PlayerChestUpgrades))]
public class ChestInteractor : NetworkBehaviour
{
    [Header("Interaction")]
    public LayerMask dealerMask;
    public float interactRange = 5f;

    private Camera cam;
    private PlayerState ps;
    private PlayerChestUpgrades upgrades;

    void Start()
    {
        if (!isLocalPlayer)
        {
            enabled = false;
            return;
        }

        if (dealerMask.value == 0)
            dealerMask = Physics.DefaultRaycastLayers;

        cam = GetComponentInChildren<Camera>(true);
        ps = GetComponent<PlayerState>();
        upgrades = GetComponent<PlayerChestUpgrades>();

        if (upgrades == null)
        {
            Debug.LogError("PlayerChestUpgrades is missing on the Network Player prefab. Add it in the Editor.");
            enabled = false;
            return;
        }
        if (ps == null)
        {
            Debug.LogError("PlayerState is missing on the Network Player.");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Input.GetMouseButtonDown(0))
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (cam == null || ps == null || upgrades == null) return;

        if (ps.IsYourTurn()) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, dealerMask))
        {
            if (hit.collider.GetComponentInParent<DealerChestMarker>() == null)
                return;

            upgrades.CmdRequestChestUpgrade();
        }
    }
}
