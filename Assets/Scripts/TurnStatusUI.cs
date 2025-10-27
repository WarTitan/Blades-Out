using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

[AddComponentMenu("Turn/Turn Status UI")]
public class TurnStatusUI : MonoBehaviour
{
    [Header("UI Setup")]
    [Tooltip("If left empty, a TextMeshProUGUI will be auto-created as a child named 'TurnStatusText'.")]
    public TextMeshProUGUI text;

    [Tooltip("Optional background image behind the text.")]
    public Image background;

    [Tooltip("Optional CanvasGroup for a smooth fade in/out when showing/hiding.")]
    public CanvasGroup group;

    [Header("Style")]
    public TMP_FontAsset fontAsset;
    public int fontSize = 36;
    public Color textColor = Color.white;
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;

    [Header("Layout (on the RectTransform of the text)")]
    public Vector2 anchoredPosition = new Vector2(0, -20);  // relative to top-center
    public Vector2 sizeDelta = new Vector2(900, 80);

    [Header("Behavior")]
    [Tooltip("How often (in seconds) to refresh the string to avoid garbage. 0 = every frame.")]
    public float refreshInterval = 0.08f;

    [Header("Visibility Gates")]
    [Tooltip("If true, this UI hides while the lobby is active (LobbyStage.lobbyActive == true).")]
    public bool hideInLobby = true;

    [Tooltip("If true, this UI hides until a turn actually begins (currentTurnNetId != 0).")]
    public bool hideUntilFirstTurnStarts = true;

    [Tooltip("Fade speed when using CanvasGroup. Higher = snappier.")]
    public float fadeLerp = 14f;

    float nextRefreshTime = 0f;

    void Reset()
    {
        EnsureTextExists();
        AutoAnchorTopCenter();
    }

    void Awake()
    {
        EnsureTextExists();
        ApplyStyle();
        AutoAnchorTopCenter();

        // If a CanvasGroup is present but has no initial alpha, start hidden (nice for lobby)
        if (group != null) group.alpha = 0f;
    }

    void EnsureTextExists()
    {
        if (text != null) return;

        // Create child TextMeshProUGUI under this object
        var go = new GameObject("TurnStatusText", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        text = go.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;

#if TMP_PRESENT
        // keep consistent with newer TMP
#endif
        // Use new API (not the obsolete enableWordWrapping)
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void AutoAnchorTopCenter()
    {
        if (text == null) return;
        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        if (background != null)
        {
            var br = background.rectTransform;
            br.anchorMin = rt.anchorMin;
            br.anchorMax = rt.anchorMax;
            br.pivot = rt.pivot;
            br.anchoredPosition = rt.anchoredPosition;
            br.sizeDelta = rt.sizeDelta;
        }
    }

    void ApplyStyle()
    {
        if (text == null) return;
        if (fontAsset) text.font = fontAsset;
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = alignment;
    }

    void Update()
    {
        // Let you tweak in Inspector live
        ApplyStyle();
        AutoAnchorTopCenter();

        bool show = ShouldShow();

        // Smooth fade if CanvasGroup provided, else simple enable/disable
        if (group != null)
        {
            float target = show ? 1f : 0f;
            float k = 1f - Mathf.Exp(-fadeLerp * Time.unscaledDeltaTime);
            group.alpha = Mathf.Lerp(group.alpha, target, k);
            group.interactable = show;
            group.blocksRaycasts = show;
        }
        else
        {
            if (text) text.enabled = show;
            if (background) background.enabled = show;
        }

        // Skip text building if hidden or we want to throttle updates
        if (!show) return;

        if (refreshInterval > 0f && Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, refreshInterval);

        if (text) text.text = BuildStatusLine();
    }

    bool ShouldShow()
    {
        // Hide in lobby if requested
        if (hideInLobby && LobbyStageIsActive())
            return false;

        // Hide until game has a turn owner if requested
        var timer = TurnTimerNet.Instance;
        if (hideUntilFirstTurnStarts && (timer == null || timer.currentTurnNetId == 0))
            return false;

        // Optional: you can also hide when not connected as a client if desired
        // if (!NetworkClient.isConnected) return false;

        return true;
    }

    bool LobbyStageIsActive()
    {
        // Return true if we detect a lobby stage object and it's active
        if (LobbyStage.Instance != null)
        {
            return LobbyStage.Instance.lobbyActive;
        }
        return false;
    }

    string BuildStatusLine()
    {
        var timer = TurnTimerNet.Instance;
        if (timer == null)
            return "Waiting for turn info...";

        // Seconds remaining (server-synced end time vs local network clock)
        int remain = Mathf.CeilToInt(timer.GetSecondsRemaining());

        // Determine if it's my turn
        bool myTurn = false;
        string ownerName = "Player";

        // Find local player state (if connected)
        PlayerState myPs = null;
        if (NetworkClient.active && NetworkClient.localPlayer != null)
            myPs = NetworkClient.localPlayer.GetComponent<PlayerState>();

        var tm = TurnManager.Instance;

        if (myPs != null && tm != null)
            myTurn = tm.IsPlayersTurn(myPs);
        else if (myPs != null)
            myTurn = (timer.currentTurnNetId == myPs.netId);

        // If not my turn, try to look up the owner's display name from netId
        if (!myTurn)
        {
            ownerName = LookupNameByNetId(timer.currentTurnNetId);
        }

        // Compose final line
        if (myTurn)
        {
            return "Your turn (" + Mathf.Max(0, remain) + ")";
        }
        else
        {
            return ownerName + " turn — you can upgrade your cards now (" + Mathf.Max(0, remain) + ")";
        }
    }

    string LookupNameByNetId(uint netId)
    {
        if (netId == 0) return "Player";

        // Find PlayerState with matching netId
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<PlayerState>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var ps = all[i];
            if (ps != null && ps.netId == netId)
            {
                // Prefer a PlayerNameNet component if you have one
                var nameNet = ps.GetComponent<PlayerNameNet>();
                if (nameNet != null && !string.IsNullOrEmpty(nameNet.displayName))
                    return nameNet.displayName;

                // Fallback: seat number or generic label
                return "Player " + (ps.seatIndex + 1);
            }
        }

        return "Player";
    }
}
