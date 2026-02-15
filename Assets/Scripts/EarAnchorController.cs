using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARFace))]
public class EarAttachARCore : MonoBehaviour
{
    public GameObject leftEarringPrefab;
    public GameObject rightEarringPrefab;

    private ARFace arFace;

    private GameObject leftEarring;
    private GameObject rightEarring;

    // ARCore approximate ear vertices
    private const int LEFT_EAR_INDEX = 234;
    private const int RIGHT_EAR_INDEX = 454;

    void Awake()
    {
        arFace = GetComponent<ARFace>();

        leftEarring = Instantiate(leftEarringPrefab, transform);
        rightEarring = Instantiate(rightEarringPrefab, transform);

        // Mirror right earring
        rightEarring.transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    void Update()
    {
        if (arFace.vertices.Length <= RIGHT_EAR_INDEX)
            return;

        Vector3 leftEarLocal = arFace.vertices[LEFT_EAR_INDEX];
        Vector3 rightEarLocal = arFace.vertices[RIGHT_EAR_INDEX];

        Vector3 leftEarWorld = arFace.transform.TransformPoint(leftEarLocal);
        Vector3 rightEarWorld = arFace.transform.TransformPoint(rightEarLocal);

        // Offset values (we will tweak these)
        float outwardOffset = -0.01f;  // push away from face
        float downwardOffset = -0.035f; // hang down
        float depthOffset = -0.05f; // slight backward

        Vector3 leftOffset =
            arFace.transform.right * outwardOffset +
            arFace.transform.up * downwardOffset +
            -arFace.transform.forward * depthOffset;


        Vector3 rightOffset =
            -arFace.transform.right * outwardOffset +
            arFace.transform.up * downwardOffset +
            -arFace.transform.forward * depthOffset;


        leftEarring.transform.position =
            Vector3.Lerp(leftEarring.transform.position,
                        leftEarWorld + leftOffset,
                        15f * Time.deltaTime);

        rightEarring.transform.position =
            Vector3.Lerp(rightEarring.transform.position,
                        rightEarWorld + rightOffset,
                        15f * Time.deltaTime);
    }

}
