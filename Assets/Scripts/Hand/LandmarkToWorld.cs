// LandmarkToWorld_Hand.cs — v17  DEFINITIVE LANDMARK Y FIX
//
// ══════════════════════════════════════════════════════════════════════
// HISTORY OF THE bboxYCorrection SIGN BUG:
//
//   Original (pre-v15): ny = lm.y,  formula: sy = ny*scaleY - offset - scrH*correction
//     → positive correction subtracts → dots move DOWN
//     → at that time dots were too HIGH, so this was correct
//
//   v15: ny = 1-lm.y (Y flipped), formula unchanged (still - sign)
//     → With flipped ny, base dot positions moved UP
//     → Now dots were too HIGH again but correction still pushes DOWN
//     → Default 0.07 was roughly calibrated here (moving down 7%)
//
//   v16 (WRONG): changed - to + thinking "positive should mean UP"
//     → This doubled the upward error: dots moved UP by correction instead of down
//     → With default 0.04, dots shifted UP 4% → now 8% too high
//     → User reported "landmarks are more upper"
//
//   v17 (THIS VERSION): REVERT to MINUS sign
//     → Minus sign = positive correction moves dots DOWN
//     → With ny=1-lm.y, dots are shifted UP from baseline
//     → We need to bring them back DOWN by ~5% of screen height
//     → Default bboxYCorrection = 0.05 (measured: dots ~5-8% too high)
//
// CORRECT FORMULA (v17):
//   ny = 1 - lm.y                              ← Y flip (v15, correct)
//   sy = ny * scaleY - offset - scrH * correction  ← MINUS (original sign)
//
// CALIBRATION GUIDE:
//   Start at 0.05. If dots still appear ABOVE joints → INCREASE.
//   If dots appear BELOW joints → DECREASE.
//   Tune in 0.01 steps.
// ══════════════════════════════════════════════════════════════════════

using UnityEngine;

public static class LandmarkToWorld_Hand
{
    private static int _lastTexW, _lastTexH, _lastScrW, _lastScrH;
    private static float _scaleX, _scaleY, _offsetX, _offsetY;

    /// <param name="texW">Raw texture width — no dim swap needed</param>
    /// <param name="texH">Raw texture height — no dim swap needed</param>
    /// <param name="worldDepth">Metres from camera to hand plane</param>
    /// <param name="isBackCamera">Kept for API compatibility only</param>
    /// <param name="bboxYCorrection">
    ///   Positive value shifts dots DOWN by this fraction of screen height.
    ///   With ny=1-lm.y, dots start too high → positive correction brings them down.
    ///   Default 0.05. Increase if dots appear ABOVE joints.
    ///   Decrease (toward 0) if dots appear BELOW joints.
    /// </param>
    public static Vector3 Convert(
        Vector3 lm, Camera cam,
        int texW, int texH,
        float worldDepth = 0.6f,
        bool isBackCamera = false,
        float bboxYCorrection = 0.05f,
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

        // Flip X: corrects mirroring from 90°CW camera rotation
        float nx = 1f - lm.x;
        // Flip Y: MediaPipe Y=0 is image-top; Unity ScreenToWorldPoint Y=0 is screen-bottom
        float ny = 1f - lm.y;

        // MINUS sign: positive bboxYCorrection moves dots DOWN the screen.
        // With ny=1-lm.y the dots start shifted upward from baseline,
        // so we subtract to bring them back to the correct position.
        float sx = nx * _scaleX - _offsetX;
        float sy = ny * _scaleY - _offsetY - scrH * bboxYCorrection;

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
        Debug.Log($"[LandmarkToWorld v17] tex={texW}x{texH} scr={scrW}x{scrH} " +
                  $"fill={fill:F3} rnd={rndW:F0}x{rndH:F0} " +
                  $"off=({_offsetX:F1},{_offsetY:F1})");
    }
}