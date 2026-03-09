using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class NecklaceFollowFace : MonoBehaviour
{
    [Header("How much to follow head rotation")]
    [Range(0f, 1f)] public float followYaw = 1f;     // left-right
    [Range(0f, 1f)] public float followPitch = 0.5f; // up-down
    [Range(0f, 1f)] public float followRoll = 0.5f;  // tilt

    [Header("Optional fine-tune (rarely needed)")]
    public Vector3 extraLocalEuler = Vector3.zero;

    ARFace face;
    Transform parentT;

    void Awake()
    {
        parentT = transform.parent;
        face = FindObjectOfType<ARFace>();
    }

    void OnEnable()
    {
        if (!parentT) parentT = transform.parent;
        if (!face) face = FindObjectOfType<ARFace>();
    }

    void LateUpdate()
    {
        if (!face || !parentT) return;

        Vector3 e = face.transform.eulerAngles;

        float yaw   = Mathf.LerpAngle(0f, e.y, followYaw);
        float pitch = Mathf.LerpAngle(0f, e.x, followPitch);
        float roll  = Mathf.LerpAngle(0f, e.z, followRoll);

        Quaternion desiredWorldRot = Quaternion.Euler(pitch, yaw, roll);

        // Convert world rotation into local rotation relative to NecklaceAnchor
        transform.localRotation = Quaternion.Inverse(parentT.rotation) * desiredWorldRot;

        if (extraLocalEuler != Vector3.zero)
            transform.localRotation *= Quaternion.Euler(extraLocalEuler);
    }
}