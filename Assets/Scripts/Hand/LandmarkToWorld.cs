// LandmarkToWorld.cs
// CORRECT viewport-aware MediaPipe → Unity world space conversion.
//
// ROOT CAUSE OF ALL PREVIOUS ERRORS:
//   The AR camera image (e.g. 480x640 after 90° rotation) is rendered on the
//   phone screen (1080x2340) using FILL mode — it gets scaled up and CROPPED.
//   Naive mapping (nx * screenWidth) ignores this crop and produces wrong positions.
//
// CORRECT ALGORITHM:
//   1. Compute how ARCameraBackground fills the screen (scale + crop offset)
//   2. Map MediaPipe normalized coords through that exact same transform
//   3. Use ScreenToWorldPoint with the corrected screen position

using UnityEngine;
using UnityEngine.XR.ARFoundation;

public static class LandmarkToWorld
{
    // Cache viewport params — recomputed if texture size changes
    private static int   _lastTexW, _lastTexH, _lastScrW, _lastScrH;
    private static float _scaleX, _scaleY, _offsetX, _offsetY;

    /// <summary>
    /// Convert MediaPipe normalized landmark to Unity world position.
    /// Pass the ARCameraImageSource texture dimensions for correct crop mapping.
    /// </summary>
    /// <param name="lm">Normalized landmark: x,y in [0,1], z is depth hint</param>
    /// <param name="cam">AR camera</param>
    /// <param name="texW">Width of texture sent to MediaPipe (after rotation)</param>
    /// <param name="texH">Height of texture sent to MediaPipe (after rotation)</param>
    /// <param name="depthScale">How much MediaPipe Z affects world depth</param>
    public static Vector3 Convert(Vector3 lm, Camera cam,
        int texW, int texH, float depthScale = 0.15f)
    {
        int scrW = Screen.width;
        int scrH = Screen.height;

        // Recompute viewport mapping if anything changed
        if (texW != _lastTexW || texH != _lastTexH ||
            scrW != _lastScrW || scrH != _lastScrH)
        {
            ComputeViewport(texW, texH, scrW, scrH);
            _lastTexW = texW; _lastTexH = texH;
            _lastScrW = scrW; _lastScrH = scrH;
        }

        // MediaPipe back camera: X is mirrored, Y is flipped relative to screen
        float nx = 1f - lm.x;   // mirror X for back camera
        float ny = 1f - lm.y;   // flip Y (MediaPipe 0=top, Unity screen 0=bottom)

        // Apply viewport transform: scale to rendered image size, then subtract crop
        float sx = nx * _scaleX - _offsetX;
        float sy = ny * _scaleY - _offsetY;

        // Clamp to screen bounds
        sx = Mathf.Clamp(sx, 0, scrW);
        sy = Mathf.Clamp(sy, 0, scrH);

        // Depth: base distance + MediaPipe Z hint
        float depth = Mathf.Clamp(
            cam.nearClipPlane + 0.45f + lm.z * depthScale,
            cam.nearClipPlane + 0.05f, 3.0f);

        return cam.ScreenToWorldPoint(new Vector3(sx, sy, depth));
    }

    /// <summary>Legacy overload — uses screen dimensions directly (less accurate)</summary>
    public static Vector3 Convert(Vector3 lm, Camera cam, float depthScale = 0.15f)
    {
        // Fallback: assume texture fills screen (no crop correction)
        return Convert(lm, cam, Screen.width, Screen.height, depthScale);
    }

    private static void ComputeViewport(int texW, int texH, int scrW, int scrH)
    {
        // ARCameraBackground uses FILL: scale uniformly so image covers entire screen
        float scaleToFill = Mathf.Max((float)scrW / texW, (float)scrH / texH);

        float renderedW = texW * scaleToFill;
        float renderedH = texH * scaleToFill;

        // Crop offset = how many pixels of the rendered image fall outside screen
        _offsetX = (renderedW - scrW) * 0.5f;
        _offsetY = (renderedH - scrH) * 0.5f;
        _scaleX  = renderedW;
        _scaleY  = renderedH;

        Debug.Log($"[LandmarkToWorld] Viewport: tex={texW}x{texH} " +
                  $"screen={scrW}x{scrH} " +
                  $"rendered={renderedW:F0}x{renderedH:F0} " +
                  $"crop=({_offsetX:F1},{_offsetY:F1})");
    }
}
