// FILE: DartsBoardTarget.cs
// Shows "<SteamName> - <score>" when a player is bound to this board.
// If nobody is bound (or name not ready), the label is BLANK.
//
// - Accepts any TextMeshPro label via TMP_Text (TextMeshProUGUI or 3D TextMeshPro).
// - Auto-binds owner by seat index (1..5). No nearest fallback (keeps blanks on empty boards).
// - You can also assign owner explicitly: PlayerNameNet or NetworkIdentity.
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

    [Header("Label")]
    [Tooltip("TMP label (TextMeshProUGUI or 3D TextMeshPro).")]
    public TMP_Text label;
    [Tooltip("Optional legacy UI.Text fallback (TMP preferred).")]
    public Text uiLabelLegacy;

    [Header("Owner Binding")]
    [Tooltip("Try to find the player with the same seat index and bind to their PlayerNameNet.")]
    public bool autoBindOwnerBySeat = true;

    [Tooltip("Optional explicit owner (has PlayerNameNet).")]
    public PlayerNameNet ownerNameNet;
    [Tooltip("Or assign NetworkIdentity that has PlayerNameNet.")]
    public NetworkIdentity ownerIdentity;

    [Header("Retry")]
    [Tooltip("Keep trying to bind/update name once per second until resolved.")]
    public bool retryUntilBound = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private int _score;
    private string _lastShown = null;
    private Coroutine _loop;

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
        if (ownerNameNet != null) BindOwner(ownerNameNet);
        else if (ownerIdentity != null) BindOwner(ownerIdentity);

        if (_loop == null && retryUntilBound)
            _loop = StartCoroutine(CoBindLoop());
    }

    void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    void Start()
    {
        if (ownerNameNet == null && autoBindOwnerBySeat)
            TryAutoBindOwnerBySeat();

        RefreshLabel();
    }

    public void SetScore(int value)
    {
        _score = Mathf.Max(0, value);
        RefreshLabel();
    }

    public void BindOwner(NetworkIdentity ni)
    {
        if (ni == null) { ownerIdentity = null; ownerNameNet = null; RefreshLabel(); return; }
        ownerIdentity = ni;
        var p = ni.GetComponent<PlayerNameNet>();
        if (p == null) p = ni.GetComponentInChildren<PlayerNameNet>(true);
        if (p == null) p = ni.GetComponentInParent<PlayerNameNet>(true);
        ownerNameNet = p;
        if (debugLogs) Debug.Log("[DartsBoardTarget] Bound via NetworkIdentity -> " + (p != null ? p.displayName : "<none>"));
        RefreshLabel();
    }

    public void BindOwner(PlayerNameNet pnn)
    {
        ownerNameNet = pnn;
        if (ownerIdentity == null && pnn != null) ownerIdentity = pnn.GetComponent<NetworkIdentity>();
        if (debugLogs) Debug.Log("[DartsBoardTarget] Bound via PlayerNameNet -> " + (pnn != null ? pnn.displayName : "<none>"));
        RefreshLabel();
    }

    public void UnbindOwner()
    {
        ownerNameNet = null;
        ownerIdentity = null;
        RefreshLabel();
    }

    private IEnumerator CoBindLoop()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            if (ownerNameNet == null && autoBindOwnerBySeat)
                TryAutoBindOwnerBySeat();

            RefreshLabel();
            yield return wait;
        }
    }

    private void RefreshLabel()
    {
        string name = ResolveNameOrBlank();
        string text = (string.IsNullOrEmpty(name)) ? "" : (name + " - " + _score.ToString());

        if (label != null) label.text = text;
        else if (uiLabelLegacy != null) uiLabelLegacy.text = text;

        if (debugLogs && _lastShown != text)
            Debug.Log("[DartsBoardTarget] Label -> \"" + text + "\" (board " + boardIndex1Based + ")");
        _lastShown = text;
    }

    private string ResolveNameOrBlank()
    {
        if (ownerNameNet != null && !string.IsNullOrEmpty(ownerNameNet.displayName))
            return ownerNameNet.displayName;
        return ""; // BLANK when no active player
    }

    private void TryAutoBindOwnerBySeat()
    {
        int want = Mathf.Clamp(boardIndex1Based, 1, 5);
        var all = GameObject.FindObjectsOfType<PlayerNameNet>();
        if (all == null || all.Length == 0) return;

        for (int i = 0; i < all.Length; i++)
        {
            var pnn = all[i];
            if (pnn == null) continue;
            int seat = ExtractSeatIndexFromHierarchy(pnn.transform);
            if (seat == want)
            {
                BindOwner(pnn);
                return;
            }
        }
    }

    private int ExtractSeatIndexFromHierarchy(Transform t)
    {
        int v;
        if (TryExtractSeatFromObject(t.gameObject, out v)) return v;

        var parents = t.GetComponentsInParent<Component>(true);
        for (int i = 0; i < parents.Length; i++)
            if (TryExtractSeatFromComponent(parents[i], out v)) return v;

        var children = t.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < children.Length; i++)
            if (TryExtractSeatFromComponent(children[i], out v)) return v;

        return -1;
    }

    private bool TryExtractSeatFromObject(GameObject go, out int value)
    {
        value = -1;
        var trays = go.GetComponent<PlayerItemTrays>();
        if (trays != null && TryExtractSeatFromComponent(trays, out value)) return true;

        var state = go.GetComponent<PlayerState>();
        if (state != null && TryExtractSeatFromComponent(state, out value)) return true;

        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
            if (TryExtractSeatFromComponent(comps[i], out value)) return true;
        return false;
    }

    private bool TryExtractSeatFromComponent(object obj, out int value)
    {
        value = -1;
        if (obj == null) return false;
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
}
