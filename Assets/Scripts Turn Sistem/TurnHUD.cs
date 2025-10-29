// FILE: TurnHUD.cs
// FULL REPLACEMENT (ASCII)
// Pre-trade: "Craft items now" + countdown.
// Turn: "<PlayerName>'s turn" + "Ends in mm:ss".

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

[AddComponentMenu("Gameplay/Turn HUD")]
public class TurnHUD : MonoBehaviour
{
    [Header("Assign UI (either Text or TMP_Text)")]
    public GameObject titleObject;
    public GameObject timerObject;

    private Text titleText;
    private Text timerText;
    private TMP_Text titleTMP;
    private TMP_Text timerTMP;

    private TurnManagerNet net;

    void Awake()
    {
        Cache();
    }

    void OnEnable()
    {
        net = FindObjectOfType<TurnManagerNet>();
    }

    void Update()
    {
        if (net == null)
        {
            SetTitle("Waiting...");
            SetTimer("");
            return;
        }

        if (net.phase == TurnManagerNet.Phase.PreTrade)
        {
            SetTitle("Craft items now:");
            double remain = net.phaseEndTime - NetworkTime.time;
            if (remain < 0) remain = 0;
            SetTimer(FormatTime(remain));
        }
        else if (net.phase == TurnManagerNet.Phase.Turn)
        {
            string who = ResolveTurnName(net.currentTurnNetId, net.currentSeat);
            SetTitle(who + "'s turn");
            double remain = net.turnEndTime - NetworkTime.time;
            if (remain < 0) remain = 0;
            SetTimer("Ends in " + FormatTime(remain));
        }
        else
        {
            SetTitle("Waiting...");
            SetTimer("");
        }
    }

    private string ResolveTurnName(uint netId, int seat)
    {
        if (netId != 0)
        {
            var all = FindObjectsOfType<NetworkIdentity>();
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
        return "Seat " + seat;
    }

    private void Cache()
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

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        int s = Mathf.FloorToInt((float)seconds);
        int m = s / 60;
        s = s % 60;
        return m.ToString("00") + ":" + s.ToString("00");
    }
}
