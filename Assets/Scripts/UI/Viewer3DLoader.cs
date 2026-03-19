// using UnityEngine;

// public class Viewer3DLoader : MonoBehaviour
// {
//     [Header("3D Viewer References")]
//     public Transform modelPivot;

//     [Header("Optional Spawn Tweaks")]
//     public Vector3 spawnPosition = Vector3.zero;
//     public Vector3 spawnRotationEuler = Vector3.zero;
//     public Vector3 spawnScale = Vector3.one * 50f;

//     private GameObject currentModelInstance;

//     private void OnEnable()
//     {
//         Debug.LogError("Viewer3DLoader ON ENABLE fired.");
//         LoadSelectedModel();
//     }

//     public void LoadSelectedModel()
//     {
//         Debug.LogError("LoadSelectedModel started.");

//         if (modelPivot == null)
//         {
//             Debug.LogError("Viewer3DLoader: modelPivot is NULL.");
//             return;
//         }

//         SelectedCategoryStore categoryStore = FindFirstObjectByType<SelectedCategoryStore>();
//         SelectedProductStore productStore = FindFirstObjectByType<SelectedProductStore>();

//         if (categoryStore == null)
//         {
//             Debug.LogError("Viewer3DLoader: categoryStore not found in scene.");
//             return;
//         }

//         if (productStore == null)
//         {
//             Debug.LogError("Viewer3DLoader: productStore not found in scene.");
//             return;
//         }

//         string category = categoryStore.selectedCategory;
//         string productName = productStore.selectedProductName;

//         Debug.LogError("CATEGORY = " + category);
//         Debug.LogError("PRODUCT = " + productName);

//         if (string.IsNullOrEmpty(category))
//         {
//             Debug.LogError("Viewer3DLoader: category is empty.");
//             return;
//         }

//         if (string.IsNullOrEmpty(productName))
//         {
//             Debug.LogError("Viewer3DLoader: productName is empty.");
//             return;
//         }

//         category = category.Trim();
//         productName = productName.Trim();

//         ClearCurrentModel();

//         string path = "Prefabs/Jewellery/" + category;
//         GameObject[] prefabs = Resources.LoadAll<GameObject>(path);

//         Debug.LogError("LOADING PATH = Resources/" + path);
//         Debug.LogError("PREFAB COUNT = " + prefabs.Length);

//         if (prefabs == null || prefabs.Length == 0)
//         {
//             Debug.LogError("Viewer3DLoader: no prefabs found.");
//             return;
//         }

//         GameObject matchedPrefab = null;
//         string cleanProductName = productName.ToLower().Trim().Replace(" ", "_");

//         foreach (GameObject prefab in prefabs)
//         {
//             string cleanPrefabName = prefab.name.ToLower().Trim().Replace(" ", "_");
//             Debug.LogError("CHECKING PREFAB = " + prefab.name);

//             if (cleanPrefabName == cleanProductName)
//             {
//                 matchedPrefab = prefab;
//                 break;
//             }
//         }

//         if (matchedPrefab == null)
//         {
//             Debug.LogError("Viewer3DLoader: no matched prefab for " + productName);
//             return;
//         }

//         currentModelInstance = Instantiate(matchedPrefab, modelPivot);
//         currentModelInstance.transform.localPosition = spawnPosition;
//         currentModelInstance.transform.localRotation = Quaternion.Euler(spawnRotationEuler);
//         currentModelInstance.transform.localScale = spawnScale;

//         Debug.LogError("SPAWNED MODEL = " + matchedPrefab.name);
//         Debug.LogError("MODEL WORLD POSITION = " + currentModelInstance.transform.position);
//     }

//     public void ClearCurrentModel()
//     {
//         if (modelPivot == null)
//             return;

//         if (currentModelInstance != null)
//         {
//             Destroy(currentModelInstance);
//             currentModelInstance = null;
//         }

//         for (int i = modelPivot.childCount - 1; i >= 0; i--)
//         {
//             Destroy(modelPivot.GetChild(i).gameObject);
//         }
//     }
// }
using UnityEngine;

public class Viewer3DLoader : MonoBehaviour
{
    [Header("3D Viewer References")]
    public Transform modelPivot;

    [Header("Viewer Fit")]
    public Vector3 earringsFitSize = new Vector3(1.2f, 1.2f, 1.2f);
    public Vector3 necklacesFitSize = new Vector3(1.8f, 1.8f, 1.8f);

    [Range(0.6f, 1f)]
    public float fitPadding = 0.9f;

    public float globalScaleMultiplier = 1f;

    [Header("Optional Final Offset")]
    public Vector3 spawnPosition = Vector3.zero;

    private GameObject currentRoot;
    private Transform currentVisual;

    private void OnEnable()
    {
        LoadSelectedModel();
    }

    public void LoadSelectedModel()
    {
        if (modelPivot == null)
        {
            Debug.LogError("Viewer3DLoader: modelPivot is NULL.");
            return;
        }

        SelectedCategoryStore categoryStore = FindFirstObjectByType<SelectedCategoryStore>();
        SelectedProductStore productStore = FindFirstObjectByType<SelectedProductStore>();

        if (categoryStore == null || productStore == null)
        {
            Debug.LogError("Viewer3DLoader: categoryStore or productStore not found.");
            return;
        }

        string category = categoryStore.selectedCategory?.Trim();
        string productName = productStore.selectedProductName?.Trim();

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(productName))
        {
            Debug.LogError("Viewer3DLoader: category or productName is empty.");
            return;
        }

        ClearCurrentModel();

