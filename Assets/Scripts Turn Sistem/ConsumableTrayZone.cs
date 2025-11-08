using UnityEngine;

[AddComponentMenu("Gameplay/Items/Consumable Tray Zone")]
public class ConsumableTrayZone : MonoBehaviour
{
    [Tooltip("Seat index (1-based) this tray belongs to (1..5, etc).")]
    public int seatIndex1Based = 1;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Just to visualize the drop zone in the editor.
        var box = GetComponent<Collider>() as BoxCollider;
        if (box == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
#endif
}
