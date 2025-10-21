using UnityEngine;
using Mirror;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkIdentity))]
[AddComponentMenu("Net/Lobby Idle Animator")]
public class LobbyIdleAnimator : NetworkBehaviour
{
    [Header("Animator & Clips")]
    // Leave null to auto-find the deepest active child Animator (the real model).
    public Animator animator;
    // Idle state name or path (e.g. "Idle" or "Locomotion.Idle").
    public string baseIdleStateName = "Idle";
    // The clip assigned to the Idle state in the model's controller.
    public AnimationClip baseIdleClip;
    // Lobby variants to randomize between (one will be chosen on the server).
    public AnimationClip[] lobbyIdleClips;

    [Header("Playback")]
    public int baseLayerIndex = 0;
    public float crossfade = 0.20f;
    public Vector2 speedRange = new Vector2(0.95f, 1.05f);

    [Header("Behaviour")]
    // Keep forcing the Idle state while lobby is active (prevents other states taking over).
    public bool forceIdleWhileLobby = true;
    // When a model is swapped, jump EXACTLY to the saved phase (no blend).
    public bool onSwitchUseCrossfade = false;

    [Header("Anti-freeze settings")]
    // Use UnscaledTime so idles run even if Time.timeScale==0 in lobby.
    public bool useUnscaledTimeInLobby = true;
    // Ensure animator keeps updating even if not visible by a camera.
    public bool forceAlwaysAnimateCullingInLobby = true;
    // Re-assert speed each frame in lobby (avoids other scripts setting 0).
    public bool enforcePositiveSpeedInLobby = true;
    public float minLobbySpeed = 0.1f;
    // Watchdog: if phase does not change for this many seconds, nudge it forward a tiny amount.
    public float watchdogInterval = 1.0f;
    public float watchdogNudge = 0.02f;

    [Header("Auto-resolve")]
    public bool preferDeepestChildAnimator = true;   // pick the deepest valid Animator under this object

    [Header("Debug")]
    public bool verbose = false;

    // Server-chosen so all clients see the same idle choice
    [SyncVar] private int idleVariant = -1;
    [SyncVar] private float idleSpeed = 1f;
    [SyncVar] private float idleTimeOffset01 = 0f;

    private RuntimeAnimatorController originalController;
    private AnimatorOverrideController aoc;
    private bool applied;

    // Phase keeping (0..1) across swaps
    private float savedPhase01 = 0f;
    private float rescanTimer = 0f;

    // Watchdog
    private float lastWatchdogCheckUnscaled = 0f;
    private float lastPhaseForWatchdog = -1f;

    void OnEnable()
    {
        LobbyStage.OnLobbyStateChanged += OnLobbyChanged;
    }

