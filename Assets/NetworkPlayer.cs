using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private HeartBar heartBar;
    [SerializeField] private Transform cardSpawnPoint;
    [SerializeField] private HeartBar heartBarPrefab; // assign in prefab

    [Header("Player Data")]
    public NetworkVariable<int> currentHearts = new NetworkVariable<int>(10);
    public NetworkVariable<int> maxHearts = new NetworkVariable<int>(10);

    // Public getters for external scripts
    public Camera PlayerCamera => playerCamera;
    public HeartBar HeartBar
    {
        get => heartBar;
        set => heartBar = value;
    }
    public Transform CardSpawnPoint => cardSpawnPoint;

    public override void OnNetworkSpawn()
    {
        // Auto-assign the camera if missing
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        // Auto-find CardSpawnPoint if missing
        if (cardSpawnPoint == null)
        {
            Transform found = transform.Find("CardSpawnPoint");
            if (found != null)
                cardSpawnPoint = found;
        }

        // Enable camera only for the local player
        if (IsOwner)
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Debug.LogWarning($"[{name}] Missing playerCamera.");
            }
        }
        else
        {
            if (playerCamera != null)
                playerCamera.enabled = false;
        }

        // Spawn the HeartBar automatically for the owner
        if (IsOwner && heartBar == null && heartBarPrefab != null && playerCamera != null)
        {
            Transform lookAtPoint = cardSpawnPoint != null ? cardSpawnPoint : transform;
            HeartBar hb = Instantiate(heartBarPrefab);
            hb.Initialize(playerCamera.transform, playerCamera, lookAtPoint);
            hb.SetHearts(currentHearts.Value, maxHearts.Value);
            heartBar = hb;
        }

        if (heartBar == null)
        {
            Debug.LogWarning($"[{name}] No HeartBar found or assigned for {playerCamera?.name ?? "Unknown Camera"}");
        }
    }

    [ServerRpc]
    public void TakeDamageServerRpc(int amount)
    {
        currentHearts.Value = Mathf.Clamp(currentHearts.Value - amount, 0, maxHearts.Value);
    }

    [ServerRpc]
    public void HealServerRpc(int amount)
    {
        currentHearts.Value = Mathf.Clamp(currentHearts.Value + amount, 0, maxHearts.Value);
    }
}
