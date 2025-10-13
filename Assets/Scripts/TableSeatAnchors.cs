using UnityEngine;
using System.Collections.Generic;

public class TableSeatAnchors : MonoBehaviour
{
    public static TableSeatAnchors Instance;

    [Header("Assign 5 hand anchors in seat order (0..4)")]
    public List<Transform> handAnchors = new List<Transform>();

    void Awake()
    {
        Instance = this;
    }

    public Transform GetHandAnchor(int seat)
    {
        if (handAnchors == null) return null;
        if (seat < 0 || seat >= handAnchors.Count) return null;
        return handAnchors[seat];
    }
}
