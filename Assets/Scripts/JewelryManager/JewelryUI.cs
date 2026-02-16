using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UPDATED VERSION - Dynamic category-based UI
/// Automatically generates buttons for all categories and items
/// </summary>
public class JewelryUI : MonoBehaviour
{
    [Header("Manager Reference")]
    [Tooltip("The JewelryManager that controls jewelry spawning")]
    public JewelryManager jewelryManager;

    [Header("UI Containers")]
    [Tooltip("Parent object for category buttons (Earrings, Necklace tabs)")]
    public Transform categoryButtonContainer;

    [Tooltip("Parent object for item buttons (individual jewelry thumbnails)")]
    public Transform itemButtonContainer;

    [Tooltip("Optional: Scroll view for items (if you have many items)")]
    public ScrollRect itemScrollView;

    [Header("Button Prefabs")]
    [Tooltip("Template for category buttons")]
    public GameObject categoryButtonPrefab;

    [Tooltip("Template for item buttons (jewelry thumbnails)")]
    public GameObject itemButtonPrefab;

    [Header("Remove Button")]
    [Tooltip("Button to remove all jewelry")]
    public Button removeAllButton;

    [Header("UI Settings")]
    [Tooltip("Highlight color for selected category")]
    public Color selectedCategoryColor = new Color(0.2f, 0.6f, 1f);

    [Tooltip("Normal color for category buttons")]
    public Color normalCategoryColor = Color.white;

    [Tooltip("Animation speed for button transitions")]
    public float buttonAnimationSpeed = 0.2f;

    // State tracking
    private int currentCategoryIndex = -1;
    private List<Button> categoryButtons = new List<Button>();
    private List<GameObject> itemButtonObjects = new List<GameObject>();

    void Start()
    {
        // Setup remove button if assigned
        if (removeAllButton != null)
        {
            removeAllButton.onClick.AddListener(() => jewelryManager.RemoveAllJewelry());
        }

        // Generate category buttons
        CreateCategoryButtons();

        // Auto-select first category
        if (jewelryManager.GetCategoryCount() > 0)
        {
            SelectCategory(0);
        }
    }

    // ============ CATEGORY BUTTON CREATION ============

    /// <summary>
    /// Creates buttons for each category (Earrings, Necklace, etc.)
    /// </summary>
    void CreateCategoryButtons()
    {
        Debug.Log("🎨 Creating category buttons...");

        if (categoryButtonContainer == null)
        {
            Debug.LogError("❌ Category Button Container not assigned!");
            return;
        }

        if (categoryButtonPrefab == null)
        {
            Debug.LogError("❌ Category Button Prefab not assigned!");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in categoryButtonContainer)
        {
            Destroy(child.gameObject);
        }
        categoryButtons.Clear();

        // Create a button for each category
        for (int i = 0; i < jewelryManager.categories.Length; i++)
        {
            JewelryCategory category = jewelryManager.categories[i];

            // Instantiate button
            GameObject buttonObj = Instantiate(categoryButtonPrefab, categoryButtonContainer);
            buttonObj.name = $"CategoryButton_{category.categoryName}";

            // Get button component
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"❌ Category button prefab missing Button component!");
                continue;
            }

            // Setup button appearance
            SetupCategoryButton(buttonObj, category, i);

            // Add click listener
            int categoryIndex = i; // Capture for closure
            button.onClick.AddListener(() => SelectCategory(categoryIndex));

            // Store reference
            categoryButtons.Add(button);

