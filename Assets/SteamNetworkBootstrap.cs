using UnityEngine;
using Unity.Netcode;
using Steamworks;

public class SteamNetworkBootstrap : MonoBehaviour
{
    [SerializeField] private CardDeckManager cardDeckManager;

    private void Start()
    {
        // 1️⃣ Check Steam
        if (!SteamManager.Initialized)
        {
            Debug.LogError("❌ Steam not initialized. Please start the game through Steam.");
            return;
        }

        string steamName = SteamFriends.GetPersonaName();
        Debug.Log($"✅ Steam initialized as {steamName}");

        // 2️⃣ Start network host or client
        // For now, start as host automatically (for testing)
        // Later we’ll add UI buttons
        if (SystemInfo.deviceName.Contains("HOST") || Application.isEditor)
        {
            Debug.Log("🟢 Starting as Host...");
            NetworkManager.Singleton.StartHost();
        }
        else
        {
            Debug.Log("🔵 Starting as Client...");
            NetworkManager.Singleton.StartClient();
        }

        // 3️⃣ Link CardDeckManager
        if (cardDeckManager == null)
        {
            cardDeckManager = FindObjectOfType<CardDeckManager>();
        }

        if (cardDeckManager == null)
        {
            Debug.LogWarning("⚠️ No CardDeckManager found in scene!");
        }
    }

    // Optional UI buttons (can be connected later)
    public void StartHost() => NetworkManager.Singleton.StartHost();
    public void StartClient() => NetworkManager.Singleton.StartClient();
}
