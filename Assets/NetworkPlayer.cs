using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public HeartBar heartBar;
    public Transform cardSpawnPoint;

    [Header("Player Data")]
    public NetworkVariable<int> currentHearts = new NetworkVariable<int>(10);
    public NetworkVariable<int> maxHearts = new NetworkVariable<int>(10);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Enable only the local player's camera
            playerCamera.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Disable remote player cameras
            if (playerCamera != null)
                playerCamera.enabled = false;
        }

        // Setup heart bar visuals
        if (heartBar != null)
        {
            heartBar.Initialize(cardSpawnPoint, playerCamera, cardSpawnPoint);
            heartBar.SetHearts(currentHearts.Value, maxHearts.Value);
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
