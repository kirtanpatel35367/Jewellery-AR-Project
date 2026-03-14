using System.Collections;
using UnityEngine;

public class SplashScreenController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject splashPanel;
    public GameObject homePanel;

    [Header("Splash Duration")]
    public float splashDuration = 2f;

    private void Start()
    {
        if (splashPanel != null) splashPanel.SetActive(true);
        if (homePanel != null) homePanel.SetActive(false);

        StartCoroutine(ShowSplashThenHome());
    }

    private IEnumerator ShowSplashThenHome()
    {
        yield return new WaitForSeconds(splashDuration);

        if (splashPanel != null) splashPanel.SetActive(false);
        if (homePanel != null) homePanel.SetActive(true);
    }
}