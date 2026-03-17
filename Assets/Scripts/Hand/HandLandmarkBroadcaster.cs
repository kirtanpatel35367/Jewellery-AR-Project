// HandLandmarkBroadcaster.cs
// Static channel — receives results from HandLandmarkerRunner
// and makes them available to JewelleryLandmarkReader.
// Place in: Assets/Scripts/

using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    public static class HandLandmarkBroadcaster
    {
        public static List<Vector3> Landmarks    { get; private set; } = new List<Vector3>();
        public static bool          HandDetected { get; private set; } = false;
        public static bool          IsLeftHand   { get; private set; } = false;

        public static void Broadcast(HandLandmarkerResult result)
        {
            if (result.handLandmarks == null || result.handLandmarks.Count == 0)
            {
                HandDetected = false;
                Landmarks.Clear();
                return;
            }

            HandDetected = true;

            // Determine handedness
            if (result.handedness != null && result.handedness.Count > 0)
            {
                var categories = result.handedness[0].categories;
                if (categories != null && categories.Count > 0)
                    IsLeftHand = categories[0].categoryName == "Left";
            }

            // Store landmarks
            var lms = result.handLandmarks[0];
            Landmarks.Clear();
            foreach (var lm in lms.landmarks)
                Landmarks.Add(new Vector3(lm.x, lm.y, lm.z));
        }

        public static void Clear()
        {
            HandDetected = false;
            Landmarks.Clear();
        }
    }
}
