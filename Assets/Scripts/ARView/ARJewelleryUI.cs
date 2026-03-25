using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ARJewelleryUI : MonoBehaviour
{
   public JewelleryDatabase database;
public PlacementManager placementManager;

    [Header("Panels")]
    public GameObject selectionPanel;       // drag SelectionPanel GO
    public GameObject arPanel;              // drag your BottomPanel GO

    [Header("Grid")]
    public Transform gridContent;          // drag Content child of ScrollView
    public GameObject itemCardPrefab;      // drag your card prefab asset

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

        // Thumbnail
        card.GetComponentInChildren<Image>().sprite = item.thumbnail;

        // Name
        card.GetComponentInChildren<TextMeshProUGUI>().text = item.itemName;

        var capturedItem = item;

        // Button click
        card.GetComponent<Button>().onClick.AddListener(() =>
        {
            placementManager.SetActivePrefab(capturedItem.prefab);
            selectedLabel.text = capturedItem.itemName;
            ShowARPanel();
        });
    }
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