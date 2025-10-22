using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[AddComponentMenu("Currency/Currency Stacks Manual (Separate Chip Mirror + Label Offsets)")]
public class CurrencyStacksManual : MonoBehaviour
{
    [Header("Seat anchors (index == seatIndex)")]
    public Transform[] seatAnchors = new Transform[5]; // assign HandAnchor_Seat0..4 OR GoldChip0..4

    [Header("Prefabs")]
    public GameObject goldElementPrefab;
    public GameObject chipElementPrefab;

    [Header("Element scale (per type)")]
    public Vector3 goldScaleMultiplier = new Vector3(2f, 2f, 2f); // 2x gold size
    public Vector3 chipScaleMultiplier = new Vector3(1f, 1f, 1f);

    [Header("Local placement (relative to anchor)")]
    [Range(0f, 1f)] public float betweenT = 0.60f; // 0=toward player, 1=at anchor
    public float seatBackOffset = 0.60f;           // toward player (local -Z)
    public float forwardNudge = 0.00f;             // toward table (local +Z)
    public float lateralSeparation = 0.20f;        // +/- local X between stacks
    public float yOffset = 0.25f;                  // lift above plane
    public bool flipForward = false;               // true if anchor.forward faces player

    [Header("Stack visuals (shared)")]
    public int maxVisibleElements = 20;
    public bool autoStepFromPrefabBounds = true;
    public float manualVerticalStep = 0.018f;
    public float labelYOffset = 0.025f;
    public bool useIgnoreRaycastLayer = false;     // OFF so TMP is on Default layer

    [Header("Label style (applied to spawned stacks)")]
    public TMP_FontAsset labelFontAsset;
    public float labelFontSize = 3.0f;
    public bool labelAutoSize = false;
    public Color labelColor = Color.white;
    public Vector3 labelLocalScale = Vector3.one;

    [Header("Label orientation")]
    public bool labelFaceParentZ = true;           // faces anchor’s +Z
    public float labelXRotation = -90f;            // -90 => flat on stack
    public float labelYawOffsetCommon = 0f;        // add here for all labels

    [Header("Left-Right Mirror (separate)")]
    public bool labelMirrorLeftRightGold = false;  // mirror gold only if true
    public bool labelMirrorLeftRightChips = true;  // mirror chips only if true

    [Header("Label Local Offsets")]
    public Vector3 goldLabelOffset = Vector3.zero; // per-stack label local XYZ offset
    public Vector3 chipLabelOffset = Vector3.zero; // per-stack label local XYZ offset

    [Header("Randomization (natural look)")]
    public float yawJitterDegrees = 8f;
    public float xzJitter = 0.0035f;
    public float yJitter = 0.0015f;

    [Header("Behavior")]
    public float rescanInterval = 0.5f;

    private class Entry
    {
        public int seat;
        public Transform anchor;
        public PlayerState ps;
        public CurrencyStack gold;
        public CurrencyStack chips;
        public int lastGold = int.MinValue;
        public int lastChips = int.MinValue;
    }

    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>(8);
    private float scanTimer;

    void OnEnable() { scanTimer = 0f; }

