using UnityEngine;
using Mirror;
using System.Collections.Generic;

// Server-authoritative chest flow (no animation) showing exactly 3 upgrade choices.
// Visuals: UI renders front face; effect text "+1 lv"; level badge "+1". No costs shown in preview.
public class ChestUpgradeNet : NetworkBehaviour
{
    public static ChestUpgradeNet Instance;

    [Header("Chest Settings")]
    [SerializeField] private int chestGoldCost = 3;    // keep or set requireGold=false
    [SerializeField] private bool requireGold = true;  // set false to disable cost without code changes

    void Awake()
    {
        Instance = this;
    }

    private struct Offer
    {
        public uint playerNetId;
        public int[] handIndices;
        public int[] cardIds;
        public byte[] targetLevels;
    }

    private readonly Dictionary<uint, Offer> activeOffers = new Dictionary<uint, Offer>();

    [Server]
    public void StartChestFor(PlayerState ps)
    {
        if (ps == null) return;

        if (ps.IsYourTurn())
        {
            Target_ChestDenied(ps.connectionToClient, "[Chest] Only available off-turn.");
            return;
        }
        if (ps.isDead)
        {
            Target_ChestDenied(ps.connectionToClient, "[Chest] You are dead.");
            return;
        }

        if (activeOffers.ContainsKey(ps.netId))
        {
            Target_ChestDenied(ps.connectionToClient, "[Chest] Already opening a chest.");
            return;
        }

        var candidates = BuildUpgradableCandidates(ps);
        if (candidates.Count == 0)
        {
            Target_ChestDenied(ps.connectionToClient, "[Chest] No cards can be upgraded right now.");
            return;
        }

        if (requireGold)
        {
            if (ps.gold < chestGoldCost)
            {
                Target_ChestDenied(ps.connectionToClient, "[Chest] You need " + chestGoldCost + " gold.");
                return;
            }
            ps.gold -= chestGoldCost;
        }

        var offer = BuildThreeChoiceOffer(ps, candidates);
        activeOffers[ps.netId] = offer;

        Target_ShowChestOffer(ps.connectionToClient, offer.cardIds, offer.targetLevels, offer.handIndices);
    }

    [Server]
    private List<int> BuildUpgradableCandidates(PlayerState ps)
    {
        var uniqueCardToHandIndex = new Dictionary<int, int>(); // cardId -> representative hand index

        for (int i = 0; i < ps.handIds.Count && i < ps.handLvls.Count; i++)
        {
            int cardId = ps.handIds[i];
            var def = ps.database != null ? ps.database.Get(cardId) : null;
            if (def == null) continue;

            int current = ps.Server_GetEffectiveLevelForHandIndex(i);
            if (current >= def.MaxLevel) continue;

            if (!uniqueCardToHandIndex.ContainsKey(cardId))
                uniqueCardToHandIndex[cardId] = i;
        }

        return new List<int>(uniqueCardToHandIndex.Values);
    }

    [Server]
    private Offer BuildThreeChoiceOffer(PlayerState ps, List<int> upgradableHandIndices)
    {
        // Shuffle
        for (int i = 0; i < upgradableHandIndices.Count; i++)
        {
            int j = Random.Range(i, upgradableHandIndices.Count);
            int t = upgradableHandIndices[i];
            upgradableHandIndices[i] = upgradableHandIndices[j];
            upgradableHandIndices[j] = t;
        }

        var handIdx = new List<int>(3);
        var cardIds = new List<int>(3);
        var targetLvls = new List<byte>(3);

        if (upgradableHandIndices.Count == 0)
        {
            return new Offer
            {
                playerNetId = ps.netId,
                handIndices = new int[0],
                cardIds = new int[0],
                targetLevels = new byte[0]
            };
        }

        for (int k = 0; k < 3; k++)
        {
            int index = upgradableHandIndices[k % upgradableHandIndices.Count];
            int cardId = ps.handIds[index];
            var def = ps.database != null ? ps.database.Get(cardId) : null;
            if (def == null) continue;

            int current = ps.Server_GetEffectiveLevelForHandIndex(index);
            int next = Mathf.Min(def.MaxLevel, current + 1);

            handIdx.Add(index);
            cardIds.Add(cardId);
            targetLvls.Add((byte)next);
        }

        return new Offer
        {
            playerNetId = ps.netId,
            handIndices = handIdx.ToArray(),
            cardIds = cardIds.ToArray(),
            targetLevels = targetLvls.ToArray()
        };
    }

    // Server-side apply, called via PlayerChestUpgrades (client authority)
    [Server]
    public void Server_ChooseFor(PlayerState ps, int chosenHandIndex)
    {
        if (ps == null || ps.IsYourTurn() || ps.isDead) return;

        if (!activeOffers.TryGetValue(ps.netId, out Offer offer)) return;

        int found = -1;
        int cardId = -1;
        byte target = 0;

        for (int i = 0; i < offer.handIndices.Length; i++)
        {
            if (offer.handIndices[i] == chosenHandIndex)
            {
                found = i;
                cardId = offer.cardIds[i];
                target = offer.targetLevels[i];
                break;
            }
        }
        if (found < 0) return;

        var def = ps.database != null ? ps.database.Get(cardId) : null;
        if (def == null) return;

        int current = ps.Server_GetEffectiveLevelForHandIndex(chosenHandIndex);
        if (current >= def.MaxLevel) return;

        byte finalLevel = (byte)Mathf.Min(def.MaxLevel, current + 1);

        ps.upgradeLevels[cardId] = finalLevel;
        ps.Server_PropagateUpgradeToAllCopies(cardId);

        activeOffers.Remove(ps.netId);
        Target_OnChestApplied(ps.connectionToClient, cardId, finalLevel, chosenHandIndex);
    }

    // ---------- Client RPCs ----------

    [TargetRpc]
    private void Target_ShowChestOffer(NetworkConnectionToClient target, int[] cardIds, byte[] targetLevels, int[] handIndices)
    {
        ChestUpgradePicker.ShowChoices(cardIds, targetLevels, handIndices);
    }

    [TargetRpc]
    private void Target_ChestDenied(NetworkConnectionToClient target, string message)
    {
        Debug.LogWarning(message);
    }

    [TargetRpc]
    private void Target_OnChestApplied(NetworkConnectionToClient target, int cardId, byte newLevel, int handIndex)
    {
        Debug.Log("[Chest] Applied upgrade: hand[" + handIndex + "] cardId " + cardId + " -> L" + newLevel);
        ChestUpgradePicker.HideChoices();
    }
}
