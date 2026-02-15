using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

[RequireComponent(typeof(ARFace))]
public class ProfessionalNecklaceAR : MonoBehaviour
{
    [Header("Necklace Setup")]
    public GameObject necklacePrefab;
    
    [Header("Positioning")]
    [SerializeField] private float neckDropDistance = 0.12f;
    [SerializeField] private float forwardDepth = 0.02f;
    [SerializeField] private Vector3 manualOffset = Vector3.zero;
    
    [Header("Stabilization")]
    [SerializeField] private float positionDamping = 15f;
    [SerializeField] private float rotationDamping = 12f;
    [SerializeField] private bool useKalmanFilter = true;
    [SerializeField] private float kalmanQ = 0.001f;
    [SerializeField] private float kalmanR = 0.01f;
    
    [Header("Rotation Control")]
    [SerializeField] private bool lockPitch = true;
    [SerializeField] private bool lockRoll = true;
    [SerializeField] private float yawFollowStrength = 0.3f;
    
    [Header("Visual Enhancements")]
    [SerializeField] private bool enableDepthOcclusion = true;
    [SerializeField] private bool enableSoftShadows = true;
    [SerializeField] private float shadowIntensity = 0.6f;
    [SerializeField] private bool enableEnvironmentReflections = true;
    
    // ARCore face vertex indices for neck tracking
    private static readonly int[] NECK_TRACKING_INDICES = new int[]
    {
        152, // Chin center
        175, // Neck left
        400, // Neck right
        148, // Chin left
        377  // Chin right
    };
    
    private ARFace arFace;
    private GameObject necklaceInstance;
    private ARCameraManager arCameraManager;
    
    // Stabilization
    private KalmanFilter positionFilter;
    private Vector3 smoothedPosition;
    private Quaternion smoothedRotation;
    private Vector3 velocity;
    
    // Rendering
    private Renderer necklaceRenderer;
    private Material necklaceMaterial;
    private Light dynamicLight;
    
    // Light estimation
    private float currentBrightness = 1f;
    
    void Awake()
    {
        arFace = GetComponent<ARFace>();
        arCameraManager = FindObjectOfType<ARCameraManager>();
        
        InitializeNecklace();
        SetupFilters();
        ConfigureRendering();
    }
    
