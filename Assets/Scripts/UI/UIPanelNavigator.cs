using UnityEngine;

public class UIPanelNavigator : MonoBehaviour
{
    [Header("Panels")]
    public GameObject homePanel;
    public GameObject productPanel;

    public void OpenProductPanel()
    {
        if (homePanel != null) homePanel.SetActive(false);
        if (productPanel != null) productPanel.SetActive(true);
    }

    public void OpenHomePanel()
    {
        if (productPanel != null) productPanel.SetActive(false);
        if (homePanel != null) homePanel.SetActive(true);
    }

    public void SelectCategoryAndOpen(string categoryName)
    {
        if (SelectedCategoryStore.Instance != null)
            SelectedCategoryStore.Instance.SetCategory(categoryName);

        OpenProductPanel();
    }
}