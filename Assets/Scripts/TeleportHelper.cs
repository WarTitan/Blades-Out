using UnityEngine;
using Mirror;

public class TeleportHelper : NetworkBehaviour
{
    [TargetRpc]
    public void TargetSnapAndEnterGameplay(NetworkConnectionToClient conn, Vector3 pos, Quaternion rot)
    {
        // Safe warp for CharacterController or Rigidbody
        var cc = GetComponent<CharacterController>();
        if (cc)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            cc.enabled = was;
        }
        else
        {
            var rb = GetComponent<Rigidbody>();
            if (rb)
            {
                bool kin = rb.isKinematic;
                rb.isKinematic = true;
                rb.position = pos;
                rb.rotation = rot;
                rb.isKinematic = kin;
            }
            else
            {
                transform.SetPositionAndRotation(pos, rot);
            }
        }

        Debug.Log("[TeleportHelper] Owner snap applied -> gameplay");

        // Force local gameplay state (camera on, input/crosshair on)
        var lca = GetComponent<LocalCameraActivator>();
        if (lca && lca.isLocalPlayer) lca.ForceEnterGameplay();

        var lld = GetComponent<LobbyLocalDisabler>();
        if (lld && lld.isLocalPlayer) lld.ForceEnableGameplay();

        // Extra safety: disable lobby cam on this client
        if (LobbyStage.Instance && LobbyStage.Instance.lobbyCamera)
            LobbyStage.Instance.lobbyCamera.enabled = false;
    }
}
