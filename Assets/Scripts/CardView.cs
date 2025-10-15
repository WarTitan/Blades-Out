using UnityEngine;

[AddComponentMenu("Cards/Card View")]
public class CardView : MonoBehaviour
{
    public PlayerState owner;
    public int handIndex = -1;           // index in owner's hand list at spawn-time
    public int cardId = -1;
    public int level = 1;
    public bool isInHand = true;         // false if in set row

    [Header("Selection Visual")]
    public float hoverHeight = 0.06f;    // meters
    public float moveLerp = 12f;         // speed
    public HoverAxis hoverAxis = HoverAxis.WorldUp;

    public enum HoverAxis { WorldUp, LocalY, LocalZ }

    Vector3 baseWorldPos;
    bool selected;

    Transform _parent;

    public void Init(PlayerState owner, int handIndex, int cardId, int level, bool isInHand)
    {
        this.owner = owner;
        this.handIndex = handIndex;
        this.cardId = cardId;
        this.level = level;
        this.isInHand = isInHand;

        _parent = transform.parent;
        baseWorldPos = transform.position;
    }

    public void SetSelected(bool on)
    {
        selected = on;
        // record current base pos when selecting so it lifts relative to current
        if (on) baseWorldPos = transform.position;
    }

    void LateUpdate()
    {
        if (_parent == null) _parent = transform.parent;
        Vector3 target = baseWorldPos;
        if (selected)
        {
            Vector3 lift;
            switch (hoverAxis)
            {
                case HoverAxis.LocalY: lift = _parent ? _parent.up : transform.up; break;
                case HoverAxis.LocalZ: lift = _parent ? _parent.forward : transform.forward; break;
                default: lift = Vector3.up; break;
            }
            target += lift.normalized * hoverHeight;
        }
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-moveLerp * Time.deltaTime));
    }
}