            Debug.Log($"✓ Created category button: {category.categoryName}");
        }

        Debug.Log($"✓ Created {categoryButtons.Count} category buttons");
    }

    /// <summary>
    /// Configures the visual appearance of a category button
    /// </summary>
    void SetupCategoryButton(GameObject buttonObj, JewelryCategory category, int index)
    {
        // Set text
        Text buttonText = buttonObj.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = category.categoryName;
            buttonText.fontSize = 20;
            buttonText.fontStyle = FontStyle.Bold;
        }

        // Set icon (if available)
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null && category.categoryIcon != null)
        {
            buttonImage.sprite = category.categoryIcon;
        }

        // You can also add a separate Image component for icons
        Image iconImage = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null && category.categoryIcon != null)
        {
            iconImage.sprite = category.categoryIcon;
            iconImage.preserveAspect = true;
        }
    }

    // ============ CATEGORY SELECTION ============

    /// <summary>
    /// Called when user clicks a category button
    /// Shows all items in that category
    /// </summary>
    public void SelectCategory(int categoryIndex)
    {
        Debug.Log($"📂 Selecting category: {categoryIndex}");

        if (categoryIndex < 0 || categoryIndex >= jewelryManager.categories.Length)
        {
            Debug.LogError($"❌ Invalid category index: {categoryIndex}");
            return;
        }

        // Update current category
        currentCategoryIndex = categoryIndex;

        // Update category button visuals
        UpdateCategoryButtonStates();

        // Clear and create new item buttons
        CreateItemButtons();
    }

    /// <summary>
    /// Updates the visual state of category buttons (highlight selected)
    /// </summary>
    void UpdateCategoryButtonStates()
    {
        for (int i = 0; i < categoryButtons.Count; i++)
        {
            Button button = categoryButtons[i];
            ColorBlock colors = button.colors;

            if (i == currentCategoryIndex)
            {
                // Selected state
                colors.normalColor = selectedCategoryColor;
                colors.highlightedColor = selectedCategoryColor * 1.1f;

                // Make text bold
                Text text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.fontStyle = FontStyle.Bold;
                }
            }
            else
            {
                // Normal state
                colors.normalColor = normalCategoryColor;
                colors.highlightedColor = normalCategoryColor * 0.9f;

                // Make text normal
                Text text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.fontStyle = FontStyle.Normal;
                }
            }

            button.colors = colors;
        }
    }

    // ============ ITEM BUTTON CREATION ============

    /// <summary>
    /// Creates thumbnail buttons for all items in the current category
    /// </summary>
    void CreateItemButtons()
    {
        Debug.Log("🎨 Creating item buttons...");

        if (itemButtonContainer == null)
        {
            Debug.LogError("❌ Item Button Container not assigned!");
            return;
        }

        if (itemButtonPrefab == null)
        {
            Debug.LogError("❌ Item Button Prefab not assigned!");
            return;
        }

        // Clear existing item buttons
        ClearItemButtons();

        // Get items from current category
        JewelryCategory category = jewelryManager.categories[currentCategoryIndex];

        Debug.Log($"📦 Creating {category.items.Length} item buttons for {category.categoryName}");

        // Create a button for each item
        for (int i = 0; i < category.items.Length; i++)
        {
            JewelryItem item = category.items[i];

            // Instantiate button
            GameObject buttonObj = Instantiate(itemButtonPrefab, itemButtonContainer);
            buttonObj.name = $"ItemButton_{item.itemName}";

            // Get button component
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("❌ Item button prefab missing Button component!");
                continue;
            }

            // Setup button appearance
            SetupItemButton(buttonObj, item, i);

            // Add click listener
            int itemIndex = i; // Capture for closure
            button.onClick.AddListener(() => SelectItem(itemIndex));

            // Store reference
            itemButtonObjects.Add(buttonObj);

            Debug.Log($"✓ Created item button: {item.itemName}");
        }

        Debug.Log($"✓ Created {itemButtonObjects.Count} item buttons");

        // Scroll to top
        if (itemScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            itemScrollView.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    /// Configures the visual appearance of an item button
    /// </summary>
    void SetupItemButton(GameObject buttonObj, JewelryItem item, int index)
    {
        // Set thumbnail image (main button image)
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null && item.thumbnailImage != null)
        {
            buttonImage.sprite = item.thumbnailImage;
            buttonImage.preserveAspect = true;
        }
        else if (buttonImage != null)
        {
            // No thumbnail - use placeholder color
            buttonImage.color = new Color(0.8f, 0.8f, 0.8f);
        }

        // Set item name (if there's a Text component)
        Text nameText = buttonObj.GetComponentInChildren<Text>();
        if (nameText != null)
        {
            nameText.text = item.itemName;
            nameText.fontSize = 14;
            nameText.alignment = TextAnchor.MiddleCenter;
        }

        // You can also find specific child objects
        // For example, if your prefab has:
        // - "Thumbnail" Image for the picture
        // - "NameLabel" Text for the name

        Transform thumbnailTransform = buttonObj.transform.Find("Thumbnail");
        if (thumbnailTransform != null)
        {
            Image thumbnailImage = thumbnailTransform.GetComponent<Image>();
            if (thumbnailImage != null && item.thumbnailImage != null)
            {
                thumbnailImage.sprite = item.thumbnailImage;
                thumbnailImage.preserveAspect = true;
            }
        }

        Transform nameTransform = buttonObj.transform.Find("NameLabel");
        if (nameTransform != null)
        {
            Text labelText = nameTransform.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.text = item.itemName;
            }
        }

        // Optional: Add description
        Transform descTransform = buttonObj.transform.Find("Description");
        if (descTransform != null && !string.IsNullOrEmpty(item.description))
        {
            Text descText = descTransform.GetComponent<Text>();
            if (descText != null)
            {
                descText.text = item.description;
            }
        }
    }

    /// <summary>
    /// Removes all item buttons
    /// </summary>
    void ClearItemButtons()
    {
        foreach (GameObject buttonObj in itemButtonObjects)
        {
            if (buttonObj != null)
            {
                Destroy(buttonObj);
            }
        }
        itemButtonObjects.Clear();
    }

    // ============ ITEM SELECTION ============

    /// <summary>
    /// Called when user clicks an item button
    /// Tells the JewelryManager to equip that jewelry
    /// </summary>
    public void SelectItem(int itemIndex)
    {
        Debug.Log($"✨ Selected item: {itemIndex} in category {currentCategoryIndex}");

        // Tell the manager to equip this jewelry
        jewelryManager.EquipJewelryByIndex(currentCategoryIndex, itemIndex);

        // Optional: Add visual feedback
        HighlightSelectedItem(itemIndex);
    }

    /// <summary>
    /// Optional: Highlights the selected item button
    /// </summary>
    void HighlightSelectedItem(int itemIndex)
    {
        for (int i = 0; i < itemButtonObjects.Count; i++)
        {
            Button button = itemButtonObjects[i].GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;

                if (i == itemIndex)
                {
                    // Selected - add a border or change color
                    colors.normalColor = Color.yellow;
                    colors.highlightedColor = Color.yellow * 1.1f;
                }
                else
                {
                    // Normal
                    colors.normalColor = Color.white;
                    colors.highlightedColor = Color.white * 0.9f;
                }

                button.colors = colors;
            }
        }
    }

    // ============ PUBLIC UTILITY METHODS ============

    /// <summary>
    /// Programmatically select a category (useful for default selection)
    /// </summary>
    public void SetActiveCategory(string categoryName)
    {
        for (int i = 0; i < jewelryManager.categories.Length; i++)
        {
            if (jewelryManager.categories[i].categoryName == categoryName)
            {
                SelectCategory(i);
                return;
            }
        }
        Debug.LogWarning($"⚠ Category not found: {categoryName}");
    }

    /// <summary>
    /// Programmatically select an item by name
    /// </summary>
    public void SelectItemByName(string itemName)
    {
        if (currentCategoryIndex < 0) return;

        JewelryItem[] items = jewelryManager.categories[currentCategoryIndex].items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].itemName == itemName)
            {
                SelectItem(i);
                return;
            }
        }
        Debug.LogWarning($"⚠ Item not found: {itemName}");
    }

    /// <summary>
    /// Refresh the UI (useful if categories change at runtime)
    /// </summary>
    public void RefreshUI()
    {
        CreateCategoryButtons();
        if (currentCategoryIndex >= 0)
        {
            CreateItemButtons();
        }
    }
}