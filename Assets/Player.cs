using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public Transform cardSpawnPoint;
    public Camera playerCamera;
    public HeartBar heartBar;

    public int maxHearts = 10;
    public int currentHearts = 10;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Enable the player’s camera only for the local player
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);
        }
        else
        {
            // Disable other players’ cameras
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
        }
    }

    public void TakeDamage(int amount)
    {
        currentHearts -= amount;
        currentHearts = Mathf.Clamp(currentHearts, 0, maxHearts);
        heartBar?.SetHearts(currentHearts, maxHearts);
    }
}
