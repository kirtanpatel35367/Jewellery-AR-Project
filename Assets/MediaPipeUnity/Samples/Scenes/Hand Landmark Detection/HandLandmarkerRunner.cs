// Copyright (c) 2023 homuler
// Modified for AR Jewellery try-on project.
// Changes:
//   1. Forces CPU delegate
//   2. Waits for ARCameraImageSource to be injected before proceeding
//   3. Skips screen.Initialize() when screen is null (Annotatable Screen disabled)
//   4. Broadcasts hand landmark results via HandLandmarkBroadcaster

using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    public class HandLandmarkerRunner : VisionTaskApiRunner<HandLandmarker>
    {
        [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;

        private Experimental.TextureFramePool _textureFramePool;

        public readonly HandLandmarkDetectionConfig config = new HandLandmarkDetectionConfig();

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;
        }

        protected override IEnumerator Run()
        {
            // Force CPU — prevent GL_INVALID_ENUM crash on Android
            config.Delegate = Tasks.Core.BaseOptions.Delegate.CPU;
            config.ImageReadMode = ImageReadMode.CPU;

            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Image Read Mode = {config.ImageReadMode}");
            Debug.Log($"Running Mode = {config.RunningMode}");
            Debug.Log($"NumHands = {config.NumHands}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            var options = config.GetHandLandmarkerOptions(
              config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM
                ? OnHandLandmarkDetectionOutput
                : null);

            taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

            // ── Wait until ARCameraImageSource is injected ──────────────────
            // ARCameraImageSourceBehaviour re-injects after 0.5s delay.
            // We must NOT call Play() on StaticImageSource — it crashes.
            Debug.Log("[HandLandmarkerRunner] Waiting for ARCameraImageSource...");
            yield return new WaitUntil(() =>
              ImageSourceProvider.ImageSource is ARCameraImageSource);
            Debug.Log("[HandLandmarkerRunner] ARCameraImageSource ready — proceeding.");

            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            _textureFramePool = new Experimental.TextureFramePool(
              imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            // Only initialize screen if it exists (Annotatable Screen may be disabled)
            if (screen != null && _handLandmarkerResultAnnotationController != null)
            {
                screen.Initialize(imageSource);
                SetupAnnotationController(_handLandmarkerResultAnnotationController, imageSource);
            }
            else
            {
                Debug.Log("[HandLandmarkerRunner] No screen — skipping screen/annotation init.");
            }

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;
            var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(
              rotationDegrees: (int)transformationOptions.rotationAngle);

            var waitForEndOfFrame = new WaitForEndOfFrame();
            var result = HandLandmarkerResult.Alloc(options.numHands);

            Debug.Log("[HandLandmarkerRunner] Entering detection loop.");

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(() => isPaused);
                }

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                // CPU path only
                yield return waitForEndOfFrame;
                textureFrame.ReadTextureOnCPU(
                  imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                var image = textureFrame.BuildCPUImage();
                textureFrame.Release();

                switch (taskApi.runningMode)
                {
                    case Tasks.Vision.Core.RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
                        {
                            if (_handLandmarkerResultAnnotationController != null)
                                _handLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            if (_handLandmarkerResultAnnotationController != null)
                                _handLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;

                    case Tasks.Vision.Core.RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(),
                            imageProcessingOptions, ref result))
                        {
                            if (_handLandmarkerResultAnnotationController != null)
                                _handLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            if (_handLandmarkerResultAnnotationController != null)
                                _handLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;

                    case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
                        taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                        break;
                }
            }
        }

        private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
        {
            // Broadcast landmarks for bangle placement
            HandLandmarkBroadcaster.Broadcast(result);

            if (_handLandmarkerResultAnnotationController != null)
                _handLandmarkerResultAnnotationController.DrawLater(result);
        }
    }
}
