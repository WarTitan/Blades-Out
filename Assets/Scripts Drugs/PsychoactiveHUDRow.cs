// FILE: PsychoactiveHUDRow.cs
// FULL REPLACEMENT
// Drag your two UI objects (either legacy Text or TMP_Text GameObjects) into these fields.
// The script auto-detects whether they are Text or TMP_Text and sets the text accordingly.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PsychoactiveHUDRow : MonoBehaviour
{
    [Header("Assign the GameObjects that hold the text components")]
    public GameObject nameObject;
    public GameObject timeObject;

    private Text nameText;
    private Text timeText;
    private TMP_Text nameTMP;
    private TMP_Text timeTMP;

    void Awake()
    {
        Cache();
    }

    private void Cache()
    {
        if (nameObject != null)
        {
            nameText = nameObject.GetComponent<Text>();
            nameTMP = nameObject.GetComponent<TMP_Text>();
        }
        if (timeObject != null)
        {
            timeText = timeObject.GetComponent<Text>();
            timeTMP = timeObject.GetComponent<TMP_Text>();
        }
    }

    public void SetName(string value)
    {
        if (nameText != null) nameText.text = value;
        if (nameTMP != null) nameTMP.text = value;
    }

    public void SetTime(string value)
    {
        if (timeText != null) timeText.text = value;
        if (timeTMP != null) timeTMP.text = value;
    }
}
