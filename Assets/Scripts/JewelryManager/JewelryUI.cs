using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class JewelryUI : MonoBehaviour
{
    [Header("Required")]
    public JewelryManager jewelryManager;

    private Font font;
    private int activeCat = 0;
    private List<GameObject> itemObjects = new List<GameObject>();
    private List<GameObject> catObjects = new List<GameObject>();

    // We build everything on ONE new canvas we create ourselves
    private Canvas uiCanvas;
    private GameObject bottomPanel;
    private GameObject itemGrid;

    void Start()
    {
        if (jewelryManager == null)
        {
            Debug.LogError("[JewelryUI] Assign JewelryManager!");
            return;
        }
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        yield return new WaitForSeconds(0.5f); // let AR scene finish loading
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildEverything();
    }

    void BuildEverything()
    {
        // ── Create a brand-new canvas ────────────────────────────────────
        GameObject canvasGO = new GameObject("JEWELRY_UI_CANVAS");
        DontDestroyOnLoad(canvasGO);

        uiCanvas = canvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 32767; // maximum possible — on top of everything

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

        // ── TOP BAR ──────────────────────────────────────────────────────
        GameObject topBar = MakeBox("TopBar", canvasRT,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0.5f, 1), new Vector2(0, 130));
        SetColor(topBar, new Color(0.1f, 0.1f, 0.15f, 1));

        // Category buttons in top bar
        float catW = 540f - 20f; // half width minus gap
        Color[] catColors = new Color[]
        {
            new Color(0.82f,0.62f,0.10f,1),  // gold (selected)
            new Color(0.25f,0.25f,0.35f,1)   // dark (unselected)
        };

        for (int i = 0; i < Mathf.Min(jewelryManager.CategoryCount, 2); i++)
        {
            int ci = i;
            string nm = jewelryManager.categories[i].categoryName;
            float xPos = 20f + i * (catW + 20f);

            GameObject catBtn = MakeBox("CatBtn_" + i, topBar.GetComponent<RectTransform>(),
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0.5f), new Vector2(catW, -20));
            catBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(xPos, 0);
            SetColor(catBtn, catColors[i]);
            AddText(catBtn, nm, 36, Color.white);

            Button b = catBtn.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SelectCat(ci));
            catObjects.Add(catBtn);
        }

        // ── BOTTOM PANEL ─────────────────────────────────────────────────
        bottomPanel = MakeBox("BottomPanel", canvasRT,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0.5f, 0), new Vector2(0, 450));
        SetColor(bottomPanel, new Color(0.12f, 0.12f, 0.18f, 1));

        // Item grid inside bottom panel (vertical scroll via manual layout)
        itemGrid = MakeBox("ItemGrid", bottomPanel.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(0, 1), new Vector2(0, 0));
        itemGrid.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        // No Image on itemGrid so it's transparent
        Object.Destroy(itemGrid.GetComponent<Image>());

        // Add GridLayoutGroup
        GridLayoutGroup g = itemGrid.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(320, 300);
        g.spacing = new Vector2(15, 15);
        g.padding = new RectOffset(20, 20, 20, 20);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 3;
        g.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter cf = itemGrid.AddComponent<ContentSizeFitter>();
        cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wrap in ScrollRect
        ScrollRect sr = bottomPanel.AddComponent<ScrollRect>();
        sr.content = itemGrid.GetComponent<RectTransform>();
        sr.viewport = bottomPanel.GetComponent<RectTransform>();
        sr.horizontal = false;
        sr.vertical = true;

        // ── REMOVE BUTTON ─────────────────────────────────────────────────
        GameObject remBtn = MakeBox("RemoveBtn", canvasRT,
            new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(1, 0), new Vector2(300, 75));
        remBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-20, 470);
        SetColor(remBtn, new Color(0.8f, 0.15f, 0.15f, 1));
        AddText(remBtn, "X Remove All", 26, Color.white);
        Button rb = remBtn.AddComponent<Button>();
        rb.transition = Selectable.Transition.None;
        rb.onClick.AddListener(() => jewelryManager.RemoveAll());

        // ── Show first category ───────────────────────────────────────────
        SelectCat(0);
    }

    void SelectCat(int idx)
    {
        activeCat = idx;

        // Update category button colors
        for (int i = 0; i < catObjects.Count; i++)
        {
            Image img = catObjects[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == idx)
                    ? new Color(0.82f, 0.62f, 0.10f, 1)
                    : new Color(0.25f, 0.25f, 0.35f, 1);
        }

        // Clear old items
        foreach (var ob in itemObjects)
            if (ob != null) Destroy(ob);
        itemObjects.Clear();

        JewelryItem[] items = jewelryManager.categories[idx].items;
        if (items == null || items.Length == 0)
        {
            Debug.LogWarning("[JewelryUI] No items in category " + idx);
            return;
        }

        // Spawn item buttons
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            int ci = i;
            GameObject cell = SpawnCell(items[i]);
            Button b = cell.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                jewelryManager.EquipJewelryByIndex(activeCat, ci);
                HighlightCell(ci);
            });
            itemObjects.Add(cell);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            itemGrid.GetComponent<RectTransform>());
    }

    GameObject SpawnCell(JewelryItem item)
    {
        // Outer cell (border)
        GameObject cell = new GameObject(item.itemName, typeof(RectTransform));
        cell.transform.SetParent(itemGrid.transform, false);
        Image border = cell.AddComponent<Image>();
        border.color = new Color(0.7f, 0.7f, 0.9f, 1f); // light blue-grey border

        // Inner background (inset 4px)
        GameObject inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(cell.transform, false);
        RectTransform irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(4, 4);
        irt.offsetMax = new Vector2(-4, -4);
        Image bg = inner.AddComponent<Image>();

        // If thumbnail exists, show it; otherwise show a purple box
        if (item.thumbnailImage != null)
        {
            bg.sprite = item.thumbnailImage;
            bg.color = Color.white;
            bg.preserveAspect = true;
        }
        else
        {
            bg.color = new Color(0.30f, 0.28f, 0.48f, 1f); // visible purple
        }

        // Name label at bottom
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(inner.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 0);
        lrt.pivot = new Vector2(0.5f, 0);
        lrt.sizeDelta = new Vector2(0, 80);
        lrt.anchoredPosition = Vector2.zero;

        // Dark strip behind text
        Image labelBg = labelGO.AddComponent<Image>();
        labelBg.color = new Color(0, 0, 0, 0.85f);

        // Text
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(labelGO.transform, false);
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4, 2);
        trt.offsetMax = new Vector2(-4, -2);
        Text t = textGO.AddComponent<Text>();
        t.text = item.itemName;
        t.font = font;
        t.fontSize = 24;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;

        return cell;
    }

    void HighlightCell(int idx)
    {
        for (int i = 0; i < itemObjects.Count; i++)
        {
            Image img = itemObjects[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == idx)
                    ? new Color(0.82f, 0.62f, 0.10f, 1f)
                    : new Color(0.7f, 0.7f, 0.9f, 1f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Makes a RectTransform box with an Image
    GameObject MakeBox(string name, RectTransform parent,
                       Vector2 ancMin, Vector2 ancMax,
                       Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().raycastTarget = true;
        return go;
    }

    void SetColor(GameObject go, Color c)
    {
        Image img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = c;
    }

    void AddText(GameObject go, string txt, int size, Color col)
    {
        GameObject tgo = new GameObject("Lbl", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        RectTransform rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4, 2);
        rt.offsetMax = new Vector2(-4, -2);
        Text t = tgo.AddComponent<Text>();
        t.text = txt;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.color = col;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
    }
}