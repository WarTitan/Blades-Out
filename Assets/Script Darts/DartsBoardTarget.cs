// FILE: DartsBoardTarget.cs
// Shows "<SteamName> - <score>" above the dartboard.
// - Accepts any TMP label via TMP_Text (TextMeshProUGUI or 3D TextMeshPro).
// - You can drag either PlayerNameNet or NetworkIdentity of the owner.
// - Auto-binds by seat (1..5), checking many seat field names:
//     seatIndex1Based, seIndex1Based, seatIndex, seIndex, seat, seat1Based
// - Optional fallback to nearest player if seat binding fails.
// - Retries once per second until it finds a non-empty Steam name.
// - Toggle debugLogs to see what it is doing.
//
// ASCII-only.

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.UI;

[AddComponentMenu("Minigames/Darts Board Target")]
public class DartsBoardTarget : MonoBehaviour
{
    [Header("Board Id")]
    [Tooltip("Seat/board number (1..5).")]
    public int boardIndex1Based = 1;

    [Header("Label References")]
    [Tooltip("Any TextMeshPro label (TMP_Text covers TextMeshProUGUI or 3D TextMeshPro).")]
    public TMP_Text label;
    [Tooltip("Optional fallback if you still use legacy UI.Text.")]
    public Text uiLabelLegacy;

    [Header("Owner Binding (explicit)")]
    [Tooltip("If assigned, we'll use this PlayerNameNet directly.")]
    public PlayerNameNet ownerNameNet;         // drag this if easy
    [Tooltip("Or assign the NetworkIdentity that has PlayerNameNet.")]
    public NetworkIdentity ownerIdentity;      // or drag this

    [Header("Owner Binding (automatic)")]
    [Tooltip("Try to find the player with the same seat index and bind to their PlayerNameNet.")]
    public bool autoBindOwnerBySeat = true;

    [Tooltip("If seat binding fails, bind to the nearest PlayerNameNet to this board.")]
    public bool bindByNearestIfSeatFails = true;

    [Tooltip("Reference point for nearest-player fallback. If null, uses this.transform.")]
    public Transform boardAnchorForProximity;

    [Header("Retry and Display")]
    [Tooltip("Keep trying to bind owner and refresh name once per second until non-empty name is found.")]
    public bool retryUntilBound = true;

    [Tooltip("Shown until a PlayerNameNet with a non-empty displayName is found.")]
    public string fallbackName = "Player";

    [Header("Debug")]
    public bool debugLogs = false;

    private int _score;
    private string _lastShownName = null;
    private Coroutine _bindLoop;

