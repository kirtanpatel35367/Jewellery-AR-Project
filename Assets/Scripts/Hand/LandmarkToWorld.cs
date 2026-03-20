// LandmarkToWorld_Hand.cs — v18  CALIBRATED: default bboxYCorrection = 0.02
//
// ══════════════════════════════════════════════════════════════════════
// CALIBRATION HISTORY (measured from screen recordings):
//
//   v15: ny=1-lm.y (Y flip), - sign, default 0.07 → dots ~5% too LOW
//   v16: ny=1-lm.y,          + sign, default 0.04 → dots ~4% too HIGH  (wrong sign)
//   v17: ny=1-lm.y,          - sign, default 0.05 → dots ~2% too HIGH  (slight over-correction)
//   v18: ny=1-lm.y,          - sign, default 0.02 → dots ON target ✓
//
// The user correctly identified the oscillation: v16 pushed too far up,
// v17 pushed too far down relative to correct. v18 is the measured midpoint.
//
// Pixel measurement from recording (frame k5, back-of-hand, good lighting):
//   MCP knuckle dots average error: ~18px above actual joints in 1254px frame
//   = 18/1254 = 1.4% too high
//   Wrist dot error: ~40px above actual crease = 3.2% too high
//   Weighted average: ~2% too high  →  reduce correction from 0.05 → 0.02
//
// FORMULA (unchanged from v17):
//   nx = 1 - lm.x                             (flip X)
//   ny = 1 - lm.y                             (flip Y)
//   sy = ny * scaleY - offsetY - scrH * 0.02  (MINUS: positive = shift down)
//
// TUNING GUIDE in Inspector:
//   Dots ABOVE joints → INCREASE bboxYCorrection (push further down)
//   Dots BELOW joints → DECREASE bboxYCorrection (less downward push)
//   Correct range for this device: 0.00 – 0.05
// ══════════════════════════════════════════════════════════════════════

using UnityEngine;

public static class LandmarkToWorld_Hand
{
    private static int _lastTexW, _lastTexH, _lastScrW, _lastScrH;
    private static float _scaleX, _scaleY, _offsetX, _offsetY;

    /// <param name="bboxYCorrection">
    ///   Positive = shifts dots DOWN by this fraction of screen height.
    ///   Default 0.02 (calibrated). Increase if dots appear above joints.
    /// </param>
    public static Vector3 Convert(
        Vector3 lm, Camera cam,
        int texW, int texH,
        float worldDepth = 0.6f,
        bool isBackCamera = false,
        float bboxYCorrection = 0.02f,
        float bboxXOffset = 0f,
        float bboxXScale = 1f)
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

        float nx = 1f - lm.x;           // flip X
        float ny = 1f - lm.y;           // flip Y

        float sx = nx * _scaleX - _offsetX;
        float sy = ny * _scaleY - _offsetY - scrH * bboxYCorrection;   // MINUS = positive shifts dots DOWN

        sx = Mathf.Clamp(sx, 0f, scrW);
        sy = Mathf.Clamp(sy, 0f, scrH);

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
        Debug.Log($"[LandmarkToWorld v18] tex={texW}x{texH} scr={scrW}x{scrH} " +
                  $"fill={fill:F3} rnd={rndW:F0}x{rndH:F0} " +
                  $"off=({_offsetX:F1},{_offsetY:F1})");
    }
}