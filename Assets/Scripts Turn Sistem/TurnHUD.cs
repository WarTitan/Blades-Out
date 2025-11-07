using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Text;

[AddComponentMenu("Gameplay/Turn HUD")]
public class TurnHUD : MonoBehaviour
{
    public static TurnHUD Instance;

    [Header("Assign UI (either Text or TMP_Text)")]
    public GameObject titleObject;
    public GameObject timerObject;

    [Header("Strike Board (optional, manual only)")]
    public bool showStrikeBoard = false;
    public GameObject strikeBoardObject;

    [Header("Font (optional for legacy Text)")]
    public Font overrideFont;

    [Header("Singleton Settings")]
    public bool preferThisInstance = false;
    public bool dontDestroyOnLoad = true;

    private static bool s_resolved = false;
    private static TurnHUD s_winner = null;

    private Text titleText;
    private Text timerText;
    private TMP_Text titleTMP;
    private TMP_Text timerTMP;

    private Text strikeBoardText;
    private TMP_Text strikeBoardTMP;

    private TurnManagerNet net;

    void Awake()
    {
        ResolveSingleton();

        if (s_winner != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad && transform.root != null)
            DontDestroyOnLoad(transform.root.gameObject);

        CacheRefs();

        var child = transform.Find("StrikeBoard");
        if (child != null) Destroy(child.gameObject);

        if (!showStrikeBoard && strikeBoardObject != null)
            strikeBoardObject.SetActive(false);
    }

    private void ResolveSingleton()
    {
        if (s_resolved) return;

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<TurnHUD>(FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
        var all = FindObjectsOfType<TurnHUD>(true);
#pragma warning restore CS0618
#endif

        int bestScore = int.MinValue;
        TurnHUD best = null;

        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            int score = 0;

            if (h.preferThisInstance) score += 1000;
            if (h.titleObject != null) score += 3;
            if (h.timerObject != null) score += 3;
            if (IsUnderCanvas(h.transform)) score += 3;
            if (h.isActiveAndEnabled) score += 1;
            if (h.gameObject.activeInHierarchy) score += 1;
            if (h.gameObject.scene.IsValid()) score += 1;

            if (score > bestScore) { bestScore = score; best = h; }
        }

        s_winner = best;
        s_resolved = true;
    }

    private static bool IsUnderCanvas(Transform t)
    {
        Transform p = t;
        while (p != null)
        {
            if (p.GetComponent<Canvas>() != null) return true;
            p = p.parent;
        }
        return false;
    }

    void OnEnable()
    {
        if (s_winner != this) return;

#if UNITY_2023_1_OR_NEWER
        net = Object.FindFirstObjectByType<TurnManagerNet>();
#else
#pragma warning disable CS0618
        net = FindObjectOfType<TurnManagerNet>();
#pragma warning restore CS0618
#endif
    }

    void Update()
    {
        if (s_winner != this) return;

        if (net == null)
        {
            SetTitle("Stage: Waiting");
            SetTimer("");
            return;
        }

        if (net.phase == TurnManagerNet.Phase.Waiting)
        {
            SetTitle("Stage: Waiting");
            SetTimer("");
        }
        else if (net.phase == TurnManagerNet.Phase.Crafting)
        {
            double remain = net.turnEndTime - NetworkTime.time;
            if (remain < 0) remain = 0;
            SetTitle("Stage: Crafting (all players)");
            SetTimer("Ends in " + FormatTime(remain));
        }
        else if (net.phase == TurnManagerNet.Phase.Turn)
        {
            string who = ResolveTurnName(net.currentTurnNetId, net.currentSeat);
            int lvl = MemoryLevelTracker.GetLevelForNetId(net.currentTurnNetId);
            var mt = MemoryStrikeTracker.FindForNetId(net.currentTurnNetId);
            int strikes = (mt != null) ? mt.strikes : 0;

            if (net.memoryActive)
            {
                double remain = net.memoryEndTime - NetworkTime.time;
                if (remain < 0) remain = 0;
                SetTitle("Stage: Memory  |  " + who + "  |  LVL " + lvl +
                         "  |  Strikes " + strikes + "/" + MemoryStrikeTracker.MaxStrikes);
                SetTimer("Ends in " + FormatTime(remain));
            }
            else
            {
                SetTitle("Stage: Delivery");
                SetTimer("");
            }
        }
        else
        {
            SetTitle("Stage: Waiting");
            SetTimer("");
        }

        if (showStrikeBoard && strikeBoardObject != null)
        {
            if (!strikeBoardObject.activeSelf) strikeBoardObject.SetActive(true);
            SetBoard(BuildStrikeBoard());
        }
    }

