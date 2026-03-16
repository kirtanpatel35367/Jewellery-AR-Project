// using UnityEngine;

// public class SelectedProductStore : MonoBehaviour
// {
//     public static SelectedProductStore Instance;

//     [Header("Current Selection")]
//     public string selectedProductName;

//     private void Awake()
//     {
//         if (Instance == null)
//         {
//             Instance = this;
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }

//     public void SetProduct(string productName)
//     {
//         selectedProductName = productName;
//         Debug.Log("Selected Product: " + selectedProductName);
//     }
// }
using UnityEngine;

public class SelectedProductStore : MonoBehaviour
{
    public static SelectedProductStore Instance;

    [Header("Current Selection")]
    public string selectedProductName;

    private void Awake()
    {
        Instance = this;
        Debug.Log("SelectedProductStore Awake.");
    }

    private void OnEnable()
    {
        Instance = this;
    }

    public void SetProduct(string productName)
    {
        selectedProductName = productName;
        Debug.Log("Selected Product: " + selectedProductName);
    }
}