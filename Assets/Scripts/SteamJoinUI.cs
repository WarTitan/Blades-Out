using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using Steamworks;

[AddComponentMenu("Networking/Steam Join UI (Fizzy, Auto Wire)")]
public class SteamJoinUI : MonoBehaviour
{
    [Header("Refs")]
    public MirrorBootstrap bootstrap;           // assign in Inspector or auto-resolve
    public TMP_InputField joinSteamIdInput;     // paste host's 17-digit SteamID
    public TMP_Text mySteamIdText;              // shows your own SteamID
    public TMP_Text logText;                    // optional: status output

    [Header("Buttons (optional)")]
    public Button hostButton;                   // click -> Host (Steam)
    public Button joinButton;                   // click -> Join (Steam)

    [Header("Auto-wire by GameObject name (optional)")]
    public bool autoFindByName = true;
    public string hostButtonName = "BtnHostSteam";
    public string joinButtonName = "BtnJoinSteam";
    public string inputFieldName = "InputHostSteamID";

    void Awake()
    {
        if (bootstrap == null) bootstrap = FindObjectOfType<MirrorBootstrap>();

        // Try auto-find UI refs if not assigned
        if (autoFindByName)
        {
            if (hostButton == null && !string.IsNullOrEmpty(hostButtonName))
            {
                var go = GameObject.Find(hostButtonName);
                if (go) hostButton = go.GetComponent<Button>();
            }
            if (joinButton == null && !string.IsNullOrEmpty(joinButtonName))
            {
                var go = GameObject.Find(joinButtonName);
                if (go) joinButton = go.GetComponent<Button>();
            }
            if (joinSteamIdInput == null && !string.IsNullOrEmpty(inputFieldName))
            {
                var go = GameObject.Find(inputFieldName);
                if (go) joinSteamIdInput = go.GetComponent<TMP_InputField>();
            }
        }

        // Hook listeners if buttons are present
        if (hostButton != null)
        {
            hostButton.onClick.RemoveListener(OnClick_HostSteam);
            hostButton.onClick.AddListener(OnClick_HostSteam);
        }
        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(OnClick_JoinSteam);
            joinButton.onClick.AddListener(OnClick_JoinSteam);
        }
    }

    void Start()
    {
        // Display your own SteamID so friends can join you
        if (mySteamIdText != null)
        {
            string myId = "(steam not init)";
            try { myId = SteamUser.GetSteamID().ToString(); } catch { }
            mySteamIdText.text = "My SteamID: " + myId;
        }
    }

    void Update()
    {
        // Keyboard fallbacks
        if (Input.GetKeyDown(KeyCode.F5)) OnClick_HostSteam();
        if (Input.GetKeyDown(KeyCode.F6)) OnClick_JoinSteam();
    }

    // Button: Host via Steam (Fizzy)
    public void OnClick_HostSteam()
    {
        if (bootstrap == null) { Log("[UI] No MirrorBootstrap found."); return; }
        bootstrap.UseFizzySteamworks();   // select Fizzy transport
        bootstrap.StartHostActive();      // start hosting
        Log("[UI] Hosting via Steam (Fizzy)...");
    }

    // Button: Join via Steam (Fizzy) using host's SteamID string
    public void OnClick_JoinSteam()
    {
        if (bootstrap == null) { Log("[UI] No MirrorBootstrap found."); return; }
        if (bootstrap.networkManager == null) { Log("[UI] No NetworkManager."); return; }

        string id = joinSteamIdInput != null ? joinSteamIdInput.text.Trim() : "";
        if (string.IsNullOrEmpty(id))
        {
            Log("[UI] Enter the host's 17-digit SteamID.");
            return;
        }

        bootstrap.UseFizzySteamworks();
        bootstrap.networkManager.networkAddress = id;  // Fizzy treats this as host SteamID
        bootstrap.StartClientActive();
        Log("[UI] Joining host " + id + " via Steam...");
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (logText != null) logText.text = msg;
    }
}