    void OnEnable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }
    }
    
    void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
        
        if (necklaceInstance != null)
        {
            necklaceInstance.SetActive(false);
        }
    }
    
    void InitializeNecklace()
    {
        if (necklacePrefab == null)
        {
            Debug.LogError("Necklace prefab not assigned!");
            return;
        }
        
        necklaceInstance = Instantiate(necklacePrefab);
        necklaceInstance.transform.SetParent(null); // Independent from face transform
        necklaceRenderer = necklaceInstance.GetComponentInChildren<Renderer>();
        
        if (necklaceRenderer != null)
        {
            necklaceMaterial = necklaceRenderer.material;
        }
    }
    
    void SetupFilters()
    {
        if (useKalmanFilter)
        {
            positionFilter = new KalmanFilter(kalmanQ, kalmanR);
        }
        
        smoothedPosition = Vector3.zero;
        smoothedRotation = Quaternion.identity;
    }
    
    void ConfigureRendering()
    {
        if (necklaceRenderer == null) return;
        
        // Enable GPU instancing for performance
        necklaceRenderer.shadowCastingMode = enableSoftShadows 
            ? UnityEngine.Rendering.ShadowCastingMode.On 
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        
        necklaceRenderer.receiveShadows = true;
        
        // Setup depth occlusion
        if (enableDepthOcclusion && arCameraManager != null)
        {
            SetupDepthOcclusion();
        }
        
        // Setup PBR material properties
        if (necklaceMaterial != null)
        {
            ConfigurePBRMaterial();
        }
        
        // Add dynamic lighting
        if (enableSoftShadows)
        {
            CreateDynamicLight();
        }
    }
    
    void SetupDepthOcclusion()
    {
        // Enable ARCore depth if available
        var occlusionManager = FindObjectOfType<AROcclusionManager>();
        if (occlusionManager != null)
        {
            occlusionManager.requestedOcclusionPreferenceMode = 
                OcclusionPreferenceMode.PreferEnvironmentOcclusion;
        }
        
        // Set render queue for proper occlusion
        if (necklaceMaterial != null)
        {
            necklaceMaterial.renderQueue = 2001; // After transparent, before overlay
        }
    }
    
    void ConfigurePBRMaterial()
    {
        // Enable realistic lighting
        necklaceMaterial.EnableKeyword("_METALLICGLOSSMAP");
        necklaceMaterial.EnableKeyword("_NORMALMAP");
        
        if (enableEnvironmentReflections)
        {
            necklaceMaterial.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            necklaceMaterial.SetFloat("_GlossyReflections", 1f);
        }
        
        // Set appropriate smoothness for jewelry
        if (necklaceMaterial.HasProperty("_Glossiness"))
        {
            necklaceMaterial.SetFloat("_Glossiness", 0.8f);
        }
        
        if (necklaceMaterial.HasProperty("_Metallic"))
        {
            necklaceMaterial.SetFloat("_Metallic", 0.9f);
        }
    }
    
    void CreateDynamicLight()
    {
        GameObject lightObj = new GameObject("NecklaceLight");
        lightObj.transform.SetParent(necklaceInstance.transform);
        
        dynamicLight = lightObj.AddComponent<Light>();
        dynamicLight.type = LightType.Point;
        dynamicLight.range = 0.5f;
        dynamicLight.intensity = shadowIntensity;
        dynamicLight.shadows = LightShadows.Soft;
        dynamicLight.shadowStrength = 0.4f;
        dynamicLight.color = new Color(1f, 0.95f, 0.9f); // Warm light
        
        lightObj.transform.localPosition = new Vector3(0, 0.1f, 0.1f);
    }
    
    void Update()
    {
        if (necklaceInstance == null || arFace == null) return;
        
        // FIX #1: Check tracking state properly
        if (arFace.trackingState == TrackingState.None || 
            arFace.trackingState == TrackingState.Limited)
        {
            necklaceInstance.SetActive(false);
            return;
        }
        
        necklaceInstance.SetActive(true);
        
        // Calculate optimal neck position using multiple anchor points
        Vector3 targetPosition = CalculateNeckPosition();
        Quaternion targetRotation = CalculateNeckRotation();
        
        // Apply stabilization
        ApplyStabilization(targetPosition, targetRotation);
        
        // Update lighting based on environment
        UpdateDynamicLighting();
    }
    
    Vector3 CalculateNeckPosition()
    {
        if (arFace.vertices.Length == 0) return smoothedPosition;
        
        List<Vector3> neckPoints = new List<Vector3>();
        
        // Gather neck anchor points
        foreach (int index in NECK_TRACKING_INDICES)
        {
            if (index < arFace.vertices.Length)
            {
                Vector3 localPoint = arFace.vertices[index];
                Vector3 worldPoint = arFace.transform.TransformPoint(localPoint);
                neckPoints.Add(worldPoint);
            }
        }
        
        if (neckPoints.Count == 0) return smoothedPosition;
        
        // Calculate centroid of neck points
        Vector3 neckCenter = Vector3.zero;
        foreach (Vector3 point in neckPoints)
        {
            neckCenter += point;
        }
        neckCenter /= neckPoints.Count;
        
        // Apply offsets for natural positioning
        Vector3 downwardOffset = arFace.transform.up * -neckDropDistance;
        Vector3 depthOffset = arFace.transform.forward * forwardDepth;
        
        return neckCenter + downwardOffset + depthOffset + manualOffset;
    }
    
    Quaternion CalculateNeckRotation()
    {
        // Get face rotation
        Vector3 faceEuler = arFace.transform.eulerAngles;
        
        float pitch = lockPitch ? 0 : faceEuler.x;
        float yaw = faceEuler.y;
        float roll = lockRoll ? 0 : faceEuler.z;
        
        // Reduce yaw influence for natural look
        yaw = Mathf.LerpAngle(0, yaw, yawFollowStrength);
        
        return Quaternion.Euler(pitch, yaw, roll);
    }
    
    void ApplyStabilization(Vector3 targetPosition, Quaternion targetRotation)
    {
        // Kalman filtering for position
        if (useKalmanFilter && positionFilter != null)
        {
            targetPosition = positionFilter.Update(targetPosition);
        }
        
        // Smooth damping
        smoothedPosition = Vector3.SmoothDamp(
            smoothedPosition,
            targetPosition,
            ref velocity,
            1f / positionDamping
        );
        
        smoothedRotation = Quaternion.Slerp(
            smoothedRotation,
            targetRotation,
            Time.deltaTime * rotationDamping
        );
        
        // Apply to necklace
        necklaceInstance.transform.position = smoothedPosition;
        necklaceInstance.transform.rotation = smoothedRotation;
    }
    
    // FIX #2: Proper event handler for light estimation
    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        // Update brightness based on AR light estimation
        if (args.lightEstimation.averageBrightness.HasValue)
        {
            currentBrightness = args.lightEstimation.averageBrightness.Value;
        }
        
        // Update color temperature if available
        if (dynamicLight != null && args.lightEstimation.averageColorTemperature.HasValue)
        {
            float colorTemp = args.lightEstimation.averageColorTemperature.Value;
            dynamicLight.color = ColorTemperatureToRGB(colorTemp);
        }
    }
    
    void UpdateDynamicLighting()
    {
        if (dynamicLight == null) return;
        
        // Adjust light intensity based on environment brightness
        dynamicLight.intensity = shadowIntensity * currentBrightness;
    }
    
    // Convert Kelvin temperature to RGB color
    Color ColorTemperatureToRGB(float kelvin)
    {
        // Clamp to reasonable range
        kelvin = Mathf.Clamp(kelvin, 1000f, 40000f) / 100f;
        
        float red, green, blue;
        
        // Calculate red
        if (kelvin <= 66f)
        {
            red = 255f;
        }
        else
        {
            red = kelvin - 60f;
            red = 329.698727446f * Mathf.Pow(red, -0.1332047592f);
            red = Mathf.Clamp(red, 0f, 255f);
        }
        
        // Calculate green
        if (kelvin <= 66f)
        {
            green = kelvin;
            green = 99.4708025861f * Mathf.Log(green) - 161.1195681661f;
        }
        else
        {
            green = kelvin - 60f;
            green = 288.1221695283f * Mathf.Pow(green, -0.0755148492f);
        }
        green = Mathf.Clamp(green, 0f, 255f);
        
        // Calculate blue
        if (kelvin >= 66f)
        {
            blue = 255f;
        }
        else if (kelvin <= 19f)
        {
            blue = 0f;
        }
        else
        {
            blue = kelvin - 10f;
            blue = 138.5177312231f * Mathf.Log(blue) - 305.0447927307f;
            blue = Mathf.Clamp(blue, 0f, 255f);
        }
        
        return new Color(red / 255f, green / 255f, blue / 255f);
    }
    
    void OnDestroy()
    {
        if (necklaceInstance != null)
        {
            Destroy(necklaceInstance);
        }
    }
    
    // Performance optimization
    void OnBecameInvisible()
    {
        enabled = false;
    }
    
    void OnBecameVisible()
    {
        enabled = true;
    }
}

/// <summary>
/// Kalman filter for position stabilization
/// Reduces jitter while maintaining responsiveness
/// </summary>
public class KalmanFilter
{
    private Vector3 estimate;
    private Vector3 errorCovariance;
    private float processNoise;
    private float measurementNoise;
    
    public KalmanFilter(float q, float r)
    {
        processNoise = q;
        measurementNoise = r;
        estimate = Vector3.zero;
        errorCovariance = Vector3.one;
    }
    
    public Vector3 Update(Vector3 measurement)
    {
        // Prediction
        Vector3 predictedError = errorCovariance + Vector3.one * processNoise;
        
        // Update
        Vector3 kalmanGain = new Vector3(
            predictedError.x / (predictedError.x + measurementNoise),
            predictedError.y / (predictedError.y + measurementNoise),
            predictedError.z / (predictedError.z + measurementNoise)
        );
        
        estimate = estimate + Vector3.Scale(kalmanGain, measurement - estimate);
        errorCovariance = Vector3.Scale(Vector3.one - kalmanGain, predictedError);
        
        return estimate;
    }
}