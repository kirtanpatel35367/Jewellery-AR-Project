using UnityEngine;
using UnityEngine.SceneManagement;

public class ARModeMenu : MonoBehaviour
{
    public void Open3DView()
    {
        SceneManager.LoadScene("Jewelry3DScene");
    }

    public void OpenRealWorldView()
    {
        SceneManager.LoadScene("RealWorldScene");
    }

    public void OpenHumanView()
    {
        SceneManager.LoadScene("FaceTryOn");
    }
}