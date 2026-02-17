using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

// [RequireComponent(typeof(ARFace))]
// public class ProfessionalNecklaceAR : MonoBehaviour
// {
//     [Header("Necklace Setup")]
//     public GameObject necklacePrefab;
    
//     [Header("Positioning")]
//     [SerializeField] private float neckDropDistance = 0.20f; //0.12
//     [SerializeField] private float forwardDepth = 0.07f; //0.02
//     [SerializeField] private Vector3 manualOffset = Vector3.zero;
    
//     [Header("Stabilization")]
//     [SerializeField] private float positionDamping = 15f;
//     [SerializeField] private float rotationDamping = 12f;
//     [SerializeField] private bool useKalmanFilter = true;
//     [SerializeField] private float kalmanQ = 0.001f;
//     [SerializeField] private float kalmanR = 0.01f;
    
//     [Header("Rotation Control")]
//     [SerializeField] private bool lockPitch = true;
//     [SerializeField] private bool lockRoll = true;
//     [SerializeField] private float yawFollowStrength = 0.3f;
    
//     [Header("Visual Enhancements")]
//     [SerializeField] private bool enableDepthOcclusion = true;
//     [SerializeField] private bool enableSoftShadows = true;
//     [SerializeField] private float shadowIntensity = 0.6f;
//     [SerializeField] private bool enableEnvironmentReflections = true;
    
//     // ARCore face vertex indices for neck tracking
//     private static readonly int[] NECK_TRACKING_INDICES = new int[]
//     {
//         152, // Chin center
//         175, // Neck left
//         400, // Neck right
//         148, // Chin left
//         377  // Chin right
//     };
    
//     private ARFace arFace;
//     private GameObject necklaceInstance;
//     private ARCameraManager arCameraManager;
    
//     // Stabilization
//     private KalmanFilter positionFilter;
//     private Vector3 smoothedPosition;
//     private Quaternion smoothedRotation;
//     private Vector3 velocity;
    
//     // Rendering
//     private Renderer necklaceRenderer;
//     private Material necklaceMaterial;
//     private Light dynamicLight;
    
//     // Light estimation
//     private float currentBrightness = 1f;
    
//     void Awake()
//     {
//         arFace = GetComponent<ARFace>();
//         arCameraManager = FindObjectOfType<ARCameraManager>();
        
//         InitializeNecklace();
//         SetupFilters();
//         ConfigureRendering();
//     }
    
//     void OnEnable()
//     {
//         if (arCameraManager != null)
//         {
//             arCameraManager.frameReceived += OnCameraFrameReceived;
//         }
//     }
    
//     void OnDisable()
//     {
//         if (arCameraManager != null)
//         {
//             arCameraManager.frameReceived -= OnCameraFrameReceived;
//         }
        
//         if (necklaceInstance != null)
//         {
//             necklaceInstance.SetActive(false);
//         }
//     }
    
//     void InitializeNecklace()
//     {
//         if (necklacePrefab == null)
//         {
//             Debug.LogError("Necklace prefab not assigned!");
//             return;
//         }
        
//         necklaceInstance = Instantiate(necklacePrefab);
//         necklaceInstance.transform.SetParent(null); // Independent from face transform
//         necklaceRenderer = necklaceInstance.GetComponentInChildren<Renderer>();
        
//         if (necklaceRenderer != null)
//         {
//             necklaceMaterial = necklaceRenderer.material;
//         }
//     }
    
//     void SetupFilters()
//     {
//         if (useKalmanFilter)
//         {
//             positionFilter = new KalmanFilter(kalmanQ, kalmanR);
//         }
        
//         smoothedPosition = Vector3.zero;
//         smoothedRotation = Quaternion.identity;
//     }
    
//     void ConfigureRendering()
//     {
//         if (necklaceRenderer == null) return;
        
//         // Enable GPU instancing for performance
//         necklaceRenderer.shadowCastingMode = enableSoftShadows 
//             ? UnityEngine.Rendering.ShadowCastingMode.On 
//             : UnityEngine.Rendering.ShadowCastingMode.Off;
        
//         necklaceRenderer.receiveShadows = true;
        
