using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProductCardItem : MonoBehaviour
{
    private Image productImage;
    private TextMeshProUGUI productNameText;

    private string productName;
    private GameObject productPanel;
    private GameObject viewer3DPanel;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        Transform imageTf = transform.Find("ProductImage");
        Transform nameTf = transform.Find("ProductName");

        if (imageTf != null)
            productImage = imageTf.GetComponent<Image>();

        if (nameTf != null)
            productNameText = nameTf.GetComponent<TextMeshProUGUI>();
    }

    public void Setup(Sprite thumb, GameObject productPanelRef, GameObject viewer3DPanelRef)
    {
        if (productImage == null || productNameText == null)
            AutoFindReferences();

        productName = thumb.name;
        productPanel = productPanelRef;
        viewer3DPanel = viewer3DPanelRef;

        if (productImage != null)
        {
            productImage.sprite = thumb;
            productImage.preserveAspect = true;
        }
        else
        {
            Debug.LogError("ProductImage child or Image component not found on " + gameObject.name);
        }

        if (productNameText != null)
        {
            productNameText.text = thumb.name.Replace("_", " ");
        }
        else
        {
            Debug.LogError("ProductName child or TMP component not found on " + gameObject.name);
        }
    }

    // public void OnCardClicked()
    // {
    //     Debug.LogError("CARD CLICKED = " + productName);

    //     if (SelectedProductStore.Instance != null)
    //     {
    //         SelectedProductStore.Instance.SetProduct(productName);
    //         Debug.LogError("STORE NOW = " + SelectedProductStore.Instance.selectedProductName);
    //     }
    //     else
    //     {
    //         Debug.LogError("SelectedProductStore.Instance is NULL");
    //     }

    //     if (productPanel != null)
    //         productPanel.SetActive(false);

    //     if (viewer3DPanel != null)
    //         viewer3DPanel.SetActive(true);
    // }

    public void OnCardClicked()
{
    Debug.LogError("CARD CLICKED = " + productName);

    if (SelectedProductStore.Instance != null)
    {
        SelectedProductStore.Instance.SetProduct(productName);
        Debug.LogError("STORE NOW = " + SelectedProductStore.Instance.selectedProductName);
    }
    else
    {
        Debug.LogError("SelectedProductStore.Instance is NULL");
        return;
    }

    if (viewer3DPanel != null)
        viewer3DPanel.SetActive(false);

    if (productPanel != null)
        productPanel.SetActive(false);

    if (viewer3DPanel != null)
        viewer3DPanel.SetActive(true);
}
}