using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener playerAudioListener;
    [SerializeField] private Transform cardSpawnPoint;
    [SerializeField] private HeartBar heartBarPrefab;

    private HeartBar heartBarInstance;

    // Simple health system for each player
    public int MaxHearts { get; private set; } = 10;
    public int CurrentHearts { get; private set; } = 10;

    // Public accessors used by CardDeckManager
    public Camera PlayerCamera => playerCamera;
    public Transform CardSpawnPoint => cardSpawnPoint;
    public HeartBar HeartBar { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log($"Server spawned player {OwnerClientId}");
        }

        // Disable remote players' cameras and audio
        if (!IsOwner)
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
            return;
        }

        // Enable local player's camera and audio
        if (playerCamera != null) playerCamera.enabled = true;
        if (playerAudioListener != null) playerAudioListener.enabled = true;

        // Spawn and initialize the HeartBar
        if (heartBarPrefab != null)
        {
            heartBarInstance = Instantiate(heartBarPrefab);
            heartBarInstance.Initialize(transform, playerCamera, cardSpawnPoint);
            HeartBar = heartBarInstance;
        }
        else
        {
            Debug.LogWarning("No HeartBar prefab assigned on " + gameObject.name);
        }

        // Server-only spawn positioning
        if (IsServer && CardDeckManager.Instance != null)
        {
            transform.position = CardDeckManager.Instance.GetSpawnPositionForPlayer(OwnerClientId);
            transform.LookAt(Vector3.zero);
        }
    }

    // Helper accessors
    public Transform GetCardSpawnPoint() => cardSpawnPoint;
    public HeartBar GetHeartBar() => heartBarInstance;
}
