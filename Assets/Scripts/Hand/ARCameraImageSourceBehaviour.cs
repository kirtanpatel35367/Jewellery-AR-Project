// ARCameraImageSourceBehaviour.cs
// Attach to: JewelleryManager
// Script Execution Order: -100

using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

public class ARCameraImageSourceBehaviour : MonoBehaviour
{
    [Header("AR Camera")]
    public ARCameraManager arCameraManager;

    private ARCameraImageSource _arImageSource;

    // Expose source so BanglePlacer can read texture dimensions
    public ARCameraImageSource GetImageSource() => _arImageSource;

    void Awake()
    {
        if (arCameraManager == null)
        {
            Debug.LogError("[ARCameraImageSourceBehaviour] arCameraManager not assigned!");
            return;
        }

        _arImageSource = new ARCameraImageSource
        {
            arCameraManager = this.arCameraManager
        };

        InjectImageSource();
        StartCoroutine(InjectAfterBootstrap());
    }

    private IEnumerator InjectAfterBootstrap()
    {
        yield return null;
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.5f);
        InjectImageSource();
        Debug.Log("[ARCameraImageSourceBehaviour] Re-injected AR ImageSource after Bootstrap.");
    }

    private void InjectImageSource()
    {
        var prop = typeof(ImageSourceProvider).GetProperty(
            "ImageSource",
            BindingFlags.Public | BindingFlags.Static);

        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(null, _arImageSource);
            Debug.Log("[ARCameraImageSourceBehaviour] AR ImageSource injected via property.");
            return;
        }

        var field = typeof(ImageSourceProvider).GetField(
            "<ImageSource>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (field != null)
        {
            field.SetValue(null, _arImageSource);
            Debug.Log("[ARCameraImageSourceBehaviour] AR ImageSource injected via backing field.");
        }
        else
        {
            Debug.LogError("[ARCameraImageSourceBehaviour] Could not inject ImageSource!");
        }
    }

    void OnEnable()
    {
        StartCoroutine(StartCaptureWhenReady());
    }

    private IEnumerator StartCaptureWhenReady()
    {
        yield return new WaitUntil(() => _arImageSource != null);
        _arImageSource.StartCapture();
        Debug.Log("[ARCameraImageSourceBehaviour] Capture started.");
    }

    void OnDisable()
    {
        _arImageSource?.StopCapture();
        Debug.Log("[ARCameraImageSourceBehaviour] Capture stopped.");
    }
}
