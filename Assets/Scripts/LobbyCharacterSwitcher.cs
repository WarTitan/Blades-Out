using UnityEngine;
using Mirror;

public class LobbyCharacterSwitcher : NetworkBehaviour
{
    [Header("Visuals")]
    public Transform visualRoot;               // empty child for the model
    public GameObject[] characterVisualPrefabs;

    [SyncVar(hook = nameof(OnIndexChanged))] public int index = 0;
    private GameObject currentVisual;

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyVisual();
    }

    [Command(requiresAuthority = true)]
    public void CmdCycle(int delta)
    {
        if (LobbyStage.Instance && !LobbyStage.Instance.lobbyActive) return; // lobby only
        int count = characterVisualPrefabs != null ? characterVisualPrefabs.Length : 0;
        if (count == 0) return;
        int newIndex = (index + delta) % count;
        if (newIndex < 0) newIndex += count;
        index = newIndex;
    }

    void OnIndexChanged(int oldV, int newV) => ApplyVisual();

    void ApplyVisual()
    {
        if (visualRoot == null) visualRoot = this.transform;

        if (currentVisual != null) { Destroy(currentVisual); currentVisual = null; }

        var prefab = GetPrefab(index);
        if (!prefab) return;

        currentVisual = Instantiate(prefab, visualRoot);
        currentVisual.transform.localPosition = Vector3.zero;
        currentVisual.transform.localRotation = Quaternion.identity;
        currentVisual.transform.localScale = Vector3.one;
    }

    GameObject GetPrefab(int i)
    {
        if (characterVisualPrefabs == null || characterVisualPrefabs.Length == 0) return null;
        if (i < 0 || i >= characterVisualPrefabs.Length) return null;
        return characterVisualPrefabs[i];
    }
}
