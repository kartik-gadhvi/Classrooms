using UnityEngine;

/// <summary>
/// Puts the learner's eyes on the camera rig's placed spot, facing the way the rig faces.
/// Floor tracking otherwise adds the headset's real position and yaw, so the learner
/// would appear somewhere else in the room, turned whichever way they stand at home.
/// </summary>
public class PlayerStartPose : MonoBehaviour
{
    private Transform eyes;
    private Vector3 spawnPosition;
    private float spawnYaw;
    private float giveUpAt;
    private float lockAt = -1f;

    private void Awake()
    {
        var rig = GetComponent<OVRCameraRig>();
        eyes = rig != null ? rig.centerEyeAnchor : null;
        spawnPosition = transform.position;
        spawnYaw = transform.eulerAngles.y;
    }

    private void Start()
    {
        giveUpAt = Time.time + 3f;
    }

    private void LateUpdate()
    {
        if (eyes == null || Time.time > giveUpAt)
        {
            enabled = false;
            return;
        }

        if (lockAt > 0f && Time.time > lockAt)
        {
            enabled = false;
            return;
        }

        if (!OVRNodeStateProperties.IsHmdPresent())
            return;

        if (eyes.localPosition.sqrMagnitude < 0.002f)
            return;

        if (lockAt < 0f)
            lockAt = Time.time + 0.5f;

        float yaw = Mathf.DeltaAngle(eyes.eulerAngles.y, spawnYaw);
        transform.Rotate(0f, yaw, 0f, Space.World);
        transform.position += spawnPosition - eyes.position;
    }
}
