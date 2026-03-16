// using UnityEngine;
// using TMPro;
// using UnityEngine.UI;

// public class ProductPanelLoader : MonoBehaviour
// {
//     [Header("UI References")]
//     public Transform productGrid;
//     public GameObject productCardPrefab;     
//     public TextMeshProUGUI titleText;

//     private void OnEnable()
//     {
//         LoadProducts();
//     }

//     public void LoadProducts()
//     {
//         if (SelectedCategoryStore.Instance == null)
//         {
//             Debug.LogWarning("SelectedCategoryStore instance not found.");
//             return;
//         }

//         string category = SelectedCategoryStore.Instance.selectedCategory;

//         if (string.IsNullOrEmpty(category))
//         {
//             Debug.LogWarning("No category selected.");
//             return;
//         }

//         if (titleText != null)
// {
//     titleText.text = category;
//     Debug.Log("Title updated to: " + titleText.text);
// }

//         ClearGrid();

//         Sprite[] thumbnails = Resources.LoadAll<Sprite>("Thumbnails/" + category);

//         if (thumbnails == null || thumbnails.Length == 0)
//         {
//             Debug.LogWarning("No thumbnails found in Resources/Thumbnails/" + category);
//             return;
//         }

//         foreach (Sprite thumb in thumbnails)
//         {
//             GameObject card = Instantiate(productCardPrefab, productGrid);

//             Transform imageTf = card.transform.Find("ProductImage");
//             Transform nameTf = card.transform.Find("ProductName");

//             if (imageTf != null)
//             {
//                 Image img = imageTf.GetComponent<Image>();
//                 if (img != null)
//                 {
//                     img.sprite = thumb;
//                     img.preserveAspect = true;
//                 }
//             }

//             if (nameTf != null)
//             {
//                 TextMeshProUGUI txt = nameTf.GetComponent<TextMeshProUGUI>();
//                 if (txt != null)
//                 {
//                     txt.text = thumb.name;
//                 }
//             }
//         }
//     }

//     private void ClearGrid()
//     {
//         for (int i = productGrid.childCount - 1; i >= 0; i--)
//         {
//             Destroy(productGrid.GetChild(i).gameObject);
//         }
//     }
// }
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ProductPanelLoader : MonoBehaviour
{
    [Header("UI References")]
    public Transform productGrid;
    public GameObject productCardPrefab;
    public TextMeshProUGUI titleText;

    [Header("Panels")]
    public GameObject productPanel;
    public GameObject viewer3DPanel;

    private void OnEnable()
    {
        LoadProducts();
    }

    public void LoadProducts()
    {
        if (SelectedCategoryStore.Instance == null)
        {
            Debug.LogWarning("SelectedCategoryStore instance not found.");
            return;
        }

        string category = SelectedCategoryStore.Instance.selectedCategory;

        if (string.IsNullOrEmpty(category))
        {
            Debug.LogWarning("No category selected.");
            return;
        }

        category = category.Trim();

        if (titleText != null)
            titleText.text = category;

        ClearGrid();

        string path = "Thumbnails/" + category;
        Sprite[] thumbnails = Resources.LoadAll<Sprite>(path);

        Debug.Log("Loading thumbnails from Resources/" + path);
        Debug.Log("Found thumbnails count = " + thumbnails.Length);

        if (thumbnails == null || thumbnails.Length == 0)
        {
            Debug.LogWarning("No thumbnails found in Resources/" + path);
            return;
        }

        foreach (Sprite thumb in thumbnails)
        {
            GameObject card = Instantiate(productCardPrefab, productGrid);
            card.name = "Card_" + thumb.name;

            ProductCardItem cardItem = card.GetComponent<ProductCardItem>();
            if (cardItem == null)
            {
                cardItem = card.AddComponent<ProductCardItem>();
            }

            cardItem.Setup(thumb, productPanel, viewer3DPanel);

            Button btn = card.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(cardItem.OnCardClicked);
            }
            else
            {
                Debug.LogError("Button missing on ProductCard prefab root.");
            }
        }
    }

    private void ClearGrid()
    {
        if (productGrid == null)
            return;

        for (int i = productGrid.childCount - 1; i >= 0; i--)
        {
            Destroy(productGrid.GetChild(i).gameObject);
        }
    }
}