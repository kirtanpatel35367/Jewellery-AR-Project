using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public GameObject jewelleryPrefab;

    private ARRaycastManager raycastManager;
    private GameObject spawnedObject;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
{
    if (Input.touchCount == 0) return;

    Touch touch = Input.GetTouch(0);

    if (touch.phase != TouchPhase.Began) return;

    Debug.Log("Touch detected");

    if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
    {
        Debug.Log("Raycast HIT plane");

        Pose pose = hits[0].pose;

        if (spawnedObject == null)
        {
            Debug.Log("Spawning object");
            spawnedObject = Instantiate(jewelleryPrefab, pose.position, pose.rotation);
        }
        else
        {
            Debug.Log("Moving object");
            spawnedObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }
    else
    {
        Debug.Log("Raycast MISS");
    }
}
}