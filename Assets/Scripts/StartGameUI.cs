using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartGameUI : MonoBehaviour
{
    public Button startButton;
    public TMP_Text statusTextTMP;  // assign if you use TMP
    public UnityEngine.UI.Text statusText; // or assign legacy Text

    private PlayerState localPlayer;

    void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
    }

    void Update()
    {
        if (localPlayer == null)
        {
#if UNITY_2023_1_OR_NEWER
            var arr = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
            var arr = Object.FindObjectsOfType<PlayerState>();
#endif
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].isLocalPlayer) { localPlayer = arr[i]; break; }
            }
            SetStatus(localPlayer != null ? "Local player ready" : "Waiting for local player...");
        }
    }

    private void OnStartClicked()
    {
        if (localPlayer != null)
        {
            localPlayer.CmdRequestStartGame();
            SetStatus("Start requested...");
        }
        else
        {
            SetStatus("No local player yet.");
        }
    }

    private void SetStatus(string s)
    {
        if (statusTextTMP != null) { statusTextTMP.text = s; return; }
        if (statusText != null) { statusText.text = s; return; }
    }
}