    void OnDisable()
    {
        LobbyStage.OnLobbyStateChanged -= OnLobbyChanged;
        Revert();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (lobbyIdleClips != null && lobbyIdleClips.Length > 0)
        {
            idleVariant = Random.Range(0, lobbyIdleClips.Length);
            idleSpeed = Random.Range(speedRange.x, speedRange.y);
            idleTimeOffset01 = Random.value;
        }
        else
        {
            idleVariant = -1;
            idleSpeed = 1f;
            idleTimeOffset01 = 0f;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveAnimator(true);
        if (verbose)
            Debug.Log("[LobbyIdle] StartClient -> animator=" + (animator ? GetPath(animator.transform) : "null") + ", lobby=" + (LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : true));
        UpdateMode(IsLobby());
    }

    void Update()
    {
        if (!IsLobby()) return;

        // Keep culling/update mode/speed safe in lobby
        if (animator != null)
        {
            if (forceAlwaysAnimateCullingInLobby && animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var desiredUpdate = useUnscaledTimeInLobby ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
            if (animator.updateMode != desiredUpdate)
                animator.updateMode = desiredUpdate;

            if (enforcePositiveSpeedInLobby)
            {
                float target = Mathf.Max(minLobbySpeed, idleSpeed);
                if (Mathf.Abs(animator.speed - target) > 0.0001f)
                    animator.speed = target;
            }
        }

        // Track current Idle phase continuously
        if (animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(baseLayerIndex);
            if (StateMatches(st))
            {
                float t = st.normalizedTime;
                savedPhase01 = t - Mathf.Floor(t); // wrap to 0..1
            }
        }

        // Periodically re-resolve: handles character swaps during lobby
        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = 0.20f;
            Animator before = animator;
            ResolveAnimator();
            Animator after = animator;

            if (after != before && animator != null)
            {
                if (verbose) Debug.Log("[LobbyIdle] Animator changed -> " + GetPath(animator.transform) + " (phase=" + savedPhase01.ToString("0.00") + ")");
                applied = false;      // override must be applied to the new animator
                Apply();
                // Jump to the exact saved phase (no blend to avoid time reset)
                SetIdleAtPhase(savedPhase01, onSwitchUseCrossfade);
            }
        }

        // Keep in Idle while in lobby if requested
        if (forceIdleWhileLobby && animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(baseLayerIndex);
            if (!StateMatches(st))
                SetIdleAtPhase(savedPhase01, false);
        }

        // Watchdog: if phase hasn't advanced in a while, nudge it forward
        float nowUnscaled = Time.unscaledTime;
        if (nowUnscaled - lastWatchdogCheckUnscaled >= watchdogInterval)
        {
            lastWatchdogCheckUnscaled = nowUnscaled;
            if (Mathf.Abs(savedPhase01 - lastPhaseForWatchdog) < 0.0005f)
            {
                // phase stuck -> nudge a little forward
                SetIdleAtPhase(savedPhase01 + watchdogNudge, false);
                if (verbose) Debug.Log("[LobbyIdle] Watchdog nudged idle to phase " + (savedPhase01 + watchdogNudge).ToString("0.00"));
            }
            lastPhaseForWatchdog = savedPhase01;
        }
    }

    void OnLobbyChanged(bool lobbyActive)
    {
        if (verbose) Debug.Log("[LobbyIdle] LobbyActive -> " + lobbyActive);
        UpdateMode(lobbyActive);
    }

    bool IsLobby()
    {
        return LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : true;
    }

    void UpdateMode(bool lobbyActive)
    {
        if (!animator) ResolveAnimator(true);
        if (!animator) return;

        if (lobbyActive)
        {
            Apply();
            // On first apply, start at a randomized offset (or the saved one if already tracking)
            float startPhase = (savedPhase01 > 0f) ? savedPhase01 : idleTimeOffset01;
            SetIdleAtPhase(startPhase, false);
        }
        else
        {
            Revert();
        }
    }

    void ResolveAnimator(bool forceLog = false)
    {
        // Keep explicit assignment if valid
        if (animator && animator.runtimeAnimatorController != null && animator.gameObject.activeInHierarchy)
        {
            return;
        }

        Animator chosen = null;
        int bestDepth = -1;

        var all = GetComponentsInChildren<Animator>(true);
        foreach (var a in all)
        {
            if (a == null) continue;
            if (!a.gameObject.activeInHierarchy) continue;
            if (a.runtimeAnimatorController == null) continue;

            int depth = 0; Transform t = a.transform;
            while (t != null && t != this.transform) { depth++; t = t.parent; }

            if (!preferDeepestChildAnimator)
            {
                chosen = a; break;
            }
            else if (depth > bestDepth)
            {
                bestDepth = depth;
                chosen = a;
            }
        }

        animator = chosen;
        if ((verbose || forceLog) && animator)
            Debug.Log("[LobbyIdle] Using animator at " + GetPath(animator.transform));
    }

    void Apply()
    {
        if (applied) return;
        if (!animator) return;
        if (lobbyIdleClips == null || lobbyIdleClips.Length == 0) return;

        originalController = animator.runtimeAnimatorController;
        if (originalController == null)
        {
            if (verbose) Debug.Log("[LobbyIdle] No runtime controller on animator.");
            return;
        }

        int idx = Mathf.Clamp(idleVariant < 0 ? 0 : idleVariant, 0, lobbyIdleClips.Length - 1);
        var chosenClip = lobbyIdleClips[idx];

        aoc = new AnimatorOverrideController(originalController);

        // Replace the base idle clip; otherwise replace any clip whose name contains "idle"
        bool replaced = false;
        if (baseIdleClip) replaced = TryReplaceClip(aoc, baseIdleClip, chosenClip);
        if (!replaced) replaced = ReplaceAnyIdleNamedClip(aoc, chosenClip);

        animator.runtimeAnimatorController = aoc;

        // Apply safe lobby Animator settings
        if (forceAlwaysAnimateCullingInLobby) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = useUnscaledTimeInLobby ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;

        if (enforcePositiveSpeedInLobby)
            animator.speed = Mathf.Max(minLobbySpeed, idleSpeed);
        else
            animator.speed = idleSpeed;

        // Force an immediate evaluation so we can Play to an exact time right away
        animator.Update(0f);

        if (verbose)
            Debug.Log("[LobbyIdle] APPLY: variant=" + idx + " clip='" + chosenClip.name + "', speed=" + animator.speed.ToString("0.00") + ", replaced=" + replaced + ", anim=" + GetPath(animator.transform));

        applied = true;
    }

    void Revert()
    {
        if (!applied) return;
        if (animator)
        {
            animator.runtimeAnimatorController = originalController;
            animator.speed = 1f;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (verbose) Debug.Log("[LobbyIdle] REVERT -> " + GetPath(animator.transform));
        }
        aoc = null;
        applied = false;
    }

    bool TryReplaceClip(AnimatorOverrideController oc, AnimationClip from, AnimationClip to)
    {
        try { oc[from] = to; return true; }
        catch { return false; }
    }

    bool ReplaceAnyIdleNamedClip(AnimatorOverrideController oc, AnimationClip to)
    {
        var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        oc.GetOverrides(list);
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            var key = list[i].Key;
            if (key == null) continue;
            string n = key.name.ToLowerInvariant();
            if (n.Contains("idle"))
            {
                list[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, to);
                changed = true;
            }
        }
        if (changed)
            oc.ApplyOverrides((IList<KeyValuePair<AnimationClip, AnimationClip>>)list);
        return changed;
    }

