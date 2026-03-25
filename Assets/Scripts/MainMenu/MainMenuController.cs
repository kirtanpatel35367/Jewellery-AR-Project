using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button btnRealHumanView;
    public Button btn3DView;
    public Button btnRealWorldView;

    [Header("Scene Names")]
    public string realHumanViewScene  = "FaceTryOn";
    public string view3DScene         = "Jewelery3DScene";
    public string realWorldViewScene  = "JewelryARScene";

    [Header("Fade")]
    public CanvasGroup screenFade;

    [Header("Button Animations (CanvasGroups)")]
    public CanvasGroup[] buttonGroups; // assign all 3 button CanvasGroups

    void Start()
    {
        btnRealHumanView.onClick.AddListener(() => LoadScene(realHumanViewScene));
        btn3DView.onClick.AddListener(()        => LoadScene(view3DScene));
        btnRealWorldView.onClick.AddListener(() => LoadScene(realWorldViewScene));

        StartCoroutine(AnimateButtonsIn());
    }

    void LoadScene(string sceneName)
    {
        StartCoroutine(TransitionToScene(sceneName));
    }

    IEnumerator TransitionToScene(string sceneName)
    {
        if (screenFade != null)
        {
            screenFade.gameObject.SetActive(true);
            yield return FadeCanvasGroup(screenFade, 0f, 1f, 0.3f);
        }
        else
        {
            yield return new WaitForSeconds(0.15f);
        }

        SceneManager.LoadScene(sceneName);
    }

    IEnumerator AnimateButtonsIn()
    {
        foreach (var cg in buttonGroups)
            cg.alpha = 0f;

        foreach (var cg in buttonGroups)
        {
            StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, 0.4f));
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        cg.alpha = from;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            cg.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cg.alpha = to;
    }
}