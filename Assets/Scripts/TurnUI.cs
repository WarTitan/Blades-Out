using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public class TurnUI : MonoBehaviour
{
    public Button endTurnButton;
    public Button upgradeFirstCardButton;

    [SerializeField] private TMP_Text infoTextTMP;
    [SerializeField] private Text infoTextLegacy;

    private PlayerState localPlayer;

    void Start()
    {
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurn);
        if (upgradeFirstCardButton != null) upgradeFirstCardButton.onClick.AddListener(OnUpgradeFirstCard);
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
        }

        if (localPlayer != null)
        {
            SetInfo("HP: " + localPlayer.hp +
                    "  Armor: " + localPlayer.armor +
                    "  Gold: " + localPlayer.gold +
                    "  Hand: " + localPlayer.handIds.Count);
        }
        else
        {
            SetInfo("Waiting for local player...");
        }
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