    private static readonly string[] SeatFieldNames = new string[]
    {
        "seatIndex1Based", "seIndex1Based", "seatIndex", "seIndex", "seat", "seat1Based"
    };

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null && uiLabelLegacy == null) uiLabelLegacy = GetComponentInChildren<Text>(true);
    }

    void OnEnable()
    {
        // If you explicitly set ownerNameNet or ownerIdentity, bind immediately.
        if (ownerNameNet != null) BindOwner(ownerNameNet);
        else if (ownerIdentity != null) BindOwner(ownerIdentity);

        if (_bindLoop == null && retryUntilBound)
            _bindLoop = StartCoroutine(CoBindOwnerLoop());
    }

    void OnDisable()
    {
        if (_bindLoop != null)
        {
            StopCoroutine(_bindLoop);
            _bindLoop = null;
        }
    }

    void Start()
    {
        // One immediate attempt via seat if not already bound explicitly
        if (ownerNameNet == null && autoBindOwnerBySeat)
            TryAutoBindOwnerBySeat();

        RefreshLabel();
    }

    // Called by the game manager when the score changes
    public void SetScore(int value)
    {
        _score = Mathf.Max(0, value);
        RefreshLabel();
    }

    public void BindOwner(NetworkIdentity ni)
    {
        if (ni == null) return;
        ownerIdentity = ni;
        var found = ni.GetComponent<PlayerNameNet>();
        if (found == null) found = ni.GetComponentInChildren<PlayerNameNet>(true);
        if (found == null) found = ni.GetComponentInParent<PlayerNameNet>(true);
        if (found != null)
        {
            ownerNameNet = found;
            if (debugLogs) Debug.Log("[DartsBoardTarget] Bound owner via NetworkIdentity: " + ownerNameNet.displayName);
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[DartsBoardTarget] NetworkIdentity has no PlayerNameNet.");
        }
        RefreshLabel();
    }

    public void BindOwner(PlayerNameNet pnn)
    {
        ownerNameNet = pnn;
        if (ownerIdentity == null && pnn != null)
            ownerIdentity = pnn.GetComponent<NetworkIdentity>();
        if (debugLogs) Debug.Log("[DartsBoardTarget] Bound owner via PlayerNameNet: " + (pnn != null ? pnn.displayName : "<null>"));
        RefreshLabel();
    }

    private IEnumerator CoBindOwnerLoop()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            if (ownerNameNet == null)
            {
                // Try seat first
                if (autoBindOwnerBySeat) TryAutoBindOwnerBySeat();

                // If still null, try nearest fallback
                if (ownerNameNet == null && bindByNearestIfSeatFails)
                    TryBindNearestPlayer();
            }

            // If we have an owner but name empty, keep refreshing until it appears
            RefreshLabel();

            yield return wait;
        }
    }

    private void RefreshLabel()
    {
        string name = ResolveDisplayName();
        string text = name + " - " + _score.ToString();

        if (label != null) label.text = text;
        else if (uiLabelLegacy != null) uiLabelLegacy.text = text;

        if (debugLogs && _lastShownName != name)
            Debug.Log("[DartsBoardTarget] Label now: " + text);

        _lastShownName = name;
    }

    private string ResolveDisplayName()
    {
        if (ownerNameNet != null && !string.IsNullOrEmpty(ownerNameNet.displayName))
            return ownerNameNet.displayName;
        return fallbackName;
    }

    private void TryAutoBindOwnerBySeat()
    {
        int want = Mathf.Clamp(boardIndex1Based, 1, 5);
        var all = GameObject.FindObjectsOfType<PlayerNameNet>();
        if (all == null || all.Length == 0)
        {
            if (debugLogs) Debug.Log("[DartsBoardTarget] No PlayerNameNet found in scene yet.");
            return;
        }

        for (int i = 0; i < all.Length; i++)
        {
            var pnn = all[i];
            if (pnn == null) continue;

            int seat = ExtractSeatIndexFromHierarchy(pnn.transform);
            if (debugLogs) Debug.Log("[DartsBoardTarget] Found candidate " + pnn.name + " seat=" + seat);

            if (seat == want)
            {
                BindOwner(pnn);
                return;
            }
        }

        if (debugLogs) Debug.LogWarning("[DartsBoardTarget] No player with seat=" + want + " found for board " + boardIndex1Based);
    }

    private void TryBindNearestPlayer()
    {
        var all = GameObject.FindObjectsOfType<PlayerNameNet>();
        if (all == null || all.Length == 0) return;

        Vector3 origin = (boardAnchorForProximity != null) ? boardAnchorForProximity.position : transform.position;
        float best = float.MaxValue;
        PlayerNameNet bestP = null;

        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (p == null) continue;
            float d = (p.transform.position - origin).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestP = p;
            }
        }

        if (bestP != null)
        {
            if (debugLogs) Debug.Log("[DartsBoardTarget] Fallback bound to nearest: " + bestP.displayName);
            BindOwner(bestP);
        }
    }

    private int ExtractSeatIndexFromHierarchy(Transform t)
    {
        // Check self
        int v;
        if (TryExtractSeatFromObject(t.gameObject, out v)) return v;

        // Parents
        var parents = t.GetComponentsInParent<Component>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            var c = parents[i];
            if (c == null) continue;
            if (TryExtractSeatFromComponent(c, out v)) return v;
        }

        // Children
        var children = t.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var c = children[i];
            if (c == null) continue;
            if (TryExtractSeatFromComponent(c, out v)) return v;
        }

        return -1;
    }

    private bool TryExtractSeatFromObject(GameObject go, out int value)
    {
        value = -1;

        // Known likely components
        var trays = go.GetComponent<PlayerItemTrays>();
        if (trays != null && TryExtractSeatFromComponent(trays, out value)) return true;

        var state = go.GetComponent<PlayerState>();
        if (state != null && TryExtractSeatFromComponent(state, out value)) return true;

        // Any component on this object
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            if (TryExtractSeatFromComponent(c, out value)) return true;
        }
        return false;
    }

    private bool TryExtractSeatFromComponent(object obj, out int value)
    {
        value = -1;
        var t = obj.GetType();

        for (int i = 0; i < SeatFieldNames.Length; i++)
        {
            string n = SeatFieldNames[i];

            var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int))
            {
                int v = (int)f.GetValue(obj);
                if (v > 0) { value = v; return true; }
            }

            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead)
            {
                int v = (int)p.GetValue(obj, null);
                if (v > 0) { value = v; return true; }
            }
        }

        return false;
    }

    // Inspector helpers
    [ContextMenu("Try bind owner now")]
    private void ContextBindNow()
    {
        if (ownerNameNet == null && autoBindOwnerBySeat) TryAutoBindOwnerBySeat();
        if (ownerNameNet == null && bindByNearestIfSeatFails) TryBindNearestPlayer();
        RefreshLabel();
        Debug.Log("[DartsBoardTarget] Bind attempt on " + name + " -> " +
                  (ownerNameNet != null ? ownerNameNet.displayName : "<none>"));
    }

    [ContextMenu("Auto-wire TMP label on this object")]
    private void ContextAutoWireTmp()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null) Debug.LogWarning("[DartsBoardTarget] No TMP_Text found under " + name);
        else Debug.Log("[DartsBoardTarget] Wired TMP_Text: " + label.name);
    }
}
