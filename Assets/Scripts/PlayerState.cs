using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerState : NetworkBehaviour
{
    [Header("Refs")]
    public CardDatabase database;

    [Header("Core Stats")]
    [SyncVar] public int hp = 5;
    [SyncVar] public int armor = 5;
    [SyncVar] public int gold = 0;

    [SyncVar] public int maxHP = 5;
    [SyncVar] public int maxArmor = 5;

    // For UI scripts that expect properties
    public int MaxHP => maxHP;
    public int MaxArmor => maxArmor;

    [Header("Chips (Hearthstone-style growth at END of your turn)")]
    [SyncVar(hook = nameof(OnChipsChanged))] public int chips = 0;  // current
    [SyncVar(hook = nameof(OnMaxChipsChanged))] public int maxChips = 0;  // increases via TurnManager

    public event System.Action<PlayerState> ChipsChanged;
    public event System.Action<PlayerState> MaxChipsChanged;
    void OnChipsChanged(int oldV, int newV) => ChipsChanged?.Invoke(this);
    void OnMaxChipsChanged(int oldV, int newV) => MaxChipsChanged?.Invoke(this);

    [Header("Seat / Turn")]
    [SyncVar] public int seatIndex = -1;
    [HideInInspector] public TurnManager turnManager;

    // Rows
    public readonly SyncList<int> handIds = new SyncList<int>();
    public readonly SyncList<byte> handLvls = new SyncList<byte>();
    public readonly SyncList<int> setIds = new SyncList<int>();
    public readonly SyncList<byte> setLvls = new SyncList<byte>();

    // Global upgrades per cardId (affects all copies + future draws)
    public readonly SyncDictionary<int, byte> upgradeLevels = new SyncDictionary<int, byte>();

    // Action safety
    private bool actionLock = false;
    private float lastActionTime = -999f;
    [SerializeField] private float minActionInterval = 0.10f;

    // Statuses
    private readonly List<StatusData> statuses = new List<StatusData>();
    private struct StatusData { public StatusType type; public int magnitude; public int turns; }
    private enum StatusType { Poison, CactusReflect, TurtleSkip, C4Fuse }

    // For Mirror card (copy last played)
    private int lastPlayedCardId = -1;
    private int lastPlayedCardLevel = 1;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterPlayer(this);
            turnManager = TurnManager.Instance;
        }
    }

    // ───────────────────────────────────────────────── Game init / draw ─────────────────────────────────────────────────

    /// <summary>
    /// Start-of-game setup:
    /// - 5 gold
    /// - Chips start 1/1
    /// - Exactly 3 starting cards
    /// </summary>
    [Server]
    public void Server_Init(CardDatabase db, int deckSize, int startGold, int startHand)
    {
        database = db;

        gold = 5;
        maxChips = 1;
        chips = 1;

        hp = maxHP;
        armor = maxArmor;

        handIds.Clear(); handLvls.Clear();
        setIds.Clear(); setLvls.Clear();
        // upgradeLevels.Clear(); // uncomment if you want upgrades reset per match

        const int startingCards = 3;
        for (int i = 0; i < startingCards; i++)
        {
            int id = database != null ? database.GetRandomId() : -1;
            if (id >= 0)
            {
                handIds.Add(id);
                handLvls.Add(GetLevelForNewInstance(id));
            }
        }
    }

    [Server]
    public void Server_Draw(int count)
    {
        if (count <= 0 || database == null) return;
        for (int i = 0; i < count; i++)
        {
            int id = database.GetRandomId();
            if (id >= 0)
            {
                handIds.Add(id);
                handLvls.Add(GetLevelForNewInstance(id));
            }
        }
    }

    // ───────────────────────────────────────────────── Chips helpers ─────────────────────────────────────────────────

    [Server] public void Server_RefillChipsToMax() { chips = maxChips; }

    /// <summary>Called by TurnManager at END of your turn (increase + refill).</summary>
    [Server]
    public void Server_IncreaseMaxChipsAndRefill(int cap, int amount = 1)
    {
        int newMax = Mathf.Min(cap, maxChips + Mathf.Max(1, amount));
        maxChips = newMax;
        chips = maxChips;
    }

    [Server]
    public bool Server_TrySpendChips(int amount)
    {
        if (amount <= 0) return true;
        if (chips < amount) return false;
        chips -= amount;
        return true;
    }

    [TargetRpc]
    void TargetActionDenied(NetworkConnection target, string reason)
        => Debug.LogWarning(reason);

    // ───────────────────────────────────────────────── Commands ─────────────────────────────────────────────────

    [Command] public void CmdRequestStartGame() => TurnManager.Instance?.Server_AttemptStartGame();
    [Command] public void CmdEndTurn() => TurnManager.Instance?.Server_EndTurn(this);
    [Command] public void CmdUpgradeCard(int handIndex) => TurnManager.Instance?.Server_UpgradeCard(this, handIndex);

    /// <summary>Play an instant card on YOUR turn. Costs chips. Set-only cards cannot be cast.</summary>
    [Command]
    public void CmdPlayInstant(int handIndex, uint targetNetId)
    {
        if (!IsYourTurn()) { TargetActionDenied(connectionToClient, "[Cast] Only during YOUR turn."); return; }
        if (!ValidHandIndex(handIndex)) return;

        if (Time.time - lastActionTime < minActionInterval) return;
        lastActionTime = Time.time;
        if (actionLock) return;

        var def = database != null ? database.Get(handIds[handIndex]) : null;
        if (def == null) return;
        if (def.playStyle == CardDefinition.PlayStyle.SetReaction) return; // cannot cast set-only

        int lvl = handLvls[handIndex];
        int cost = def.GetCastChipCost(lvl);
        if (!Server_TrySpendChips(cost))
        {
            TargetActionDenied(connectionToClient, "[Cast] Not enough chips (" + cost + " needed).");
            return;
        }

        var target = FindPlayerByNetId(targetNetId);

        // Mirror special-case: stays in hand
        bool keepInHand = (def.effect == CardDefinition.EffectType.Mirror_CopyLastPlayedByYou);

        actionLock = true;
        try
        {
            if (!keepInHand)
                Server_RemoveHandAt_Internal(handIndex);

            CardEffectResolver.PlayInstant(def, lvl, this, target);
        }
        finally { actionLock = false; }
    }

    /// <summary>Set a reaction card ONLY when it's NOT your turn. Free (no chips).</summary>
    [Command(requiresAuthority = false)]
    public void CmdSetCard(int handIndex)
    {
        var senderIdentity = connectionToClient != null ? connectionToClient.identity : null;
        var senderPS = senderIdentity ? senderIdentity.GetComponent<PlayerState>() : null;
        if (senderPS == null || senderPS != this) return;

        if (IsYourTurn()) { TargetActionDenied(connectionToClient, "[Set] Only OFF-turn."); return; }

        if (Time.time - lastActionTime < minActionInterval) return;
        lastActionTime = Time.time;

        if (actionLock) return;
        if (!Server_IsSetReactionInHand(handIndex)) return;

        actionLock = true;
        try { Server_SetCard_ByIndex(handIndex); }
        finally { actionLock = false; }
    }

    // ───────────────────────────────────────────────── Helpers ─────────────────────────────────────────────────

    public bool IsYourTurn()
        => (TurnManager.Instance != null && TurnManager.Instance.IsPlayersTurn(this));

    private bool ValidHandIndex(int i)
        => i >= 0 && i < handIds.Count && i < handLvls.Count;

    private PlayerState FindPlayerByNetId(uint netId)
    {
        if (netId == 0) return null;
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity nid))
            return nid ? nid.GetComponent<PlayerState>() : null;
        return null;
    }

    [Server] public void Server_RemoveHandAt(int index) => Server_RemoveHandAt_Internal(index);

    private void RemoveHandAt(int index) => Server_RemoveHandAt_Internal(index);

    [Server]
    void Server_RemoveHandAt_Internal(int index)
    {
        if (index < 0 || index >= handIds.Count || index >= handLvls.Count) return;
        handIds.RemoveAt(index);
        handLvls.RemoveAt(index);
    }

    [Server] public void Server_AddToHand(int id, byte lvl) { handIds.Add(id); handLvls.Add(lvl); }
    [Server] public void Server_AddToSet(int id, byte lvl) { setIds.Add(id); setLvls.Add(lvl); }

    [Server]
    private bool Server_IsSetReactionInHand(int handIndex)
    {
        if (!ValidHandIndex(handIndex) || database == null) return false;
        var def = database.Get(handIds[handIndex]);
        return def != null && def.playStyle == CardDefinition.PlayStyle.SetReaction;
    }

    /// <summary>Moves a card from Hand to Set. Also spawns status for Cactus (reflect for 3 turns).</summary>
    [Server]
    private void Server_SetCard_ByIndex(int handIndex)
    {
        if (handIndex < 0 || handIndex >= handIds.Count || handIndex >= handLvls.Count) return;

        int id = handIds[handIndex];
        byte lvl = handLvls[handIndex];

        handIds.RemoveAt(handIndex);
        handLvls.RemoveAt(handIndex);

        setIds.Add(id);
        setLvls.Add(lvl);

        // If it's a Cactus, attach a 3-turn reflect status (magnitude = tier.attack)
        var d = database?.Get(id);
        if (d != null && d.effect == CardDefinition.EffectType.Cactus_ReflectUpToX_For3Turns)
        {
            int refl = Mathf.Max(1, d.GetTier(lvl).attack);
            Server_AddStatus_Cactus(refl, 3);
        }
    }

    [Server]
    public void Server_ConsumeSetAt(int index)
    {
        if (index < 0 || index >= setIds.Count || index >= setLvls.Count) return;
        setIds.RemoveAt(index);
        setLvls.RemoveAt(index);
    }

    // ───────────────────────────────────────────────── Global upgrades ─────────────────────────────────────────────────

    [Server]
    public int Server_GetEffectiveLevelForHandIndex(int handIndex)
    {
        if (!ValidHandIndex(handIndex)) return 1;
        int id = handIds[handIndex];
        if (upgradeLevels.TryGetValue(id, out byte lvl)) return lvl;
        return handLvls[handIndex];
    }

    [Server]
    public void Server_PropagateUpgradeToAllCopies(int cardId)
    {
        if (!upgradeLevels.TryGetValue(cardId, out byte lvl)) return;

        for (int i = 0; i < handIds.Count && i < handLvls.Count; i++)
            if (handIds[i] == cardId) handLvls[i] = lvl;

        for (int i = 0; i < setIds.Count && i < setLvls.Count; i++)
            if (setIds[i] == cardId) setLvls[i] = lvl;
    }

    [Server]
    private byte GetLevelForNewInstance(int cardId)
        => upgradeLevels.TryGetValue(cardId, out byte lvl) ? lvl : (byte)1;

    // ───────────────────────────────────────────────── Combat / statuses ─────────────────────────────────────────────────

    [Server]
    public void Server_ApplyDamage(PlayerState src, int amount)
    {
        if (amount <= 0) return;

        int incoming = amount;

        // Allow reactions & statuses to modify/reflect
        CardEffectResolver.TryReactOnIncomingHit(this, src, ref incoming);

        int left = incoming;

        // Armor absorbs first
        if (armor > 0 && left > 0)
        {
            int used = Mathf.Min(armor, left);
            armor -= used;
            left -= used;
        }

        // Phoenix Feather auto-revive if lethal
        if (left > 0 && (hp - left) <= 0)
        {
            int phoenixIdx = FindFirstCardInHand(CardDefinition.EffectType.PhoenixFeather_HealX_ReviveTo2IfDead);
            if (phoenixIdx >= 0)
            {
                var def = database.Get(handIds[phoenixIdx]);
                int lvl = handLvls[phoenixIdx];
                int cost = def.GetCastChipCost(lvl);
                if (Server_TrySpendChips(cost))
                {
                    // consume and revive
                    Server_RemoveHandAt_Internal(phoenixIdx);

                    // base revive to 2
                    hp = Mathf.Max(2, hp);

                    // extra heal by tier.attack if present
                    int healX = def.GetTier(lvl).attack;
                    if (healX > 0) hp = Mathf.Min(maxHP, hp + healX);

                    left = 0; // prevent death from this hit
                }
            }
        }

        if (left > 0) { hp = Mathf.Max(0, hp - left); }

        RpcHitFlash(incoming);
    }

    [Server]
    public void Server_Heal(int amount)
    {
        if (amount > 0)
        {
            hp = Mathf.Min(maxHP, hp + amount);
            RpcHealed(amount);
        }
    }

    [Server]
    public void Server_AddPoison(int mag, int turns)
    {
        statuses.Add(new StatusData { type = StatusType.Poison, magnitude = mag, turns = turns });
        RpcGotStatus("Poison", mag, turns);
    }

    [Server]
    public void Server_AddArmor(int amount)
    {
        if (amount <= 0) return;
        armor = Mathf.Min(maxArmor, armor + amount);
    }

    [Server]
    public void Server_AddStatus_Cactus(int reflectAmount, int turns)
    {
        statuses.Add(new StatusData { type = StatusType.CactusReflect, magnitude = reflectAmount, turns = turns });
        RpcGotStatus("Cactus", reflectAmount, turns);
    }

    [Server]
    public void Server_AddStatus_TurtleSkipNext()
    {
        statuses.Add(new StatusData { type = StatusType.TurtleSkip, magnitude = 1, turns = 1 });
        RpcGotStatus("Turtle", 1, 1);
    }

    [Server]
    public void Server_AddStatus_C4Fuse(int lvl, int turns, int damage)
    {
        statuses.Add(new StatusData { type = StatusType.C4Fuse, magnitude = damage, turns = turns });
        RpcGotStatus("C4", damage, turns);
    }

    /// <summary>Called by resolver before consuming set reactions; reflects part/all of incoming damage.</summary>
    [Server]
    public void Server_TryApplyCactusStatusReflect(PlayerState attacker, ref int incomingDamage)
    {
        for (int i = 0; i < statuses.Count; i++)
        {
            var s = statuses[i];
            if (s.type != StatusType.CactusReflect) continue;
            if (attacker == null || incomingDamage <= 0) break;

            // reflect up to magnitude (doesn't exceed incoming)
            int reflect = Mathf.Clamp(s.magnitude, 0, incomingDamage);
            if (reflect > 0)
            {
                attacker.Server_ApplyDamage(this, reflect);
                incomingDamage = Mathf.Max(0, incomingDamage - reflect);
            }
            return; // cactus persists; timer handled at turn start
        }
    }

    /// <summary>Process status effects at the START of this player's turn.</summary>
    [Server]
    public void Server_OnTurnStart_ProcessStatuses()
    {
        bool skippedTurn = false;

        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            var s = statuses[i];

            if (s.type == StatusType.Poison)
            {
                Server_ApplyDamage(null, s.magnitude);
                s.turns -= 1;
                if (s.turns <= 0) statuses.RemoveAt(i);
                else statuses[i] = s;
            }
            else if (s.type == StatusType.CactusReflect)
            {
                s.turns -= 1;
                if (s.turns <= 0)
                {
                    int idx = FindFirstSetWithEffect(CardDefinition.EffectType.Cactus_ReflectUpToX_For3Turns);
                    if (idx >= 0) Server_ConsumeSetAt(idx);
                    statuses.RemoveAt(i);
                }
                else statuses[i] = s;
            }
            else if (s.type == StatusType.TurtleSkip)
            {
                int idx = FindFirstSetWithEffect(CardDefinition.EffectType.Turtle_TargetSkipsNextTurn);
                if (idx >= 0) Server_ConsumeSetAt(idx);
                statuses.RemoveAt(i);
                skippedTurn = true;
            }
            else if (s.type == StatusType.C4Fuse)
            {
                s.turns -= 1;
                if (s.turns <= 0)
                {
                    // explode on owner
                    Server_ApplyDamage(null, s.magnitude);
                    int idx = FindFirstSetWithEffect(CardDefinition.EffectType.C4_ExplodeOnTargetAfter3Turns);
                    if (idx >= 0) Server_ConsumeSetAt(idx);
                    statuses.RemoveAt(i);
                }
                else statuses[i] = s;
            }
        }

        // If Turtle says skip, end the turn immediately.
        if (skippedTurn && TurnManager.Instance != null && TurnManager.Instance.IsPlayersTurn(this))
        {
            TurnManager.Instance.Server_EndTurn(this);
        }
    }

    int FindFirstSetWithEffect(CardDefinition.EffectType fx)
    {
        for (int i = 0; i < setIds.Count; i++)
        {
            var d = database?.Get(setIds[i]);
            if (d != null && d.effect == fx) return i;
        }
        return -1;
    }

    int FindFirstCardInHand(CardDefinition.EffectType effect)
    {
        for (int i = 0; i < handIds.Count; i++)
        {
            var d = database?.Get(handIds[i]);
            if (d != null && d.effect == effect) return i;
        }
        return -1;
    }

    // Mirror helpers (store/trigger last played)
    [Server]
    public void Server_RecordLastPlayed(int defId, int lvl)
    { lastPlayedCardId = defId; lastPlayedCardLevel = Mathf.Max(1, lvl); }

    [Server]
    public void Server_PlayLastStoredCardCopy(PlayerState target)
    {
        if (lastPlayedCardId < 0 || database == null) return;
        var def = database.Get(lastPlayedCardId);
        if (def == null) return;
        CardEffectResolver.PlayInstant(def, lastPlayedCardLevel, this, target);
    }

    // Client FX stubs
    [ClientRpc] private void RpcHitFlash(int dmg) { }
    [ClientRpc] private void RpcHealed(int amt) { }
    [ClientRpc] private void RpcGotStatus(string name, int mag, int t) { }
}
