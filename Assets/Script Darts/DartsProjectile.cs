// FILE: DartsProjectile.cs
// Networked visual dart spawned by the server.
// - Flies on a quadratic Bezier arc.
// - Spins during flight.
// - On impact, it keeps the incoming direction and pushes the tip into the surface by stickDepth.
//   (No snapping to surface normal, so it does not roll 90 degrees.)
//
// Make sure this prefab has: NetworkIdentity + this script.

using System.Collections;
using Mirror;
using UnityEngine;

[AddComponentMenu("Minigames/Darts/Darts Projectile")]
public class DartsProjectile : NetworkBehaviour
{
    [SyncVar] public Vector3 startPos;
    [SyncVar] public Vector3 endPos;
    [SyncVar] public Vector3 hitNormal = Vector3.forward;
    [SyncVar] public double startTime;        // NetworkTime.time at launch
    [SyncVar] public float travelTime = 0.5f;
    [SyncVar] public float arcHeight = 0.15f; // relative to distance
    [SyncVar] public float spinSpeed = 540f;  // deg/sec
    [SyncVar] public float stickDepth = 0.02f;
    [SyncVar] public float lifeAfterStick = 8f;

    [Header("Model Forward")]
    [Tooltip("If true, the model's forward points toward the TIP (common: +Z).")]
    public bool tipIsModelForward = true;

    private Vector3 _lastVel = Vector3.forward;

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(CoFlight());
    }

    private IEnumerator CoFlight()
    {
        float dist = Vector3.Distance(startPos, endPos);
        Vector3 mid = (startPos + endPos) * 0.5f;
        Vector3 control = mid + Vector3.up * (Mathf.Max(0.01f, arcHeight) * dist);

        while (true)
        {
            float t = Mathf.Clamp01((float)(NetworkTime.time - startTime) / Mathf.Max(0.001f, travelTime));

            Vector3 p = Bezier2(startPos, control, endPos, t);
            Vector3 v = Bezier2Deriv(startPos, control, endPos, t);
            if (v.sqrMagnitude > 0.000001f) _lastVel = v;

            transform.position = p;

            // Face along velocity and spin around forward for a nice visual
            if (_lastVel.sqrMagnitude > 0.0001f)
            {
                Vector3 fwd = tipIsModelForward ? _lastVel.normalized : -_lastVel.normalized;
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);

            if (t >= 1f) break;
            yield return null;
        }

        // Stick: keep approach direction, push tip into the surface
        Vector3 approach = (_lastVel.sqrMagnitude > 0.0001f) ? _lastVel.normalized : (-hitNormal.normalized);
        Vector3 forward = tipIsModelForward ? approach : -approach;

        // Back the position along forward so the tip sits inside by stickDepth
        Vector3 pos = endPos - forward * Mathf.Max(0f, stickDepth);
        transform.position = pos;

        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (lifeAfterStick > 0f) yield return new WaitForSeconds(lifeAfterStick);

        if (isServer) NetworkServer.Destroy(gameObject);
        else Destroy(gameObject);
    }

    private static Vector3 Bezier2(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static Vector3 Bezier2Deriv(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return 2f * (1f - t) * (b - a) + 2f * t * (c - b);
    }
}