//         // Setup depth occlusion
//         if (enableDepthOcclusion && arCameraManager != null)
//         {
//             SetupDepthOcclusion();
//         }
        
//         // Setup PBR material properties
//         if (necklaceMaterial != null)
//         {
//             ConfigurePBRMaterial();
//         }
        
//         // Add dynamic lighting
//         if (enableSoftShadows)
//         {
//             CreateDynamicLight();
//         }
//     }
    
//     void SetupDepthOcclusion()
//     {
//         // Enable ARCore depth if available
//         var occlusionManager = FindObjectOfType<AROcclusionManager>();
//         if (occlusionManager != null)
//         {
//             occlusionManager.requestedOcclusionPreferenceMode = 
//                 OcclusionPreferenceMode.PreferEnvironmentOcclusion;
//         }
        
//         // Set render queue for proper occlusion
//         if (necklaceMaterial != null)
//         {
//             necklaceMaterial.renderQueue = 2001; // After transparent, before overlay
//         }
//     }
    
//     void ConfigurePBRMaterial()
//     {
//         // Enable realistic lighting
//         necklaceMaterial.EnableKeyword("_METALLICGLOSSMAP");
//         necklaceMaterial.EnableKeyword("_NORMALMAP");
        
//         if (enableEnvironmentReflections)
//         {
//             necklaceMaterial.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
//             necklaceMaterial.SetFloat("_GlossyReflections", 1f);
//         }
        
//         // Set appropriate smoothness for jewelry
//         if (necklaceMaterial.HasProperty("_Glossiness"))
//         {
//             necklaceMaterial.SetFloat("_Glossiness", 0.8f);
//         }
        
//         if (necklaceMaterial.HasProperty("_Metallic"))
//         {
//             necklaceMaterial.SetFloat("_Metallic", 0.9f);
//         }
//     }
    
//     void CreateDynamicLight()
//     {
//         GameObject lightObj = new GameObject("NecklaceLight");
//         lightObj.transform.SetParent(necklaceInstance.transform);
        
//         dynamicLight = lightObj.AddComponent<Light>();
//         dynamicLight.type = LightType.Point;
//         dynamicLight.range = 0.5f;
//         dynamicLight.intensity = shadowIntensity;
//         dynamicLight.shadows = LightShadows.Soft;
//         dynamicLight.shadowStrength = 0.4f;
//         dynamicLight.color = new Color(1f, 0.95f, 0.9f); // Warm light
        
//         lightObj.transform.localPosition = new Vector3(0, 0.1f, 0.1f);
//     }
    
//     void Update()
//     {
//         if (necklaceInstance == null || arFace == null) return;
        
//         // FIX #1: Check tracking state properly
//         if (arFace.trackingState == TrackingState.None || 
//             arFace.trackingState == TrackingState.Limited)
//         {
//             necklaceInstance.SetActive(false);
//             return;
//         }
        
//         necklaceInstance.SetActive(true);
        
//         // Calculate optimal neck position using multiple anchor points
//         Vector3 targetPosition = CalculateNeckPosition();
//         Quaternion targetRotation = CalculateNeckRotation();
        
//         // Apply stabilization
//         ApplyStabilization(targetPosition, targetRotation);
        
//         // Update lighting based on environment
//         UpdateDynamicLighting();
//     }
    
//     Vector3 CalculateNeckPosition()
//     {
//         if (arFace.vertices.Length == 0) return smoothedPosition;
        
//         List<Vector3> neckPoints = new List<Vector3>();
        
//         // Gather neck anchor points
//         foreach (int index in NECK_TRACKING_INDICES)
//         {
//             if (index < arFace.vertices.Length)
//             {
//                 Vector3 localPoint = arFace.vertices[index];
//                 Vector3 worldPoint = arFace.transform.TransformPoint(localPoint);
//                 neckPoints.Add(worldPoint);
//             }
//         }
        
//         if (neckPoints.Count == 0) return smoothedPosition;
        
//         // Calculate centroid of neck points
//         Vector3 neckCenter = Vector3.zero;
//         foreach (Vector3 point in neckPoints)
//         {
//             neckCenter += point;
//         }
//         neckCenter /= neckPoints.Count;
        
