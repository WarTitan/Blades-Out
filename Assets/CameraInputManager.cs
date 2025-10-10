using UnityEngine;
using UnityEngine.InputSystem;

public static class CameraInputManager
{
    private static Vector2 _lookInput;

    public static Vector2 LookInput => _lookInput;

    public static void UpdateLookInput()
    {
        if (Mouse.current == null)
            return;

        _lookInput = Mouse.current.delta.ReadValue();
    }

    public static int GetActiveDisplayIndex()
    {
        if (Mouse.current == null)
            return 0;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        int totalWidth = 0;
        for (int i = 0; i < Display.displays.Length; i++)
        {
            Display disp = Display.displays[i];
            int width = disp.systemWidth;
            int height = disp.systemHeight;

            if (mousePos.x >= totalWidth && mousePos.x < totalWidth + width &&
                mousePos.y >= 0 && mousePos.y < height)
                return i;

            totalWidth += width;
        }

        return 0;
    }
}
