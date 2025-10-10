using UnityEngine;

public class MultiDisplayInitializer : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"Displays connected: {Display.displays.Length}");

        // Activate all connected displays
        for (int i = 0; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
            Debug.Log($"Activated display {i + 1}");
        }
    }
}