//         // Apply offsets for natural positioning
//         Vector3 downwardOffset = arFace.transform.up * -neckDropDistance;
//         Vector3 depthOffset = arFace.transform.forward * forwardDepth;
        
//         return neckCenter + downwardOffset + depthOffset + manualOffset;
//     }
    
//     Quaternion CalculateNeckRotation()
//     {
//         // Get face rotation
//         Vector3 faceEuler = arFace.transform.eulerAngles;
        
//         float pitch = lockPitch ? 0 : faceEuler.x;
//         float yaw = faceEuler.y;
//         float roll = lockRoll ? 0 : faceEuler.z;
        
//         // Reduce yaw influence for natural look
//         yaw = Mathf.LerpAngle(0, yaw, yawFollowStrength);
        
//         return Quaternion.Euler(pitch, yaw, roll);
//     }
    
//     void ApplyStabilization(Vector3 targetPosition, Quaternion targetRotation)
//     {
//         // Kalman filtering for position
//         if (useKalmanFilter && positionFilter != null)
//         {
//             targetPosition = positionFilter.Update(targetPosition);
//         }
        
//         // Smooth damping
//         smoothedPosition = Vector3.SmoothDamp(
//             smoothedPosition,
//             targetPosition,
//             ref velocity,
//             1f / positionDamping
//         );
        
//         smoothedRotation = Quaternion.Slerp(
//             smoothedRotation,
//             targetRotation,
//             Time.deltaTime * rotationDamping
//         );
        
//         // Apply to necklace
//         necklaceInstance.transform.position = smoothedPosition;
//         necklaceInstance.transform.rotation = smoothedRotation;
//     }
    
//     // FIX #2: Proper event handler for light estimation
//     void OnCameraFrameReceived(ARCameraFrameEventArgs args)
//     {
//         // Update brightness based on AR light estimation
//         if (args.lightEstimation.averageBrightness.HasValue)
//         {
//             currentBrightness = args.lightEstimation.averageBrightness.Value;
//         }
        
//         // Update color temperature if available
//         if (dynamicLight != null && args.lightEstimation.averageColorTemperature.HasValue)
//         {
//             float colorTemp = args.lightEstimation.averageColorTemperature.Value;
//             dynamicLight.color = ColorTemperatureToRGB(colorTemp);
//         }
//     }
    
//     void UpdateDynamicLighting()
//     {
//         if (dynamicLight == null) return;
        
//         // Adjust light intensity based on environment brightness
//         dynamicLight.intensity = shadowIntensity * currentBrightness;
//     }
    
//     // Convert Kelvin temperature to RGB color
//     Color ColorTemperatureToRGB(float kelvin)
//     {
//         // Clamp to reasonable range
//         kelvin = Mathf.Clamp(kelvin, 1000f, 40000f) / 100f;
        
//         float red, green, blue;
        
//         // Calculate red
//         if (kelvin <= 66f)
//         {
//             red = 255f;
//         }
//         else
//         {
//             red = kelvin - 60f;
//             red = 329.698727446f * Mathf.Pow(red, -0.1332047592f);
//             red = Mathf.Clamp(red, 0f, 255f);
//         }
        
//         // Calculate green
//         if (kelvin <= 66f)
//         {
//             green = kelvin;
//             green = 99.4708025861f * Mathf.Log(green) - 161.1195681661f;
//         }
//         else
//         {
//             green = kelvin - 60f;
//             green = 288.1221695283f * Mathf.Pow(green, -0.0755148492f);
//         }
//         green = Mathf.Clamp(green, 0f, 255f);
        
//         // Calculate blue
//         if (kelvin >= 66f)
//         {
//             blue = 255f;
//         }
//         else if (kelvin <= 19f)
//         {
//             blue = 0f;
//         }
//         else
//         {
//             blue = kelvin - 10f;
//             blue = 138.5177312231f * Mathf.Log(blue) - 305.0447927307f;
//             blue = Mathf.Clamp(blue, 0f, 255f);
//         }
        
