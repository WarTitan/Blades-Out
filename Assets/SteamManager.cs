using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    private static SteamManager _instance;
    public static bool Initialized { get; private set; }

    private void Awake()
    {
        // Singleton pattern (only one SteamManager)
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            if (!Packsize.Test())
            {
                Debug.LogError("[Steamworks.NET] Packsize Test failed!");
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[Steamworks.NET] DllCheck Test failed!");
            }

            SteamAPI.Init();
            Initialized = true;
            Debug.Log($"[Steamworks.NET] Steam initialized as {SteamFriends.GetPersonaName()}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Steamworks.NET] Steam initialization failed: {e}");
            Initialized = false;
        }
    }

    private void Update()
    {
        if (Initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SteamAPI.Shutdown();
            Initialized = false;
            _instance = null;
        }
    }
}
