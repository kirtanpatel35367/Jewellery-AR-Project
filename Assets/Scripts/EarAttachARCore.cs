using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

/// <summary>
/// EarAttachARCore — Fixed for Xiaomi/MIUI devices.
///
/// FIXES:
/// • Registration retried every 0.2s until JewelryManager is found.
/// • Vertex array guarded before every access.
/// • Earrings tilt naturally with head rotation.
/// </summary>
[RequireComponent(typeof(ARFace))]
public class EarAttachARCore : MonoBehaviour
{
    private const int LEFT_EAR = 234;
    private const int RIGHT_EAR = 454;

    [Header("Offsets (tweak if earrings sit wrong)")]
    public float outward = -0.010f;
    public float downward = -0.035f;
    public float depth = 0.050f;

    private ARFace face;
    private Transform leftAnchor;
    private Transform rightAnchor;
    private JewelryManager mgr;
    private bool registered;

    void Awake()
    {
        face = GetComponent<ARFace>();

        leftAnchor = new GameObject("LeftEarAnchor").transform;
        rightAnchor = new GameObject("RightEarAnchor").transform;
        leftAnchor.SetParent(transform);
        rightAnchor.SetParent(transform);

        StartCoroutine(RegisterLoop());
    }

    IEnumerator RegisterLoop()
    {
        while (!registered)
        {
            mgr = FindObjectOfType<JewelryManager>();
            if (mgr != null)
            {
                mgr.RegisterEarAnchors(leftAnchor, rightAnchor);
                registered = true;
                Debug.Log("[EarAttachARCore] Registered.");
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    void Update()
    {
        if (face == null) return;
        if (face.trackingState != TrackingState.Tracking) return;
        if (face.vertices.Length <= RIGHT_EAR) return;

        Vector3 lw = face.transform.TransformPoint(face.vertices[LEFT_EAR]);
        Vector3 rw = face.transform.TransformPoint(face.vertices[RIGHT_EAR]);

        Vector3 lo = face.transform.right * outward
                   + face.transform.up * downward
                   + face.transform.forward * depth;
        Vector3 ro = face.transform.right * -outward
                   + face.transform.up * downward
                   + face.transform.forward * depth;

        float t = 15f * Time.deltaTime;
        leftAnchor.position = Vector3.Lerp(leftAnchor.position, lw + lo, t);
        rightAnchor.position = Vector3.Lerp(rightAnchor.position, rw + ro, t);
        leftAnchor.rotation = Quaternion.Slerp(leftAnchor.rotation, face.transform.rotation, t);
        rightAnchor.rotation = Quaternion.Slerp(rightAnchor.rotation, face.transform.rotation, t);
    }

    void OnDestroy()
    {
        if (leftAnchor != null) Destroy(leftAnchor.gameObject);
        if (rightAnchor != null) Destroy(rightAnchor.gameObject);
    }
}