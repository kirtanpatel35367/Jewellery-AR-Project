// LandmarkToWorld_Hand.cs — v7 CAMERA-AWARE LANDMARK MAPPING
//
// ROOT CAUSE OF MIRRORED/REVERSED LANDMARKS ON BACK CAMERA:
//
//   The back (World-facing) camera captures in landscape, then ARFoundation
//   rotates the texture 90°CW (not CCW) to produce portrait orientation.
//   This is the OPPOSITE rotation from the front camera's 90°CCW.
//
//   Effect on X:
//     Front camera: texture left → screen RIGHT  → needs flip:  nx = 1 - lm.x  ✓
//     Back  camera: texture left → screen LEFT   → no flip:     nx = lm.x       ✓
//
//   Effect on Y:
//     Front camera: texture bottom (lm.y=0) → screen bottom → Y correct, no flip ✓
//     Back  camera: texture bottom (lm.y=0) → screen TOP    → Y must be flipped:
//                   ny = 1 - lm.y                                                 ✓
//
// SUMMARY TABLE:
//   Camera   | nx              | ny
//   ---------+-----------------+------------------
//   Front    | 1 - lm.x        | lm.y
//   Back     | lm.x            | 1 - lm.y
//
// HOW TO USE:
//   Pass isBackCamera = true when the ARCameraManager is set to World-facing.
//   BanglePlacer and RingPlacer read this flag from ARCameraManager each frame.

using UnityEngine;

public static class LandmarkToWorld_Hand
{
    private static int _lastTexW, _lastTexH, _lastScrW, _lastScrH;
    private static float _scaleX, _scaleY, _offsetX, _offsetY;

    /// <summary>
    /// Convert a MediaPipe normalized landmark (0–1 range) to world space.
    /// </summary>
    /// <param name="lm">Raw MediaPipe landmark (x,y in 0–1, z ignored here)</param>
    /// <param name="cam">The AR camera</param>
    /// <param name="texW">Camera texture width</param>
    /// <param name="texH">Camera texture height</param>
    /// <param name="worldDepth">Depth from camera in metres</param>
    /// <param name="isBackCamera">True when ARCameraManager is World-facing (back camera)</param>
    public static Vector3 Convert(Vector3 lm, Camera cam,
        int texW, int texH, float worldDepth = 0.6f, bool isBackCamera = false)
    {
        int scrW = Screen.width;
        int scrH = Screen.height;

        if (texW != _lastTexW || texH != _lastTexH ||
            scrW != _lastScrW || scrH != _lastScrH)
        {
            ComputeViewport(texW, texH, scrW, scrH);
            _lastTexW = texW; _lastTexH = texH;
            _lastScrW = scrW; _lastScrH = scrH;
        }

        float nx, ny;

        if (isBackCamera)
        {
            // Back camera: 90°CW rotation
            //   X is NOT mirrored → keep as-is
            //   Y IS flipped       → flip it
            nx = lm.x;
            ny = 1f - lm.y;
        }
        else
        {
            // Front camera: 90°CCW rotation
            //   X IS mirrored → flip it
            //   Y is correct  → keep as-is
            nx = 1f - lm.x;
            ny = lm.y;
        }

        float sx = nx * _scaleX - _offsetX;
        float sy = ny * _scaleY - _offsetY;

        sx = Mathf.Clamp(sx, 0, scrW);
        sy = Mathf.Clamp(sy, 0, scrH);

        float depth = Mathf.Clamp(worldDepth, cam.nearClipPlane + 0.05f, 3f);
        return cam.ScreenToWorldPoint(new Vector3(sx, sy, depth));
    }

    private static void ComputeViewport(int texW, int texH, int scrW, int scrH)
    {
        float fill = Mathf.Max((float)scrW / texW, (float)scrH / texH);
        float rndW = texW * fill;
        float rndH = texH * fill;
        _offsetX = (rndW - scrW) * 0.5f;
        _offsetY = (rndH - scrH) * 0.5f;
        _scaleX = rndW;
        _scaleY = rndH;

        Debug.Log($"[LandmarkToWorld] tex={texW}x{texH} screen={scrW}x{scrH} " +
                  $"rendered={rndW:F0}x{rndH:F0} offset=({_offsetX:F1},{_offsetY:F1})");
    }
}