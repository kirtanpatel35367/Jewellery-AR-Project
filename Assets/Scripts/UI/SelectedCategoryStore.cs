// using UnityEngine;

// public class SelectedCategoryStore : MonoBehaviour
// {
//     public static SelectedCategoryStore Instance;

//     [Header("Current Selection")]
//     public string selectedCategory;

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

//     public void SetCategory(string categoryName)
//     {
//         selectedCategory = categoryName;
//         Debug.Log("Selected Category: " + selectedCategory);
//     }
// }
using UnityEngine;

public class SelectedCategoryStore : MonoBehaviour
{
    public static SelectedCategoryStore Instance;

    [Header("Current Selection")]
    public string selectedCategory;

    private void Awake()
    {
        Instance = this;
        Debug.Log("SelectedCategoryStore Awake.");
    }

    private void OnEnable()
    {
        Instance = this;
    }

    public void SetCategory(string categoryName)
    {
        selectedCategory = categoryName;
        Debug.Log("Selected Category: " + selectedCategory);
    }
}