        string path = "Prefabs/Jewellery/" + category;
        GameObject[] prefabs = Resources.LoadAll<GameObject>(path);

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("Viewer3DLoader: no prefabs found at Resources/" + path);
            return;
        }

        string cleanProductName = CleanName(productName);
        GameObject matchedPrefab = null;

        foreach (GameObject prefab in prefabs)
        {
            if (CleanName(prefab.name) == cleanProductName)
            {
                matchedPrefab = prefab;
                break;
            }
        }

        if (matchedPrefab == null)
        {
            Debug.LogError("Viewer3DLoader: no matched prefab for " + productName);
            return;
        }

        // Stable root anchored to pivot
        currentRoot = new GameObject("ViewerModelRoot_" + matchedPrefab.name);
        currentRoot.transform.SetParent(modelPivot, false);
        currentRoot.transform.localPosition = spawnPosition;
        currentRoot.transform.localRotation = Quaternion.identity;
        currentRoot.transform.localScale = Vector3.one;

        // Actual prefab inside wrapper
        GameObject instance = Instantiate(matchedPrefab, currentRoot.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        currentVisual = instance.transform;

        ApplyAutoRotation(currentVisual, category, cleanProductName);
        FitAndCenterVisual(currentVisual, category, cleanProductName);
        ForceEnableRenderers(currentVisual);

        DebugModelInfo(currentVisual, cleanProductName);
    }

    private void ApplyAutoRotation(Transform visual, string category, string cleanProductName)
    {
        string cleanCategory = CleanName(category);
        Vector3 euler = Vector3.zero;

        if (cleanCategory.Contains("necklace"))
        {
            // Current rule based on your testing
            if (cleanProductName == "egyptian_necklace" || cleanProductName == "necklace_gold")
                euler = new Vector3(90f, 0f, 0f);
            else
                euler = new Vector3(-90f, 0f, 0f);
        }
        else
        {
            euler = Vector3.zero;
        }

        visual.localRotation = Quaternion.Euler(euler);
    }

    private void FitAndCenterVisual(Transform visual, string category, string cleanProductName)
    {
        if (!TryGetLocalRendererBounds(visual, out Bounds bounds))
        {
            Debug.LogError("Viewer3DLoader: no renderers found on model " + cleanProductName);
            return;
        }

        Vector3 fitBox = GetFitBox(category);

        float sx = fitBox.x / Mathf.Max(bounds.size.x, 0.0001f);
        float sy = fitBox.y / Mathf.Max(bounds.size.y, 0.0001f);
        float sz = fitBox.z / Mathf.Max(bounds.size.z, 0.0001f);

        float uniformScale = Mathf.Min(sx, sy, sz) * fitPadding * globalScaleMultiplier;

        // rescue for microscopic models
        if (uniformScale < 0.01f) uniformScale = 0.01f;
        if (uniformScale > 5000f) uniformScale = 5000f;

        // Special safety boost for extremely tiny/bad imports
        if (cleanProductName == "hanging_diamond")
        {
            uniformScale *= 3f;
        }

        visual.localScale = Vector3.one * uniformScale;

        if (!TryGetLocalRendererBounds(visual, out bounds))
        {
            Debug.LogError("Viewer3DLoader: bounds failed after scaling for " + cleanProductName);
            return;
        }

        visual.localPosition = -bounds.center;
    }

    private Vector3 GetFitBox(string category)
    {
        string cleanCategory = CleanName(category);

        if (cleanCategory.Contains("necklace"))
            return necklacesFitSize;

        return earringsFitSize;
    }

    private bool TryGetLocalRendererBounds(Transform root, out Bounds combinedBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        combinedBounds = default;
        bool initialized = false;

        Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.gameObject.activeInHierarchy)
                continue;

            Bounds wb = r.bounds;

            Vector3 localCenter = rootWorldToLocal.MultiplyPoint3x4(wb.center);

            Vector3 ext = wb.extents;
            Vector3 axisX = rootWorldToLocal.MultiplyVector(new Vector3(ext.x, 0f, 0f));
            Vector3 axisY = rootWorldToLocal.MultiplyVector(new Vector3(0f, ext.y, 0f));
            Vector3 axisZ = rootWorldToLocal.MultiplyVector(new Vector3(0f, 0f, ext.z));

            Vector3 localExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
            );

            Bounds lb = new Bounds(localCenter, localExtents * 2f);

            if (!initialized)
            {
                combinedBounds = lb;
                initialized = true;
            }
            else
            {
                combinedBounds.Encapsulate(lb.min);
                combinedBounds.Encapsulate(lb.max);
            }
        }

        return initialized;
    }

    private void ForceEnableRenderers(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = true;
        }
    }

    private void DebugModelInfo(Transform root, string cleanProductName)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"Viewer3DLoader: {cleanProductName} renderer count = {renderers.Length}");

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Debug.Log(
                $"Renderer {i}: name={r.name}, enabled={r.enabled}, active={r.gameObject.activeInHierarchy}, " +
                $"boundsCenter={r.bounds.center}, boundsSize={r.bounds.size}, material={(r.sharedMaterial != null ? r.sharedMaterial.name : "NULL")}"
            );
        }
    }

    private string CleanName(string rawName)
    {
        return rawName.ToLower().Trim().Replace(" ", "_");
    }

    public void ClearCurrentModel()
    {
        if (currentRoot != null)
        {
            Destroy(currentRoot);
            currentRoot = null;
            currentVisual = null;
        }

        if (modelPivot == null) return;

        for (int i = modelPivot.childCount - 1; i >= 0; i--)
        {
            Destroy(modelPivot.GetChild(i).gameObject);
        }
    }
}