    // --- Core: set Idle and jump to exact phase (optionally crossfade) ---
    void SetIdleAtPhase(float phase01, bool useCrossfade)
    {
        if (!animator) return;

        float p = Mathf.Repeat(phase01, 1f); // clamp/wrap to [0,1)
        int fullPathHash = Animator.StringToHash("Base Layer." + baseIdleStateName);
        int nameHash = Animator.StringToHash(baseIdleStateName);

        bool hasFull = animator.HasState(baseLayerIndex, fullPathHash);

        if (useCrossfade)
        {
            animator.CrossFade(hasFull ? fullPathHash : nameHash, crossfade, baseLayerIndex, p);
        }
        else
        {
            animator.Play(hasFull ? fullPathHash : nameHash, baseLayerIndex, p);
            animator.Update(0f); // immediately evaluate at that time
        }
    }

    bool StateMatches(AnimatorStateInfo st)
    {
        return st.IsName(baseIdleStateName) || st.IsName("Base Layer." + baseIdleStateName);
    }

    static string GetPath(Transform t)
    {
        if (!t) return "null";
        var stack = new List<string>();
        while (t != null) { stack.Add(t.name); t = t.parent; }
        stack.Reverse();
        return string.Join("/", stack);
    }

    // Call this from your character switcher after instantiating the new visual
    public void NotifyVisualReplaced(Animator newAnimator)
    {
        // Capture phase from old animator
        if (animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(baseLayerIndex);
            if (StateMatches(st))
            {
                float t = st.normalizedTime;
                savedPhase01 = t - Mathf.Floor(t);
            }
        }

        if (newAnimator != null) animator = newAnimator;

        // Re-apply override to the new animator and jump to saved phase
        applied = false;
        Apply();
        SetIdleAtPhase(savedPhase01, onSwitchUseCrossfade);

        if (verbose) Debug.Log("[LobbyIdle] NotifyVisualReplaced -> phase=" + savedPhase01.ToString("0.00"));
    }
}
