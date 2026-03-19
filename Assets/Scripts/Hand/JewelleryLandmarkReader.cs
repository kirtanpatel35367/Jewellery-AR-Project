// JewelleryLandmarkReader.cs
// Attach to: JewelleryManager
// Reads hand landmarks from HandLandmarkBroadcaster every frame.

using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class JewelleryLandmarkReader : MonoBehaviour
{
    public bool  HandDetected  => HandLandmarkBroadcaster.HandDetected;
    public bool  IsLeftHand    => HandLandmarkBroadcaster.IsLeftHand;
    public int   LandmarkCount => HandLandmarkBroadcaster.Landmarks?.Count ?? 0;

    public Vector3 GetLandmark(int index)
    {
        var lms = HandLandmarkBroadcaster.Landmarks;
        if (lms == null || index < 0 || index >= lms.Count)
            return Vector3.zero;
        return lms[index];
    }
}
