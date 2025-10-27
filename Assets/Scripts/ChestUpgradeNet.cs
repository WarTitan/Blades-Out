using UnityEngine;
using Mirror;
using System.Collections.Generic;

// Server-authoritative chest flow showing exactly 3 upgrade choices.
// Visuals: UI renders front face; effect text "+1 lv"; level badge "+1". No costs shown in preview.
// Off-turn only. Optional gold cost. Includes auto-pick when the player's next turn begins.
public class ChestUpgradeNet : NetworkBehaviour
{
    public static ChestUpgradeNet Instance;

    [Header("Chest Settings")]
    [SerializeField] private int chestGoldCost = 3;     // default 3 gold
    [SerializeField] private bool requireGold = true;   // set false to disable cost

    void Awake()
    {
        Instance = this;
    }

    private struct Offer
    {
        public uint playerNetId;
        public int[] handIndices;   // which hand index is upgraded if chosen
        public int[] cardIds;       // parallel card ids
        public byte[] targetLevels; // parallel next levels (current+1 clamped to Max)
    }

    // Keyed by player netId
    private readonly Dictionary<uint, Offer> activeOffers = new Dictionary<uint, Offer>();

    // -------- Helper: check turn safely on server --------
    [Server]
    private bool IsPlayersTurnServer(PlayerState ps)
    {
        var tm = TurnManager.Instance;
        if (tm == null || ps == null) return false;
        return tm.IsPlayersTurn(ps);
    }

    // -------- Entry point: called by your interactor when player clicks dealer (server) --------
    [Server]
    public void StartChestFor(PlayerState ps)
    {
        if (ps == null) return;

        if (IsPlayersTurnServer(ps))
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

        // Show UI on owner
        Target_ShowChestOffer(ps.connectionToClient, offer.cardIds, offer.targetLevels, offer.handIndices);
    }

    // Build unique, upgradable hand indices (representatives for each cardId)
    [Server]
    private List<int> BuildUpgradableCandidates(PlayerState ps)
    {
        var map = new Dictionary<int, int>(); // cardId -> representative hand index
        for (int i = 0; i < ps.handIds.Count && i < ps.handLvls.Count; i++)
        {
            int cardId = ps.handIds[i];
            var def = ps.database != null ? ps.database.Get(cardId) : null;
            if (def == null) continue;

            int current = ps.Server_GetEffectiveLevelForHandIndex(i);
            if (current >= def.MaxLevel) continue;

            if (!map.ContainsKey(cardId))
                map[cardId] = i;
        }
        return new List<int>(map.Values);
    }

    // Shuffle candidates and produce 3 parallel arrays (wrap if <3)
    [Server]
    private Offer BuildThreeChoiceOffer(PlayerState ps, List<int> upgradableHandIndices)
    {
        // Fisher-Yates
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

    // -------- Player clicked one of the three (client -> server) --------
    [Server]
    public void Server_ChooseFor(PlayerState ps, int chosenHandIndex)
    {
        if (ps == null || ps.isDead) return;

        // Chest is only intended off-turn; block manual clicks if it's your turn now
        if (IsPlayersTurnServer(ps)) return;

        if (!activeOffers.TryGetValue(ps.netId, out Offer offer)) return;

        int slot = -1;
        int cardId = -1;
        byte target = 0;

        for (int i = 0; i < offer.handIndices.Length; i++)
        {
            if (offer.handIndices[i] == chosenHandIndex)
            {
                slot = i;
                cardId = offer.cardIds[i];
                target = offer.targetLevels[i];
                break;
            }
        }
        if (slot < 0) return;

        ApplyUpgrade(ps, cardId, target, chosenHandIndex);
    }

    // -------- NEW: Auto-pick when the player's next turn begins --------
    // Version used by TurnManager via reflection (parameterless, runs on the player's component if attached there).
    [Server]
    public void Server_AutoPickRandomIfPending()
    {
        var ps = GetComponent<PlayerState>();
        if (ps == null) return;
        Server_AutoPickRandomIfPending(ps);
    }

    // Overload with explicit player (useful if this script is on a singleton/dealer)
    [Server]
    public void Server_AutoPickRandomIfPending(PlayerState ps)
    {
        if (ps == null) return;
        if (!activeOffers.TryGetValue(ps.netId, out Offer offer)) return;
        if (offer.handIndices == null || offer.handIndices.Length == 0) return;

        int pickIdx = Random.Range(0, offer.handIndices.Length);
        int handIndex = offer.handIndices[pickIdx];
        int cardId = offer.cardIds[pickIdx];
        byte target = offer.targetLevels[pickIdx];

        // NOTE: Auto-pick runs at the start of the player's own turn.
        // We bypass the off-turn restriction here on purpose.
        ApplyUpgrade(ps, cardId, target, handIndex);
    }

    // -------- Shared apply path (server) --------
    [Server]
    private void ApplyUpgrade(PlayerState ps, int cardId, byte newLevel, int handIndex)
    {
        var def = ps.database != null ? ps.database.Get(cardId) : null;
        if (def == null) return;

        int current = ps.Server_GetEffectiveLevelForHandIndex(handIndex);
        if (current >= def.MaxLevel) { ClearOffer(ps.netId); return; }

        byte finalLevel = (byte)Mathf.Min(def.MaxLevel, Mathf.Max(current + 1, newLevel));

        ps.upgradeLevels[cardId] = finalLevel;
        ps.Server_PropagateUpgradeToAllCopies(cardId);

        ClearOffer(ps.netId);
        Target_OnChestApplied(ps.connectionToClient, cardId, finalLevel, handIndex);
    }

    [Server]
    private void ClearOffer(uint playerNetId)
    {
        if (activeOffers.ContainsKey(playerNetId))
            activeOffers.Remove(playerNetId);
    }

    // ---------- Client RPCs ----------

    [TargetRpc]
    private void Target_ShowChestOffer(NetworkConnectionToClient target, int[] cardIds, byte[] targetLevels, int[] handIndices)
    {
        // Renders the three floating card fronts with "+1 lv" preview
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
