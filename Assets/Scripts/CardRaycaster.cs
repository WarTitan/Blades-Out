using UnityEngine;
using Mirror;

[AddComponentMenu("Cards/Card Raycaster")]
public class CardRaycaster : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;                       // assign your local player's camera
    public LayerMask cardMask;               // layer for card prefabs (add a collider to card prefab)
    public LayerMask playerMask;             // layer for player hitboxes
    public float maxDistance = 8f;

    [Header("Keys")]
    public KeyCode setKey = KeyCode.V;
    public KeyCode deselectRightClick = KeyCode.Mouse1;

    // internals
    private PlayerState localPlayer;
    private CardView hoveredCard;
    private CardView selectedCard;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        CacheLocalPlayer();
        if (localPlayer == null || cam == null) return;

        // Hover (optional)
        UpdateHover();

        // Left click: select card OR cast on player
        if (Input.GetMouseButtonDown(0))
        {
            // Priority: if we already selected a card, try to click a player to cast
            if (selectedCard != null)
            {
                var target = RaycastPlayer();
                if (target != null && target != localPlayer)
                {
                    // cast to target
                    if (selectedCard.isInHand && selectedCard.handIndex >= 0)
                    {
                        localPlayer.CmdPlayInstant(selectedCard.handIndex, target.netId);
                        // the server will remove the card -> card object likely gets destroyed on next rebuild
                        selectedCard = null;
                    }
                    return;
                }
            }

            // Otherwise, try select a card we are looking at
            var cv = RaycastCard();
            if (cv != null)
            {
                SelectCard(cv);
            }
        }

        // Right click: deselect
        if (Input.GetKeyDown(deselectRightClick))
        {
            Deselect();
        }

        // Press V to Set (only if selected card is from hand)
        if (Input.GetKeyDown(setKey))
        {
            if (selectedCard != null && selectedCard.isInHand && selectedCard.handIndex >= 0)
            {
                localPlayer.CmdSetCard(selectedCard.handIndex);
                Deselect(); // the instance will be rebuilt in set row
            }
        }
    }

    private void CacheLocalPlayer()
    {
        if (localPlayer != null) return;
#if UNITY_2023_1_OR_NEWER
        var arr = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
#else
        var arr = Object.FindObjectsOfType<PlayerState>();
#endif
        foreach (var ps in arr)
        {
            if (ps.isLocalPlayer)
            {
                localPlayer = ps;
                break;
            }
        }
    }

    private void UpdateHover()
    {
        var cv = RaycastCard();
        if (cv != hoveredCard)
        {
            hoveredCard = cv;
            // (Optional) you can add a subtle scale/outline here
        }
    }

    private void SelectCard(CardView cv)
    {
        if (selectedCard == cv) return;
        if (selectedCard != null) selectedCard.SetSelected(false);

        selectedCard = cv;
        selectedCard.SetSelected(true);
        Debug.Log($"[Select] Card id {cv.cardId} at handIndex {cv.handIndex} (inHand={cv.isInHand})");
    }

    private void Deselect()
    {
        if (selectedCard != null)
        {
            selectedCard.SetSelected(false);
            selectedCard = null;
        }
    }

    private CardView RaycastCard()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out var hit, maxDistance, cardMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.GetComponentInParent<CardView>();
        }
        return null;
    }

    private PlayerState RaycastPlayer()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out var hit, maxDistance, playerMask, QueryTriggerInteraction.Ignore))
        {
            var ps = hit.collider.GetComponentInParent<PlayerState>();
            return ps;
        }
        return null;
    }
}
