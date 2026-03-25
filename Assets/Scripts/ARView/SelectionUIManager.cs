using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectionUIManager : MonoBehaviour
{
    [Header("References")]
    public JewelleryDatabase database;
    public PlacementManager placementManager;

    [Header("UI Panels")]
    public GameObject selectionPanel;   // the full-screen selection overlay
    public GameObject arPanel;          // bottom bar visible during AR

    [Header("Grid")]
    public Transform gridContent;       // Content inside your existing ScrollView
    public GameObject itemCardPrefab;   // prefab with Image + TMP label + Button

    [Header("AR Panel")]
    public Button changeButton;
    public TextMeshProUGUI selectedLabel;

    void Start()
    {
        PopulateGrid();
        ShowSelectionPanel();
        changeButton.onClick.AddListener(ShowSelectionPanel);
    }

    void PopulateGrid()
    {
        foreach (var item in database.items)
        {
            var card = Instantiate(itemCardPrefab, gridContent);
            card.GetComponentInChildren<Image>().sprite = item.thumbnail;
            card.GetComponentInChildren<TextMeshProUGUI>().text = item.itemName;

            var capturedItem = item; // closure capture
            card.GetComponent<Button>().onClick.AddListener(() => OnItemSelected(capturedItem));
        }
    }

    void OnItemSelected(JewelleryItem item)
    {
        placementManager.SetActivePrefab(item.prefab);
        selectedLabel.text = item.itemName;
        ShowARPanel();
    }

    void ShowSelectionPanel()
    {
        selectionPanel.SetActive(true);
        arPanel.SetActive(false);
    }

    void ShowARPanel()
    {
        selectionPanel.SetActive(false);
        arPanel.SetActive(true);
    }
}