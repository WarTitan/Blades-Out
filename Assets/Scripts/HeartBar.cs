using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HeartBar : MonoBehaviour
{
    [SerializeField] private List<Image> hearts = new List<Image>();
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;

    private Transform spawnPoint; // The player's card spawn point
    private Camera cam;

    // --- 2-argument overload (kept for compatibility) ---
    public void Initialize(Transform followTarget, Camera camera)
    {
        Initialize(followTarget, camera, null);
    }

    // --- Main initialization: lookAtSpawnPoint = cardSpawnPoint ---
    public void Initialize(Transform followTarget, Camera camera, Transform lookAtSpawnPoint)
    {
        cam = camera;
        spawnPoint = lookAtSpawnPoint;
    }

    public void SetHearts(int currentHearts, int maxHearts)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            hearts[i].sprite = (i < currentHearts) ? fullHeart : emptyHeart;
        }
    }

    void LateUpdate()
    {
        if (spawnPoint == null || cam == null) return;

        // 🩸 Position: float above the spawn point (not camera)
        float verticalOffset = 1.1f;  // height above the card area
        Vector3 aboveSpawn = spawnPoint.position + Vector3.up * verticalOffset;
        transform.position = aboveSpawn;

        // 🧭 Rotate the heart bar to face the player's camera
        Vector3 dirToCamera = (cam.transform.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(dirToCamera, Vector3.up);

        // Flip 180° if your canvas faces backwards
        transform.Rotate(-40f, 0f, 0f, Space.Self);
    }
}
