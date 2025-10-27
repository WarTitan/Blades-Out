using UnityEngine;
using Mirror;

[AddComponentMenu("Turn/Turn Timer (Net)")]
public class TurnTimerNet : NetworkBehaviour
{
    public static TurnTimerNet Instance;   // singleton for UI

    [Header("Config")]
    [SerializeField] private int turnDurationSeconds = 20;

    [SyncVar] public int syncedTurnDuration = 20;    // clients read this for UI
    [SyncVar] public double turnEndTime = 0;         // server writes, clients read
    [SyncVar] public uint currentTurnNetId = 0;      // who owns the current turn (for UI name lookup)

    void Awake() { Instance = this; }
    public override void OnStartClient() { base.OnStartClient(); Instance = this; }
    public override void OnStartServer()
    {
        base.OnStartServer();
        Instance = this;
        syncedTurnDuration = Mathf.Max(5, turnDurationSeconds);
        // end time is set when we first detect an owner
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    // -------- Helper: Find all with 2023+ compatibility --------
    static T[] FindAll<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }

    [ServerCallback]
    void Update()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        // Find current turn owner on server
        PlayerState owner = null;
        var players = FindAll<PlayerState>();
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p != null && tm.IsPlayersTurn(p))
            {
                owner = p;
                break;
            }
        }
        if (owner == null) return; // no active turn yet

        // If the owner CHANGED -> start a NEW countdown
        if (currentTurnNetId != owner.netId)
        {
            currentTurnNetId = owner.netId;
            StartNewCountdown();
            return;
        }

        // If countdown EXPIRED -> end the turn (do NOT reset here; the next owner will reset it)
        if (NetworkTime.time >= turnEndTime)
        {
            tm.Server_EndTurn(owner);
            // Next Update() will see a new owner and StartNewCountdown()
        }
    }

    [Server]
    void StartNewCountdown()
    {
        syncedTurnDuration = Mathf.Max(5, turnDurationSeconds);
        turnEndTime = NetworkTime.time + syncedTurnDuration;
        // Debug.Log($"[TurnTimer] New owner {currentTurnNetId}, ends at {turnEndTime:0.00}");
    }

    // Public helper for UI (client-side)
    public float GetSecondsRemaining()
    {
        double remain = turnEndTime - NetworkTime.time;
        if (remain < 0) remain = 0;
        return (float)remain;
    }
}
