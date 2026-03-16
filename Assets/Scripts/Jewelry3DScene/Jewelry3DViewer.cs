using UnityEngine;

public class Jewelry3DViewer : MonoBehaviour
{
    public Transform spawnPoint;
    public GameObject[] jewelryPrefabs;

    private GameObject currentModel;

    public void ShowJewelry(int index)
    {
        Debug.Log("Button clicked. Index = " + index);

        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        // Spawn model as child of spawn point
        currentModel = Instantiate(jewelryPrefabs[index], spawnPoint);

        currentModel.transform.localRotation = Quaternion.identity;
        currentModel.transform.localScale = Vector3.one;

        // CENTER MODEL AUTOMATICALLY
        Renderer r = currentModel.GetComponentInChildren<Renderer>();

        if (r != null)
        {
            Vector3 centerOffset = r.bounds.center - spawnPoint.position;
            currentModel.transform.position -= centerOffset;
        }

        Debug.Log("Spawned model name: " + currentModel.name);
    }
}