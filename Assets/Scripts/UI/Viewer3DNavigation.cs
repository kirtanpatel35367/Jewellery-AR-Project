using UnityEngine;

public class Viewer3DNavigation : MonoBehaviour
{
    public GameObject productPanel;
    public GameObject viewer3DPanel;
    public GameObject liveViewPanel;
    public GameObject arPlacementPanel;

    public void GoBack()
    {
        Debug.Log("GoBack called");

        if (viewer3DPanel != null)
            viewer3DPanel.SetActive(false);

        if (productPanel != null)
            productPanel.SetActive(true);
    }

    public void OpenLiveAR()
    {
        Debug.Log("OpenLiveAR called");

        if (viewer3DPanel != null)
            viewer3DPanel.SetActive(false);

        if (liveViewPanel != null)
            liveViewPanel.SetActive(true);
    }

    public void OpenARPlacement()
    {
        Debug.Log("OpenARPlacement called");

        if (viewer3DPanel != null)
            viewer3DPanel.SetActive(false);

        if (arPlacementPanel != null)
            arPlacementPanel.SetActive(true);
    }
}