    void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f) { Resync(); scanTimer = rescanInterval; }

        foreach (var kv in entries)
        {
            var e = kv.Value;
            if (e == null || e.ps == null) continue;
            if (e.seat < 0 || e.seat >= seatAnchors.Length) continue;

            var anchor = seatAnchors[e.seat];
            if (anchor == null) continue;

            if (e.anchor != anchor) { e.anchor = anchor; Reparent(e); }
            ApplyLocalPlacement(e);

            int g = SafeToInt(e.ps.gold);
            int c = SafeToInt(e.ps.chips);

            if (e.gold != null && g != e.lastGold) { e.gold.ApplyCount(g); e.lastGold = g; }
            if (e.chips != null && c != e.lastChips) { e.chips.ApplyCount(c); e.lastChips = c; }
        }
    }

    private void Resync()
    {
        var occupied = new Dictionary<int, PlayerState>(6);
#if UNITY_2023_1_OR_NEWER
        var players = UnityEngine.Object.FindObjectsByType<PlayerState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var players = UnityEngine.Object.FindObjectsOfType<PlayerState>(true);
#endif
        for (int i = 0; i < players.Length; i++)
        {
            var ps = players[i];
            if (ps == null) continue;
            var sc = ps.gameObject.scene;
            if (!sc.IsValid() || !sc.isLoaded) continue;
            if (ps.seatIndex < 0) continue;
            if (!occupied.ContainsKey(ps.seatIndex)) occupied[ps.seatIndex] = ps;
        }

        for (int seat = 0; seat < seatAnchors.Length; seat++)
        {
            var anchor = seatAnchors[seat];
            occupied.TryGetValue(seat, out var ps);
            bool isOccupied = ps != null;

            entries.TryGetValue(seat, out var e);

            if (!isOccupied || anchor == null)
            {
                if (e != null) { DestroyEntry(e); entries.Remove(seat); }
                continue;
            }

            if (e == null)
            {
                e = CreateEntry(seat, anchor, ps);
                entries[seat] = e;
            }
            else
            {
                e.ps = ps;
                if (e.anchor != anchor) { e.anchor = anchor; Reparent(e); }
            }

            ApplyLocalPlacement(e);

            int g0 = SafeToInt(ps.gold);
            int c0 = SafeToInt(ps.chips);
            if (e.gold != null) { e.gold.ApplyCount(g0); e.lastGold = g0; }
            if (e.chips != null) { e.chips.ApplyCount(c0); e.lastChips = c0; }
        }

        // cleanup out-of-range seats
        var toRemove = new List<int>();
        foreach (var kv in entries) if (kv.Key < 0 || kv.Key >= seatAnchors.Length) toRemove.Add(kv.Key);
        for (int i = 0; i < toRemove.Count; i++)
        {
            DestroyEntry(entries[toRemove[i]]);
            entries.Remove(toRemove[i]);
        }
    }

    private Entry CreateEntry(int seat, Transform anchor, PlayerState ps)
    {
        var e = new Entry { seat = seat, anchor = anchor, ps = ps };

        // GOLD
        var goldGo = new GameObject("GoldStack_Seat" + seat);
        goldGo.transform.SetParent(anchor, false);
        var gold = goldGo.AddComponent<CurrencyStack>();
        gold.elementPrefab = goldElementPrefab;
        gold.maxVisibleElements = maxVisibleElements;
        gold.autoStepFromPrefabBounds = autoStepFromPrefabBounds;
        gold.manualVerticalStep = manualVerticalStep;
        gold.scaleMultiplier = goldScaleMultiplier; // 2x
        gold.labelYOffset = labelYOffset;
        gold.putOnIgnoreRaycast = useIgnoreRaycastLayer;
        gold.yawJitterDegrees = yawJitterDegrees;
        gold.xzJitter = xzJitter;
        gold.yJitter = yJitter;
        // label style (applied at spawn so it sticks every run)
        gold.labelFontAsset = labelFontAsset;
        gold.labelFontSize = labelFontSize;
        gold.labelAutoSize = labelAutoSize;
        gold.labelColor = labelColor;
        gold.labelLocalScale = labelLocalScale;
        // orientation + mirror + offset
        gold.labelFaceParentZ = labelFaceParentZ;
        gold.labelXRotation = labelXRotation;
        gold.labelYawOffset = labelYawOffsetCommon;
        gold.labelMirrorLeftRight = labelMirrorLeftRightGold;
        gold.labelLocalOffset = goldLabelOffset;
        gold.Build();
        e.gold = gold;

        // CHIPS
        var chipGo = new GameObject("ChipStack_Seat" + seat);
        chipGo.transform.SetParent(anchor, false);
        var chips = chipGo.AddComponent<CurrencyStack>();
        chips.elementPrefab = chipElementPrefab;
        chips.maxVisibleElements = maxVisibleElements;
        chips.autoStepFromPrefabBounds = autoStepFromPrefabBounds;
        chips.manualVerticalStep = manualVerticalStep;
        chips.scaleMultiplier = chipScaleMultiplier;
        chips.labelYOffset = labelYOffset;
        chips.putOnIgnoreRaycast = useIgnoreRaycastLayer;
        chips.yawJitterDegrees = yawJitterDegrees;
        chips.xzJitter = xzJitter;
        chips.yJitter = yJitter;
        // label style
        chips.labelFontAsset = labelFontAsset;
        chips.labelFontSize = labelFontSize;
        chips.labelAutoSize = labelAutoSize;
        chips.labelColor = labelColor;
        chips.labelLocalScale = labelLocalScale;
        // orientation + mirror + offset
        chips.labelFaceParentZ = labelFaceParentZ;
        chips.labelXRotation = labelXRotation;
        chips.labelYawOffset = labelYawOffsetCommon;
        chips.labelMirrorLeftRight = labelMirrorLeftRightChips;
        chips.labelLocalOffset = chipLabelOffset;
        chips.Build();
        e.chips = chips;

        return e;
    }

    private void Reparent(Entry e)
    {
        if (e.gold != null) e.gold.transform.SetParent(e.anchor, false);
        if (e.chips != null) e.chips.transform.SetParent(e.anchor, false);
    }

    private void ApplyLocalPlacement(Entry e)
    {
        float fSign = flipForward ? -1f : 1f;
        float towardPlayerDist = seatBackOffset * (1f - Mathf.Clamp01(betweenT));

        Vector3 baseLocal =
            (-Vector3.forward * fSign * towardPlayerDist) +
            (Vector3.forward * fSign * forwardNudge) +
            (Vector3.up * yOffset);
        Vector3 side = Vector3.right * (lateralSeparation * 0.5f);

        if (e.gold != null)
        {
            e.gold.transform.localPosition = baseLocal - side;
            e.gold.transform.localRotation = Quaternion.identity;
        }
        if (e.chips != null)
        {
            e.chips.transform.localPosition = baseLocal + side;
            e.chips.transform.localRotation = Quaternion.identity;
        }
    }

    private void DestroyEntry(Entry e)
    {
        if (e == null) return;
        DestroySafe(e.gold ? e.gold.gameObject : null);
        DestroySafe(e.chips ? e.chips.gameObject : null);
    }

    private static void DestroySafe(GameObject go)
    {
        if (go == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(go);
        else UnityEngine.Object.Destroy(go);
#else
        UnityEngine.Object.Destroy(go);
#endif
    }

    private static int SafeToInt(object value)
    {
        try { return Convert.ToInt32(value); } catch { return 0; }
    }
}
