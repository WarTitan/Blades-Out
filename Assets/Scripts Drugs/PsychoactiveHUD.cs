using UnityEngine;
using System.Collections.Generic;
using Mirror;

[AddComponentMenu("Gameplay/Effects/Effects HUD")]
public class PsychoactiveHUD : NetworkBehaviour
{
    [Header("UI Hookup")]
    [Tooltip("Optional. If left empty, HUD will auto-find a RectTransform named listRootObjectName in the scene.")]
    public RectTransform listRoot;      // panel under Canvas

    [Tooltip("Prefab containing PsychoactiveHUDRow (name + time). MUST be assigned.")]
    public GameObject rowPrefab;        // prefab asset (not scene object)

    [Header("Auto-find root")]
    public string listRootObjectName = "EffectsPanel";
    public bool anchorTopRight = true;
    public Vector2 topRightOffset = new Vector2(-12f, -12f);

    [Header("Update")]
    public float refreshInterval = 0.1f;

    private PsychoactiveEffectsManager mgr;

    private class Row
    {
        public string id;
        public float endTime;
        public float duration;
        public GameObject go;
        public PsychoactiveHUDRow binder;
    }

    private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
    private float nextRefresh;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Initialize();
    }

    private void OnEnable()
    {
        if (isLocalPlayer)
            Initialize();
    }

    private void OnDisable()
    {
        Unhook();
        ClearAllRows();
    }

    private void Initialize()
    {
        if (!isLocalPlayer) return;

        Debug.Log("[HUD] Initialize on " + gameObject.name);

        if (mgr == null)
            mgr = GetComponent<PsychoactiveEffectsManager>();

        if (mgr == null)
        {
            Debug.LogWarning("[PsychoactiveHUD] PsychoactiveEffectsManager not found on player.");
            return;
        }

        // Auto-find root panel if not assigned
        if (listRoot == null && !string.IsNullOrEmpty(listRootObjectName))
        {
            var obj = GameObject.Find(listRootObjectName);
            if (obj != null)
            {
                listRoot = obj.GetComponent<RectTransform>();
                Debug.Log("[PsychoactiveHUD] Found listRoot by name '" + listRootObjectName + "'.");
            }
            else
            {
                Debug.LogWarning("[PsychoactiveHUD] Could not find listRootObjectName '" +
                                 listRootObjectName + "' in scene. HUD will not show.");
            }
        }

        if (listRoot != null && anchorTopRight)
        {
            listRoot.anchorMin = new Vector2(1f, 1f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.pivot = new Vector2(1f, 1f);
            listRoot.anchoredPosition = topRightOffset;
        }

        if (rowPrefab == null)
        {
            Debug.LogWarning("[PsychoactiveHUD] rowPrefab is not assigned. HUD will not show.");
            return;
        }

        Hook();

        // existing active effects (if any)
        var snap = mgr.GetActiveEffectsSnapshot();
        for (int i = 0; i < snap.Count; i++)
        {
            CreateOrUpdateRow(snap[i]);
        }
    }

    private void Hook()
    {
        if (mgr == null) return;
        mgr.OnEffectStarted += OnEffectStarted;
        mgr.OnEffectEnded += OnEffectEnded;
    }

    private void Unhook()
    {
        if (mgr == null) return;
        mgr.OnEffectStarted -= OnEffectStarted;
        mgr.OnEffectEnded -= OnEffectEnded;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

        if (rows.Count == 0) return;

        foreach (var kv in rows)
        {
            var r = kv.Value;
            float remaining = Mathf.Max(0f, r.endTime - Time.time);
            if (r.binder != null)
                r.binder.SetTime(FormatTime(remaining));
        }
    }

    // Manager events ------------------------------------------------------

    private void OnEffectStarted(PsychoactiveEffectsManager.ActiveEffectInfo info)
    {
        CreateOrUpdateRow(info);
    }

    private void OnEffectEnded(string id)
    {
        RemoveRow(id);
    }

    // Row management ------------------------------------------------------

    private void CreateOrUpdateRow(PsychoactiveEffectsManager.ActiveEffectInfo info)
    {
        if (listRoot == null || rowPrefab == null)
        {
            Debug.LogWarning("[PsychoactiveHUD] Cannot create row: listRoot or rowPrefab is null.");
            return;
        }

        Row row;
        if (!rows.TryGetValue(info.id, out row))
        {
            var go = Instantiate(rowPrefab, listRoot, false);
            go.transform.SetAsLastSibling();

            var binder = go.GetComponent<PsychoactiveHUDRow>();
            if (binder == null)
                binder = go.AddComponent<PsychoactiveHUDRow>();

            row = new Row
            {
                id = info.id,
                endTime = info.endTime,
                duration = info.duration,
                go = go,
                binder = binder
            };

            rows[info.id] = row;
        }
        else
        {
            row.endTime = info.endTime;
            row.duration = info.duration;
        }

        if (row.binder != null)
        {
            row.binder.SetName(info.name);
            float remainingNow = Mathf.Max(0f, info.endTime - Time.time);
            row.binder.SetTime(FormatTime(remainingNow));
        }
    }

    private void RemoveRow(string id)
    {
        Row row;
        if (!rows.TryGetValue(id, out row)) return;

        if (row.go != null) Destroy(row.go);
        rows.Remove(id);
    }

    private void ClearAllRows()
    {
        foreach (var kv in rows)
        {
            if (kv.Value.go != null) Destroy(kv.Value.go);
        }
        rows.Clear();
    }

    // Helpers -------------------------------------------------------------

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int s = Mathf.FloorToInt(seconds);
        int m = s / 60;
        s = s % 60;
        return m.ToString("00") + ":" + s.ToString("00");
    }
}
