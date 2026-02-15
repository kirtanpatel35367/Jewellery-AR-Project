// using UnityEngine;
// using UnityEngine.XR.ARFoundation;

// [RequireComponent(typeof(ARFace))]
// public class EarAnchorController : MonoBehaviour
// {
//     [Header("Anchors")]
//     public Transform leftEar;
//     public Transform rightEar;

//     [Header("Position Offsets")]
//     [SerializeField] private Vector3 leftOffset = new Vector3(0f, -0.1f, -0.04f);
//     [SerializeField] private Vector3 rightOffset = new Vector3(0.09f, -0.1f, -0.04f);

//     [Header("Smoothing")]
//     [SerializeField] private float smoothSpeed = 15f;

//     private ARFace arFace;
//     private Camera arCamera;

//     void Awake()
//     {
//         arFace = GetComponent<ARFace>();
//         arCamera = Camera.main;
        
//         if (arFace == null)
//         {
//             Debug.LogError("ARFace component not found!");
//             enabled = false;
//         }
//     }

//     void LateUpdate()
//     {
//         if (arFace == null || arFace.vertices.Length == 0) return;
//         if (leftEar == null && rightEar == null) return;

//         int leftEarVertex = 234;
//         int rightEarVertex = 454;

//         // Update left ear
//         if (leftEar != null && leftEarVertex < arFace.vertices.Length)
//         {
//             Vector3 basePos = arFace.transform.TransformPoint(arFace.vertices[leftEarVertex]);
            
//             // Calculate offset relative to camera view
//             Vector3 cameraRight = arCamera.transform.right;
//             Vector3 cameraUp = arCamera.transform.up;
//             Vector3 cameraForward = arCamera.transform.forward;
            
//             Vector3 offset = cameraRight * leftOffset.x + 
//                            cameraUp * leftOffset.y + 
//                            cameraForward * leftOffset.z;
            
//             Vector3 targetPos = basePos + offset;
            
//             leftEar.position = Vector3.Lerp(leftEar.position, targetPos, Time.deltaTime * smoothSpeed);
//             leftEar.rotation = Quaternion.Slerp(leftEar.rotation, arFace.transform.rotation, Time.deltaTime * smoothSpeed);
//         }

//         // Update right ear
//         if (rightEar != null && rightEarVertex < arFace.vertices.Length)
//         {
//             Vector3 basePos = arFace.transform.TransformPoint(arFace.vertices[rightEarVertex]);
            
//             // Calculate offset relative to camera view
//             Vector3 cameraRight = arCamera.transform.right;
//             Vector3 cameraUp = arCamera.transform.up;
//             Vector3 cameraForward = arCamera.transform.forward;
            
//             Vector3 offset = cameraRight * rightOffset.x + 
//                            cameraUp * rightOffset.y + 
//                            cameraForward * rightOffset.z;
            
//             Vector3 targetPos = basePos + offset;
            
//             rightEar.position = Vector3.Lerp(rightEar.position, targetPos, Time.deltaTime * smoothSpeed);
//             rightEar.rotation = Quaternion.Slerp(rightEar.rotation, arFace.transform.rotation, Time.deltaTime * smoothSpeed);
//         }
//     }
// }



// ======================================================================================


using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARFace))]
public class EarAnchorController : MonoBehaviour
{
    [Header("Anchors")]
    public Transform leftEar;
    public Transform rightEar;

    [Header("Position Offsets")]
    [SerializeField] private Vector3 leftOffset = new Vector3(0f, -0.1f, -0.04f);
    [SerializeField] private Vector3 rightOffset = new Vector3(0.09f, -0.1f, -0.04f);

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 15f;

    private ARFace arFace;
    private Camera arCamera;

    void Awake()
    {
        arFace = GetComponent<ARFace>();
        arCamera = Camera.main;
        
        if (arFace == null)
        {
            Debug.LogError("ARFace component not found!");
            enabled = false;
            return;
        }

        // Register anchors with JewelryManager when face is spawned
        RegisterWithManager();
    }

    void RegisterWithManager()
    {
        JewelryManager manager = FindObjectOfType<JewelryManager>();
        if (manager != null)
        {
            manager.RegisterAnchors(leftEar, rightEar);
            Debug.Log("✓ Anchors registered with JewelryManager!");
        }
        else
        {
            Debug.LogError("JewelryManager not found in scene!");
        }
    }

    void LateUpdate()
    {
        if (arFace == null || arFace.vertices.Length == 0) return;
        if (leftEar == null && rightEar == null) return;

        int leftEarVertex = 234;
        int rightEarVertex = 454;

        // Update left ear
        if (leftEar != null && leftEarVertex < arFace.vertices.Length)
        {
            Vector3 basePos = arFace.transform.TransformPoint(arFace.vertices[leftEarVertex]);
            
            Vector3 cameraRight = arCamera.transform.right;
            Vector3 cameraUp = arCamera.transform.up;
            Vector3 cameraForward = arCamera.transform.forward;
            
            Vector3 offset = cameraRight * leftOffset.x + 
                           cameraUp * leftOffset.y + 
                           cameraForward * leftOffset.z;
            
            Vector3 targetPos = basePos + offset;
            
            leftEar.position = Vector3.Lerp(leftEar.position, targetPos, Time.deltaTime * smoothSpeed);
            leftEar.rotation = Quaternion.Slerp(leftEar.rotation, arFace.transform.rotation, Time.deltaTime * smoothSpeed);
        }

        // Update right ear
        if (rightEar != null && rightEarVertex < arFace.vertices.Length)
        {
            Vector3 basePos = arFace.transform.TransformPoint(arFace.vertices[rightEarVertex]);
            
            Vector3 cameraRight = arCamera.transform.right;
            Vector3 cameraUp = arCamera.transform.up;
            Vector3 cameraForward = arCamera.transform.forward;
            
            Vector3 offset = cameraRight * rightOffset.x + 
                           cameraUp * rightOffset.y + 
                           cameraForward * rightOffset.z;
            
            Vector3 targetPos = basePos + offset;
            
            rightEar.position = Vector3.Lerp(rightEar.position, targetPos, Time.deltaTime * smoothSpeed);
            rightEar.rotation = Quaternion.Slerp(rightEar.rotation, arFace.transform.rotation, Time.deltaTime * smoothSpeed);
        }
    }
}