// FILE: CameraSurveyor.cs
// PURPOSE: Temporary diagnostic. Attach once in any always-loaded scene (or add as a prefab).
// It will print a readable table of all cameras for a few seconds when you call RunBurst().
// You can also set autoRunOnStart = true to print on scene start.

using UnityEngine;
using System.Text;

[DefaultExecutionOrder(65000)]
public class CameraSurveyor : MonoBehaviour
{
    public bool autoRunOnStart = false;
    public float burstSeconds = 2.0f;

    private float burstUntil = 0f;

    void Start()
    {
        if (autoRunOnStart) RunBurst(burstSeconds);
    }

    public void RunBurst(float seconds)
    {
        burstUntil = Time.unscaledTime + Mathf.Max(0.25f, seconds);
        Debug.Log("[CameraSurveyor] Burst started for " + seconds + "s");
    }

    void LateUpdate()
    {
        if (Time.unscaledTime > burstUntil) return;

#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var cams = Resources.FindObjectsOfTypeAll<Camera>();
#endif
        int display = 0; // Display 1

        bool anyForDisplay = false;
        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            if (cam == null) continue;
            if (!cam.gameObject.scene.IsValid()) continue; // ignore prefabs/assets

            if (cam.enabled && cam.targetDisplay == display && cam.gameObject.activeInHierarchy)
                anyForDisplay = true;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== CameraSurveyor (Display 1) ===");
        sb.AppendLine("Any camera rendering Display 1 this frame: " + anyForDisplay);
        sb.AppendLine("Name | ActiveInHierarchy | Enabled | TargetDisplay | Rect | Depth | LayerMask | Path");

        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            if (cam == null) continue;
            if (!cam.gameObject.scene.IsValid()) continue;

            string path = BuildPath(cam.transform);
            sb.AppendLine(
                cam.name + " | "
                + cam.gameObject.activeInHierarchy + " | "
                + cam.enabled + " | "
                + cam.targetDisplay + " | "
                + cam.rect + " | "
                + cam.depth + " | "
                + cam.cullingMask + " | "
                + path
            );
        }

        Debug.Log(sb.ToString());
    }

    private static string BuildPath(Transform t)
    {
        if (t == null) return "?";
        System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
        var c = t;
        while (c != null)
        {
            names.Add(c.name + (c.gameObject.activeSelf ? "" : "(inactive)"));
            c = c.parent;
        }
        names.Reverse();
        return string.Join("/", names.ToArray());
    }
}
