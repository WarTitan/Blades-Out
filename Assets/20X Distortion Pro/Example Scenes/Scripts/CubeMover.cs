using UnityEngine;

namespace DistortionPro20X.Demo
{
    /// <summary> Moves an object back and forth along a chosen axis (Ping‑Pong). </summary>
    [AddComponentMenu("DistortionPro 20X Demo/Emissive Cube Mover")]
    public class CubeMover : MonoBehaviour
    {
        [SerializeField] private Vector3 travelVector = new Vector3(6f, 0f, 0f);
        [SerializeField] private float duration = 1f; // seconds for one full traverse

        private Vector3 _start;

        private void Awake() => _start = transform.localPosition;

        private void Update()
        {
            float t = Mathf.PingPong(Time.time / duration, 1f); // 0 → 1 → 0
            transform.localPosition = _start + travelVector * t;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 start = Application.isPlaying ? _start : transform.localPosition;
            Gizmos.DrawLine(start, start + travelVector);
        }
#endif
    }
}