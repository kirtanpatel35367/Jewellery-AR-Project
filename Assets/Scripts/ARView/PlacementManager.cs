using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    // Removed: public GameObject jewelleryPrefab (single prefab)
    // Now set dynamically from SelectionUIManager
    private GameObject activePrefab;
    private ARRaycastManager raycastManager;
    private GameObject spawnedObject;
    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
    }

    // Called by SelectionUIManager when the user picks an item
    public void SetActivePrefab(GameObject prefab)
    {
        activePrefab = prefab;

        // Destroy previously placed object so the new selection spawns fresh
        if (spawnedObject != null)
        {
            Destroy(spawnedObject);
            spawnedObject = null;
        }
    }

    void Update()
    {
        // Don't raycast if no prefab is selected yet
        if (activePrefab == null) return;
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return;

        if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose pose = hits[0].pose;

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(activePrefab, pose.position, pose.rotation);
            }
            else
            {
                spawnedObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }
    }
}
