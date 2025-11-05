using UnityEngine;
namespace DistortionPro20X.Demo
{
    /// <summary> Constantly rotates an object around its local axis. </summary>
    [AddComponentMenu("DistortionPro 20X Demo/Neon Ring Rotator")]
    public class RingRotator : MonoBehaviour
    {
        [SerializeField] private Vector3 rotationAxis = Vector3.forward; // Z‑axis
        [SerializeField] private float degreesPerSecond = 30f;

        private void Update()
        {
            transform.Rotate(rotationAxis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}