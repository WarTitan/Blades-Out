using UnityEngine;

public class TableSeatAnchors : MonoBehaviour
{
    public static TableSeatAnchors Instance { get; private set; }

    [Header("Hand Anchors (size = number of seats)")]
    public Transform[] handAnchors;

    [Header("Set Anchors (size must match Hand Anchors)")]
    public Transform[] setAnchors;

    void Awake()
    {
        Instance = this;
    }

    public Transform GetHandAnchor(int seatIndex)
    {
        if (handAnchors == null || handAnchors.Length == 0) return null;
        seatIndex = Mathf.Clamp(seatIndex, 0, handAnchors.Length - 1);
        return handAnchors[seatIndex];
    }

    public Transform GetSetAnchor(int seatIndex)
    {
        if (setAnchors == null || setAnchors.Length == 0) return null;
        seatIndex = Mathf.Clamp(seatIndex, 0, setAnchors.Length - 1);
        return setAnchors[seatIndex];
    }
}
