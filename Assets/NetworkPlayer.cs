using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener playerListener;
    [SerializeField] private Transform cardSpawnPoint;
    [SerializeField] private GameObject heartBarPrefab;

    private GameObject heartBarInstance;

    public int CurrentHearts { get; private set; } = 3;
    public int MaxHearts { get; private set; } = 3;

    private void Awake()
    {
        // Auto-find camera and listener if not assigned
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
        if (playerListener == null)
            playerListener = GetComponentInChildren<AudioListener>(true);
    }

    public override void OnNetworkSpawn()
    {
        // Disable all cameras by default
        if (playerCamera != null)
            playerCamera.enabled = false;
        if (playerListener != null)
            playerListener.enabled = false;

        if (!IsOwner)
            return;

        Debug.Log("Local player spawned: " + OwnerClientId);

        // Enable camera + listener for local player
        if (playerCamera != null)
            playerCamera.enabled = true;
        if (playerListener != null)
            playerListener.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Move to proper spawn and face table center
        if (CardDeckManager.Instance != null)
        {
            transform.position = CardDeckManager.Instance.GetSpawnPositionForPlayer(OwnerClientId);
            transform.rotation = CardDeckManager.Instance.GetSpawnRotationForPlayer(OwnerClientId);
        }
    }

    [ServerRpc]
    private void RequestSpawnServerRpc(ulong clientId)
    {
        Vector3 spawnPos = CardDeckManager.Instance.GetSpawnPositionForPlayer(clientId);
        Quaternion spawnRot = CardDeckManager.Instance.GetSpawnRotationForPlayer(clientId);

        transform.SetPositionAndRotation(spawnPos, spawnRot);
        ApplySpawnClientRpc(spawnPos, spawnRot);
    }

    [ClientRpc]
    private void ApplySpawnClientRpc(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
    }

    public void SpawnHeartBar()
    {
        if (heartBarPrefab != null && heartBarInstance == null)
        {
            heartBarInstance = Instantiate(
                heartBarPrefab,
                transform.position + Vector3.up * 2f,
                Quaternion.identity);
            heartBarInstance.transform.SetParent(transform);
        }
    }

    public Camera PlayerCamera => playerCamera;
    public Transform CardSpawnPoint => cardSpawnPoint;
    public GameObject HeartBar => heartBarInstance;
}
