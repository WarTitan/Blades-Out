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
    public int MaxHP => maxHP;
    public int MaxArmor => maxArmor;

    [Header("Life state")]
    [SyncVar(hook = nameof(OnDeadChanged))] public bool isDead = false;
    void OnDeadChanged(bool oldV, bool newV)
    {
        // Hook for UI if you want to gray out hand, etc.
    }

    [Header("Chips (Hearthstone-style growth at END of your turn)")]
    [SyncVar(hook = nameof(OnChipsChanged))] public int chips = 0;
    [SyncVar(hook = nameof(OnMaxChipsChanged))] public int maxChips = 0;

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

    // Upgrades per cardId
    public readonly SyncDictionary<int, byte> upgradeLevels = new SyncDictionary<int, byte>();

    // Action safety
    private bool actionLock = false;
    private float lastActionTime = -999f;
    [SerializeField] private float minActionInterval = 0.10f;

    // Statuses
    private readonly List<StatusData> statuses = new List<StatusData>();
    private struct StatusData { public StatusType type; public int magnitude; public int turns; }
    private enum StatusType { Poison, CactusReflect, TurtleSkip, C4Fuse }

    // Mirror "copy last played"
    private int lastPlayedCardId = -1;
    private int lastPlayedCardLevel = 1;

    public enum TurnStartDirective
    {
        Normal,
        SkipNoRewards,
        AutoEndWithRewardsAndPass
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterPlayer(this);
            turnManager = TurnManager.Instance;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (database == null)
            database = CardDatabase.FindOrLoadActive();
    }

    // ---------------- Game init / draw ----------------

    [Server]
    public void Server_Init(CardDatabase db, int deckSize, int startGold, int startHand)
    {
        database = db;

        gold = 5;
        maxChips = 1;
        chips = 1;

        hp = maxHP;
        armor = maxArmor;

        isDead = false;

        handIds.Clear(); handLvls.Clear();
        setIds.Clear(); setLvls.Clear();
        // upgradeLevels.Clear();

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

    // ---------------- Chips / gold ----------------

    [Server] public void Server_RefillChipsToMax() { chips = maxChips; }

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

    // ---------------- Authority helper ----------------

    bool Server_VerifySenderIsOwner()
    {
        var senderIdentity = connectionToClient != null ? connectionToClient.identity : null;
        var senderPS = senderIdentity ? senderIdentity.GetComponent<PlayerState>() : null;
        return senderPS == this;
    }

    // ---------------- Commands ----------------

    [Command(requiresAuthority = false)]
    public void CmdRequestStartGame()
    {
        if (!Server_VerifySenderIsOwner()) return;
        TurnManager.Instance?.Server_AttemptStartGame();
    }

    [Command(requiresAuthority = false)]
    public void CmdEndTurn()
    {
        if (!Server_VerifySenderIsOwner()) return;
        if (isDead) { TargetActionDenied(connectionToClient, "[Turn] You are dead."); return; }
        TurnManager.Instance?.Server_EndTurn(this);
    }

    [Command(requiresAuthority = false)]
    public void CmdUpgradeCard(int handIndex)
    {
        if (!Server_VerifySenderIsOwner()) return;
        if (isDead) { TargetActionDenied(connectionToClient, "[Upgrade] You are dead."); return; }
        TurnManager.Instance?.Server_UpgradeCard(this, handIndex);
    }

    // Cast instant/target during YOUR turn. Dead players cannot act. Phoenix self-cast while alive is still allowed at <max HP.
    [Command(requiresAuthority = false)]
    public void CmdPlayInstant(int handIndex, uint targetNetId)
    {
        if (!Server_VerifySenderIsOwner()) return;

        if (isDead)
        {
            TargetActionDenied(connectionToClient, "[Cast] You are dead.");
            return;
        }

        if (!IsYourTurn())
        {
            TargetActionDenied(connectionToClient, "[Cast] Only during YOUR turn.");
            return;
        }
        if (!ValidHandIndex(handIndex)) return;
        if (Time.time - lastActionTime < minActionInterval) return;
        if (actionLock) return;

        lastActionTime = Time.time;

        var def = database != null ? database.Get(handIds[handIndex]) : null;
        if (def == null)
        {
            TargetActionDenied(connectionToClient, "[Cast] No definition for this card.");
            return;
        }

        // Block self-heals at full HP (allow when dead via start-of-turn logic only)
        if (def.effect == CardDefinition.EffectType.LovePotion_HealXSelf ||
            def.effect == CardDefinition.EffectType.PhoenixFeather_HealX_ReviveTo2IfDead)
        {
            if (hp >= maxHP && hp > 0)
            {
                TargetActionDenied(connectionToClient, "[Cast] You are already at full HP.");
                return;
            }
        }

        bool needsTarget = false;
        switch (def.effect)
        {
            case CardDefinition.EffectType.Knife_DealX:
            case CardDefinition.EffectType.KnifePotion_DealX_HealXSelf:
            case CardDefinition.EffectType.C4_ExplodeOnTargetAfter3Turns:
            case CardDefinition.EffectType.GoblinHands_MoveOneSetItemToCaster:
            case CardDefinition.EffectType.Pickpocket_StealOneRandomHandCard:
            case CardDefinition.EffectType.Turtle_TargetSkipsNextTurn:
                needsTarget = true; break;
        }

        if (needsTarget && targetNetId == 0)
        {
            TargetActionDenied(connectionToClient, "[Cast] This card needs a target.");
            return;
        }

        if (def.playStyle == CardDefinition.PlayStyle.SetReaction)
        {
            TargetActionDenied(connectionToClient, "[Cast] This card must be set, not cast.");
            return;
        }

        int lvl = handLvls[handIndex];
        int cost = def.GetCastChipCost(lvl);
        if (!Server_TrySpendChips(cost))
        {
            TargetActionDenied(connectionToClient, "[Cast] Not enough chips (" + cost + " needed).");
            return;
        }

        var target = FindPlayerByNetId(targetNetId);

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

    // Set reaction (your turn only). Dead players cannot act.
    [Server] private bool Server_CanSetAnother() => setIds.Count == 0;

    [Command(requiresAuthority = false)]
    public void CmdSetCard(int handIndex)
    {
        if (!Server_VerifySenderIsOwner()) return;
        if (isDead) { TargetActionDenied(connectionToClient, "[Set] You are dead."); return; }

        if (!IsYourTurn()) { TargetActionDenied(connectionToClient, "[Set] Only during YOUR turn."); return; }
        if (!Server_CanSetAnother()) { TargetActionDenied(connectionToClient, "[Set] You already have a set card."); return; }

        if (Time.time - lastActionTime < minActionInterval) return;
        lastActionTime = Time.time;
        if (actionLock) return;

        if (!Server_IsSetReactionInHand(handIndex)) { TargetActionDenied(connectionToClient, "[Set] This card can't be set."); return; }

        int id = handIds[handIndex];
        int lvl = handLvls[handIndex];
        var def = database != null ? database.Get(id) : null;
        int cost = (def != null) ? def.GetCastChipCost(lvl) : 0;
        if (!Server_TrySpendChips(cost))
        {
            TargetActionDenied(connectionToClient, "[Set] Not enough chips (" + cost + " needed).");
            return;
        }

        actionLock = true;
        try { Server_SetCard_ByIndex(handIndex); }
        finally { actionLock = false; }
    }

    [Server]
    private void Server_SetCard_ByIndex(int handIndex)
    {
        if (!Server_CanSetAnother()) return;
        if (handIndex < 0 || handIndex >= handIds.Count || handIndex >= handLvls.Count) return;

        int id = handIds[handIndex];
        byte lvl = handLvls[handIndex];

        handIds.RemoveAt(handIndex);
        handLvls.RemoveAt(handIndex);

        setIds.Add(id);
        setLvls.Add(lvl);

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

    // ---------------- Helpers ----------------

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

    // ---------------- Global upgrades ----------------

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

    // ---------------- Combat / statuses ----------------

    [Server]
    public void Server_ApplyDamage(PlayerState src, int amount)
    {
        if (amount <= 0) return;

        int incoming = amount;

        // Allow set reactions & statuses to modify/reflect
        CardEffectResolver.TryReactOnIncomingHit(this, src, ref incoming);

        int left = incoming;

        // Armor absorbs first
        if (armor > 0 && left > 0)
        {
            int used = Mathf.Min(armor, left);
            armor -= used;
            left -= used;
        }

        // DO NOT auto-revive here. Death is finalized now; phoenix is handled at start of your turn.
        if (left > 0) { hp = Mathf.Max(0, hp - left); }

        // mark death state
        bool nowDead = (hp <= 0);
        isDead = nowDead;

        // check for endgame when someone dies
        if (nowDead && TurnManager.Instance != null)
            TurnManager.Instance.Server_CheckForEndGame();

        RpcHitFlash(incoming);
    }

    [Server]
    public void Server_Heal(int amount)
    {
        if (amount > 0)
        {
            hp = Mathf.Min(maxHP, hp + amount);
            if (hp > 0) isDead = false;
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

    [Server]
    public void Server_TryApplyCactusStatusReflect(PlayerState attacker, ref int incomingDamage)
    {
        for (int i = 0; i < statuses.Count; i++)
        {
            var s = statuses[i];
            if (s.type != StatusType.CactusReflect) continue;
            if (attacker == null || incomingDamage <= 0) break;

            int reflect = Mathf.Clamp(s.magnitude, 0, incomingDamage);
            if (reflect > 0)
            {
                attacker.Server_ApplyDamage(this, reflect);
                incomingDamage = Mathf.Max(0, incomingDamage - reflect);
            }
            return; // persists; timer handled at turn start
        }
    }

    // START of your turn: handle death -> auto phoenix, turtle, etc.
    // Returns directive for TurnManager to act on.
    [Server]
    public TurnStartDirective Server_OnTurnStart_ProcessStatuses()
    {
        // 1) If dead: try auto phoenix from hand. If succeed -> alive; else skip without rewards.
        if (hp <= 0 || isDead)
        {
            int phoenixIdx = FindFirstCardInHand(CardDefinition.EffectType.PhoenixFeather_HealX_ReviveTo2IfDead);
            if (phoenixIdx >= 0 && database != null)
            {
                var def = database.Get(handIds[phoenixIdx]);
                int lvl = handLvls[phoenixIdx];
                int cost = def != null ? def.GetCastChipCost(lvl) : 0;

                if (Server_TrySpendChips(cost))
                {
                    // consume and revive
                    Server_RemoveHandAt_Internal(phoenixIdx);

                    // revive to 2 then extra heal by tier.attack
                    hp = Mathf.Max(2, hp);
                    int healX = def != null ? def.GetTier(lvl).attack : 0;
                    if (healX > 0) hp = Mathf.Min(maxHP, hp + healX);

                    isDead = false;
                    // continue to status processing below (can still turtle-auto-end)
                }
                else
                {
                    // cannot pay -> stay dead, skip
                    return TurnStartDirective.SkipNoRewards;
                }
            }
            else
            {
                // no phoenix -> stay dead, skip
                return TurnStartDirective.SkipNoRewards;
            }
        }

        // 2) Process statuses normally (poison, cactus tick, C4, turtle)
        TurnStartDirective directive = TurnStartDirective.Normal;

        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            var s = statuses[i];

            if (s.type == StatusType.Poison)
            {
                Server_ApplyDamage(null, s.magnitude);
                s.turns -= 1;
                if (s.turns <= 0) statuses.RemoveAt(i);
                else statuses[i] = s;

                if (isDead) return TurnStartDirective.SkipNoRewards; // died from poison
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
                // auto-end with rewards and pass turtle away
                statuses.RemoveAt(i);
                directive = TurnStartDirective.AutoEndWithRewardsAndPass;
                // don't break; continue to clean others
            }
            else if (s.type == StatusType.C4Fuse)
            {
                s.turns -= 1;
                if (s.turns <= 0)
                {
                    Server_ApplyDamage(null, s.magnitude);
                    int idx = FindFirstSetWithEffect(CardDefinition.EffectType.C4_ExplodeOnTargetAfter3Turns);
                    if (idx >= 0) Server_ConsumeSetAt(idx);
                    statuses.RemoveAt(i);

                    if (isDead) return TurnStartDirective.SkipNoRewards;
                }
                else statuses[i] = s;
            }
        }

        return directive;
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

    // Mirror helpers
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
