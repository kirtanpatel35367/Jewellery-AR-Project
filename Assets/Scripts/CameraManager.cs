using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;

/// <summary>
/// CAMERA MANAGER v4
///
/// ROOT CAUSE OF HANG:
///   Calling handTrackingSystem.SetActive(true/false) causes Unity to run
///   Start()/OnEnable()/OnDisable() on every script inside that GameObject,
///   including ARHandTracker which tries to acquire XR subsystems.
///   On ARCore, toggling a tracked system's active state while the AR session
///   is running causes a deadlock in the native AR thread.
///
/// FIX:
///   NEVER call SetActive on the tracking systems at runtime.
///   Both systems stay active at all times.
///   We only switch the ARCameraManager.requestedFacingDirection.
///   The tracking scripts handle their own state based on what the camera sees.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("AR Camera (auto-found if left empty)")]
    public ARCameraManager arCameraManager;

    // These are kept for legacy Inspector compatibility but NOT toggled at runtime
    [Header("Tracking Systems (DO NOT need to be toggled)")]
    public GameObject faceTrackingSystem;
    public GameObject handTrackingSystem;

    public enum CameraMode { Face, Hand }
    public CameraMode currentMode = CameraMode.Face;

    void Start()
    {
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();

        // Just set facing direction — no SetActive calls
        ApplyDirection(CameraFacingDirection.User);
        currentMode = CameraMode.Face;
        Debug.Log("[CameraManager] Started in Face mode.");
    }

    // ── Public API ──────────────────────────────────────────────────────

    public void SwitchForJewelryType(JewelryType type)
    {
        switch (type)
        {
            case JewelryType.Earrings:
            case JewelryType.Necklace:
                SwitchToFaceMode();
                break;
            case JewelryType.Bracelet:
            case JewelryType.Ring:
            case JewelryType.Bangle:
                SwitchToHandMode();
                break;
        }
    }

    public void SwitchToFaceMode()
    {
        if (currentMode == CameraMode.Face) return;
        currentMode = CameraMode.Face;
        ApplyDirection(CameraFacingDirection.User);
        Debug.Log("[CameraManager] Switched to FACE (front camera)");
    }

    public void SwitchToHandMode()
    {
        if (currentMode == CameraMode.Hand) return;
        currentMode = CameraMode.Hand;
        ApplyDirection(CameraFacingDirection.World);
        Debug.Log("[CameraManager] Switched to HAND (back camera)");
    }

    public bool IsFaceMode() => currentMode == CameraMode.Face;
    public bool IsHandMode() => currentMode == CameraMode.Hand;

    // ── Internal ────────────────────────────────────────────────────────

    void ApplyDirection(CameraFacingDirection dir)
    {
        if (arCameraManager != null)
            arCameraManager.requestedFacingDirection = dir;
        else
            Debug.LogWarning("[CameraManager] ARCameraManager not found!");
    }
}