    private string BuildStrikeBoard()
    {
#if UNITY_2023_1_OR_NEWER
        var trays = Object.FindObjectsByType<PlayerItemTrays>(FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
        var trays = GameObject.FindObjectsOfType<PlayerItemTrays>();
#pragma warning restore CS0618
#endif
        if (trays.Length == 0) return "No players.";

        PlayerItemTrays[] bySeat = new PlayerItemTrays[6]; // seats 1..5
        for (int i = 0; i < trays.Length; i++)
        {
            var t = trays[i];
            if (t.seatIndex1Based <= 0 || t.seatIndex1Based >= bySeat.Length) continue;
            bySeat[t.seatIndex1Based] = t;
        }

        var sb = new StringBuilder(256);
        for (int seat = 1; seat <= 5; seat++)
        {
            var t = bySeat[seat];
            if (t == null) continue;

            var id = t.GetComponent<NetworkIdentity>();
            uint netId = (id != null) ? id.netId : 0;
            string name = ResolveTurnName(netId, seat);

            var mt = MemoryStrikeTracker.FindForNetId(netId);
            int strikes = (mt != null) ? mt.strikes : 0;
            bool eliminated = (mt != null) && mt.eliminated;

            int lvl = MemoryLevelTracker.GetLevelForNetId(netId);

            sb.Append("Seat ").Append(seat).Append(": ").Append(name)
              .Append("  |  LVL ").Append(lvl)
              .Append("  |  Strikes ").Append(strikes).Append("/")
              .Append(MemoryStrikeTracker.MaxStrikes);

            if (eliminated) sb.Append("  |  ELIMINATED");

            sb.AppendLine();
        }
        return sb.ToString();
    }

    private string ResolveTurnName(uint netId, int seat)
    {
        if (netId != 0)
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
            var all = GameObject.FindObjectsOfType<NetworkIdentity>();
#pragma warning restore CS0618
#endif
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].netId == netId)
                {
                    var nameNet = all[i].GetComponent<PlayerNameNet>();
                    if (nameNet != null && !string.IsNullOrEmpty(nameNet.displayName))
                        return nameNet.displayName;
                    break;
                }
            }
        }
        if (seat > 0) return "Seat " + seat;
        return (netId != 0) ? ("Player " + netId) : "Unknown";
    }

    private void CacheRefs()
    {
        if (titleObject != null)
        {
            titleText = titleObject.GetComponent<Text>();
            titleTMP = titleObject.GetComponent<TMP_Text>();
        }
        if (timerObject != null)
        {
            timerText = timerObject.GetComponent<Text>();
            timerTMP = timerObject.GetComponent<TMP_Text>();
        }
        if (strikeBoardObject != null)
        {
            strikeBoardText = strikeBoardObject.GetComponent<Text>();
            strikeBoardTMP = strikeBoardObject.GetComponent<TMP_Text>();

            if (strikeBoardText != null)
            {
                if (overrideFont != null) strikeBoardText.font = overrideFont;
                else if (strikeBoardText.font == null) strikeBoardText.font = GetDefaultFont();
            }
        }
    }

    private Font GetDefaultFont()
    {
        if (overrideFont != null) return overrideFont;

        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        if (f == null)
        {
            try { f = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
        }
        return f;
    }

    private void SetTitle(string s)
    {
        if (titleText != null) titleText.text = s;
        if (titleTMP != null) titleTMP.text = s;
    }

    private void SetTimer(string s)
    {
        if (timerText != null) timerText.text = s;
        if (timerTMP != null) timerTMP.text = s;
    }

    private void SetBoard(string s)
    {
        if (strikeBoardText != null) strikeBoardText.text = s;
        if (strikeBoardTMP != null) strikeBoardTMP.text = s;
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        int s = Mathf.FloorToInt((float)seconds);
        int m = s / 60;
        s = s % 60;
        return m.ToString("00") + ":" + s.ToString("00");
    }
}
