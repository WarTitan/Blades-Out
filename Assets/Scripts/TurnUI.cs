using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System.Text;
using System.Linq;

public class TurnUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button endTurnButton;
    public Button upgradeFirstCardButton;

    [Header("Info Labels (assign one)")]
    [SerializeField] private TMP_Text infoTextTMP;   // modern TMP label
    [SerializeField] private Text infoTextLegacy;    // legacy UI.Text fallback

    private PlayerState localPlayer;
    private PlayerState[] cachedPlayers = new PlayerState[0];

    void Start()
    {
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurn);
        if (upgradeFirstCardButton != null) upgradeFirstCardButton.onClick.AddListener(OnUpgradeFirstCard);
    }

    void Update()
    {
        // Find local player if needed
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
        }

        // Refresh cached players list (order by seatIndex)
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<PlayerState>();
#endif
        cachedPlayers = all.OrderBy(p => p.seatIndex).ThenBy(p => p.netId).ToArray();

        // Build multiline scoreboard
        var sb = new StringBuilder(256);
        var tm = TurnManager.Instance;

        if (cachedPlayers.Length == 0)
        {
            sb.AppendLine("Waiting for players…");
        }
        else
        {
            // Header: show whose turn it is (if known)
            if (tm != null && localPlayer != null)
            {
                var current = cachedPlayers.FirstOrDefault(p => tm.IsPlayersTurn(p));
                if (current != null)
                {
                    sb.AppendLine($"▶ Turn: P{(current.seatIndex >= 0 ? (current.seatIndex + 1).ToString() : "?")} {(current == localPlayer ? "(YOU)" : "")}");
                }
            }

            // Rows: one per player
            for (int i = 0; i < cachedPlayers.Length; i++)
            {
                var ps = cachedPlayers[i];
                if (ps == null) continue;

                bool isTurn = (tm != null && tm.IsPlayersTurn(ps));
                string seat = ps.seatIndex >= 0 ? $"P{ps.seatIndex + 1}" : "P?";
                string me = ps == localPlayer ? " (YOU)" : "";

                // Chips shown as current/max
                // Hand count + Set count are useful for a quick overview
                int hand = ps.handIds != null ? ps.handIds.Count : 0;
                int setc = ps.setIds != null ? ps.setIds.Count : 0;

                sb.Append(isTurn ? "► " : "  ");
                sb.Append(seat).Append(me).Append("  ");
                sb.Append("HP ").Append(ps.hp).Append('/')
                  .Append(ps.maxHP).Append("  ");
                sb.Append("Armor ").Append(ps.armor).Append('/')
                  .Append(ps.maxArmor).Append("  ");
                sb.Append("Gold ").Append(ps.gold).Append("  ");
                sb.Append("Chips ").Append(ps.chips).Append('/')
                  .Append(ps.maxChips).Append("  ");
                sb.Append("Hand ").Append(hand).Append("  ");
                sb.Append("Set ").Append(setc);
                sb.AppendLine();
            }
        }

        SetInfo(sb.ToString());
    }

    private void SetInfo(string s)
    {
        if (infoTextTMP != null) { infoTextTMP.text = s; return; }
        if (infoTextLegacy != null) { infoTextLegacy.text = s; return; }
    }

    private void OnEndTurn()
    {
        if (localPlayer != null) localPlayer.CmdEndTurn();
    }

    private void OnUpgradeFirstCard()
    {
        if (localPlayer == null) return;
        if (localPlayer.handIds.Count == 0) return;
        localPlayer.CmdUpgradeCard(0);
    }
}
