// ARCameraImageSource.cs — FINAL
// Converts AR camera frames for MediaPipe with correct 90° CW rotation.
// Android back camera delivers landscape frames — we rotate to portrait
// so MediaPipe sees the image correctly oriented.

using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;

namespace Mediapipe.Unity
{
    public class ARCameraImageSource : ImageSource
    {
        public ARCameraManager arCameraManager;

        private Texture2D _rawTex;
        private Texture2D _rotTex;
        private bool _playing;
        private bool _acceptFrames;
        private long _lastTimestampUs = -1;

        public override string sourceName => "AR Camera";
        public override string[] sourceCandidateNames => new[] { "AR Camera" };
        public override ResolutionStruct[] availableResolutions =>
            new[] { new ResolutionStruct(resolution.width, resolution.height, 30) };
        public override bool isPrepared  => _rotTex != null;
        public override bool isPlaying   => _playing;
        public override int  textureWidth  => _rotTex != null ? _rotTex.width  : resolution.width;
        public override int  textureHeight => _rotTex != null ? _rotTex.height : resolution.height;
        public override bool isFrontFacing       => false;
        public override bool isVerticallyFlipped => false;

        public override void SelectSource(int sourceId) { }

        public override IEnumerator Play()
        {
            _acceptFrames = false;
            if (arCameraManager == null)
            {
                Debug.LogError("[ARCameraImageSource] arCameraManager not assigned!");
                yield break;
            }
            Debug.Log("[ARCameraImageSource] Waiting for first AR frame...");
            yield return new WaitUntil(() => _rotTex != null && _rotTex.width > 16);
            resolution = new ResolutionStruct(_rotTex.width, _rotTex.height, 30);
            _playing = true;
            _acceptFrames = true;
            _lastTimestampUs = -1;
            Debug.Log($"[ARCameraImageSource] Ready — {_rotTex.width}x{_rotTex.height}");
        }

        public override IEnumerator Resume()
        {
            _playing = true;
            _acceptFrames = true;
            _lastTimestampUs = -1;
            yield return null;
        }

        public override void Pause() { _playing = false; _acceptFrames = false; }

        public override void Stop()
        {
            _playing = false;
            _acceptFrames = false;
            _lastTimestampUs = -1;
        }

        public override Texture GetCurrentTexture() => _rotTex;

        public void StartCapture()
        {
            if (arCameraManager != null)
                arCameraManager.frameReceived += OnFrame;
        }

        public void StopCapture()
        {
            if (arCameraManager != null)
                arCameraManager.frameReceived -= OnFrame;
            _acceptFrames = false;
        }

        private void OnFrame(ARCameraFrameEventArgs args)
        {
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            try
            {
                int rawW = image.width;
                int rawH = image.height;

                if (_rawTex == null || _rawTex.width != rawW || _rawTex.height != rawH)
                    _rawTex = new Texture2D(rawW, rawH, TextureFormat.RGBA32, false);

                var conv = new XRCpuImage.ConversionParams(
                    image, TextureFormat.RGBA32, XRCpuImage.Transformation.None);

                int size   = image.GetConvertedDataSize(conv);
                var buffer = new NativeArray<byte>(size, Allocator.Temp);
                image.Convert(conv, buffer);
                _rawTex.LoadRawTextureData(buffer);
                _rawTex.Apply();
                buffer.Dispose();

                // Rotate 90° CW: landscape (rawW x rawH) → portrait (rawH x rawW)
                int rotW = rawH;
                int rotH = rawW;

                if (_rotTex == null || _rotTex.width != rotW || _rotTex.height != rotH)
                    _rotTex = new Texture2D(rotW, rotH, TextureFormat.RGBA32, false);

                Color32[] src = _rawTex.GetPixels32();
                Color32[] dst = new Color32[rotW * rotH];

                for (int y = 0; y < rotH; y++)
                {
                    for (int x = 0; x < rotW; x++)
                    {
                        // 90° CW: dst(x,y) = src(y, rawH-1-x)
                        int srcX = rawW - 1 - y;
                        int srcY = x;
                        dst[y * rotW + x] = src[srcY * rawW + srcX];
                    }
                }

                _rotTex.SetPixels32(dst);
                _rotTex.Apply();

                // Strictly increasing timestamp
                long nowUs;
                if (args.timestampNs.HasValue)
                    nowUs = args.timestampNs.Value / 1000L;
                else
                    nowUs = (long)(Time.realtimeSinceStartup * 1_000_000.0);

                if (nowUs <= _lastTimestampUs)
                    nowUs = _lastTimestampUs + 1;
                _lastTimestampUs = nowUs;
            }
            finally
            {
                image.Dispose();
            }
        }
    }
}