//         return new Color(red / 255f, green / 255f, blue / 255f);
//     }
    
//     void OnDestroy()
//     {
//         if (necklaceInstance != null)
//         {
//             Destroy(necklaceInstance);
//         }
//     }
    
//     // Performance optimization
//     void OnBecameInvisible()
//     {
//         enabled = false;
//     }
    
//     void OnBecameVisible()
//     {
//         enabled = true;
//     }
// }

// /// <summary>
// /// NecklaceAttachARCore
// /// ────────────────────
// /// Creates one invisible anchor point that tracks the neck/chin area
// /// on the detected ARCore face mesh.
// ///
// /// Registers that anchor with JewelryManager so the necklace spawned
// /// by the UI is automatically parented to the correct neck position.
// ///
// /// Nothing is ever rendered by this script.
// /// </summary>
[RequireComponent(typeof(ARFace))]
public class NecklaceAttachARCore : MonoBehaviour
{
    // Chin / neck vertex indices in the 468-point ARCore face mesh
    private static readonly int[] NECK_VERTS = { 152, 175, 400, 148, 377 };

    [Header("Position  (tweak if necklace sits in wrong place)")]
    public float neckDrop = 0.12f;       // how far below the chin
    public float forward = 0.02f;       // push forward from face plane
    public Vector3 extraOffset = Vector3.zero; // manual fine-tune

    [Header("Smoothing")]
    public float posSmooth = 12f;
    public float rotSmooth = 10f;

    [Header("Rotation follow")]
    public bool lockPitch = true;
    public bool lockRoll = true;
    [Range(0f, 1f)]
    public float yawFollow = 0.3f;   // 0 = ignore head-turn, 1 = full follow

    private ARFace face;
    private Transform neckAnchor;
    private Vector3 vel;
    private bool anchorRegistered = false;

    void Awake()
    {
        face = GetComponent<ARFace>();

        // Anchor is parented to the ARFace transform so it stays in the
        // right coordinate space; smoothing is applied on top in Update().
        neckAnchor = new GameObject("NecklaceAnchor").transform;
        neckAnchor.SetParent(transform);

        RegisterWithManager();
    }

    void OnEnable()
    {
        // Re-register if the ARFace was recycled after tracking loss
        if (!anchorRegistered)
            RegisterWithManager();
    }

    void RegisterWithManager()
    {
        JewelryManager mgr = FindObjectOfType<JewelryManager>();
        if (mgr != null)
        {
            mgr.RegisterNecklaceAnchor(neckAnchor);
            anchorRegistered = true;
            Debug.Log("[NecklaceAttachARCore] Necklace anchor registered.");
        }
        else
        {
            Debug.LogError("[NecklaceAttachARCore] JewelryManager not found!");
        }
    }

    void Update()
    {
        if (face == null || neckAnchor == null) return;
        if (face.trackingState == TrackingState.None ||
            face.trackingState == TrackingState.Limited) return;
        if (face.vertices.Length == 0) return;

        neckAnchor.position = Vector3.SmoothDamp(
            neckAnchor.position, TargetPos(), ref vel, 1f / posSmooth);

        neckAnchor.rotation = Quaternion.Slerp(
            neckAnchor.rotation, TargetRot(), Time.deltaTime * rotSmooth);
    }

    Vector3 TargetPos()
    {
        // Average the chin/neck vertices for a stable base point
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (int idx in NECK_VERTS)
        {
            if (idx < face.vertices.Length)
            {
                center += face.transform.TransformPoint(face.vertices[idx]);
                count++;
            }
        }
        if (count == 0) return neckAnchor.position;
        center /= count;

        return center
             + face.transform.up * -neckDrop
             + face.transform.forward * forward
             + extraOffset;
    }

    Quaternion TargetRot()
    {
        Vector3 e = face.transform.eulerAngles;
        return Quaternion.Euler(
            lockPitch ? 0f : e.x,
            Mathf.LerpAngle(0f, e.y, yawFollow),
            lockRoll ? 0f : e.z);
    }

    void OnDestroy()
    {
        if (neckAnchor != null) Destroy(neckAnchor.gameObject);
    }
}