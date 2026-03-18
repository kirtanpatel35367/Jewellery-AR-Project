// LandmarkToWorld_Hand.cs — v6 FINAL
//
// FORMULA DERIVATION (confirmed from video n_003.png):
//
// Observation: Y direction is correct (fingers top, wrist bottom) ✓
//              X direction is MIRRORED (thumb/pinky sides swapped) ✗
//
// Fix: nx = 1 - lm.x  (flip X to un-mirror)
//      ny = lm.y       (keep Y as-is, already correct)
//
// WHY X is mirrored:
//   The 90°CCW rotation in ARCameraImageSource maps landscape→portrait such that
//   the raw camera X axis ends up reversed in portrait X.
//   MediaPipe outputs lm.x increasing left→right in the texture,
//   but that texture left = actual screen RIGHT after the rotation.
//   So we flip: nx = 1 - lm.x.
//
// WHY Y needs no flip:
//   Unity Texture2D stores rows bottom-up (OpenGL convention).
//   MediaPipe lm.y=0 → bottom of texture → wrist area → screen bottom.
//   Unity ScreenToWorldPoint y=0 → screen bottom. Directions match. No flip.

using UnityEngine;

public static class LandmarkToWorld_Hand
{
    private static int _lastTexW, _lastTexH, _lastScrW, _lastScrH;
    private static float _scaleX, _scaleY, _offsetX, _offsetY;

    public static Vector3 Convert(Vector3 lm, Camera cam,
        int texW, int texH, float worldDepth = 0.6f)
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

        // X is mirrored due to 90°CCW rotation → flip it
        // Y is correct (texture bottom-up convention matches screen) → keep it
        float nx = 1f - lm.x;   // un-mirror X
        float ny = lm.y;         // Y already correct, no flip

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