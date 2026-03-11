using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARJewelryLighting : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public Light directionalLight;

    void OnEnable()
    {
        cameraManager.frameReceived += OnFrame;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnFrame;
    }

    void OnFrame(ARCameraFrameEventArgs args)
    {
        if (args.lightEstimation.mainLightDirection.HasValue)
            directionalLight.transform.rotation =
                Quaternion.LookRotation(args.lightEstimation.mainLightDirection.Value);

        if (args.lightEstimation.mainLightColor.HasValue)
            directionalLight.color = args.lightEstimation.mainLightColor.Value;
    }
}