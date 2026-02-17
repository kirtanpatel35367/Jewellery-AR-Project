using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// JewelryUI — Standard Unity stretch-anchor approach.
///
/// Uses the correct Unity pattern:
///   • Every panel stretches to fill its parent via anchorMin=0,0 anchorMax=1,1
///   • Then offsetMin/offsetMax crop it to the desired region
///   • This is how Unity's own UI samples work and is reliable everywhere
///
/// Layout:
///   Top bar    = full width, top N pixels    → offsetMin.y = SH - topH
///   Bottom panel = full width, bottom N px  → offsetMax.y = panelH  
///   Items      = placed manually inside content
/// </summary>
public class JewelryUI : MonoBehaviour
{
    [Header("Required")]
    public JewelryManager jewelryManager;
    public Canvas mainCanvas;

    [Header("Colors")]
    public Color barColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    public Color catNormal = new Color(0.20f, 0.20f, 0.28f, 1f);
    public Color catSelected = new Color(0.82f, 0.62f, 0.10f, 1f);
    public Color itemBg = new Color(0.22f, 0.22f, 0.30f, 1f);
    public Color itemHighlight = new Color(0.82f, 0.62f, 0.10f, 1f);
    public Color removeColor = new Color(0.72f, 0.13f, 0.13f, 1f);

    // These are set from screen size at runtime
    private float topH;      // top bar height
    private float botH;      // bottom panel height
    private float cellW;     // item cell width
    private float cellH;     // item cell height
    private float gap;       // cell gap
    private float pad;       // panel padding
    private int cols = 3;

    private int activeCat;
    private RectTransform contentRT;
    private List<Button> catBtns = new List<Button>();
    private List<Button> itemBtns = new List<Button>();
    private Font font;

    // ── Entry point ───────────────────────────────────────────────────────
    void Start()
    {
        if (jewelryManager == null || mainCanvas == null)
        {
            Debug.LogError("[JewelryUI] Assign JewelryManager + Canvas in Inspector!");
            return;
        }
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        // Step 1: lock orientation and wait for it to settle
        Screen.orientation = ScreenOrientation.Portrait;
        yield return new WaitForSeconds(0.1f);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Step 2: configure canvas FIRST, before reading any sizes
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 999;
        mainCanvas.worldCamera = null;

        // Disable CanvasScaler — we work in raw pixels
        CanvasScaler cs = mainCanvas.GetComponent<CanvasScaler>();
        if (cs != null) { cs.enabled = false; }

        // Ensure GraphicRaycaster
        if (mainCanvas.GetComponent<GraphicRaycaster>() == null)
            mainCanvas.gameObject.AddComponent<GraphicRaycaster>();

        // Step 3: wait TWO frames for canvas to resolve its rect
        yield return null;
        yield return null;

        // Step 4: read the CANVAS rect (not Screen directly - scaler may affect it)
        RectTransform canvasRT = mainCanvas.GetComponent<RectTransform>();
        float CW = canvasRT.rect.width;
        float CH = canvasRT.rect.height;

        // If canvas rect is still zero, fall back to Screen size
        if (CW < 1f || CH < 1f)
        {
            CW = Screen.width;
            CH = Screen.height;
        }

        Debug.Log($"[JewelryUI] Canvas: {CW}x{CH}  Screen: {Screen.width}x{Screen.height}");

        // Step 5: calculate layout proportions
        topH = Mathf.Round(CH * 0.07f);
        botH = Mathf.Round(CH * 0.22f);
        pad = Mathf.Round(CW * 0.03f);
        gap = Mathf.Round(CW * 0.02f);
        cellW = Mathf.Round((CW - pad * 2f - gap * (cols - 1)) / cols);
        cellH = Mathf.Round(cellW * 1.15f);

        Debug.Log($"[JewelryUI] topH={topH} botH={botH} cellW={cellW} cellH={cellH}");

        // Step 6: build UI
        BuildTopBar(canvasRT, CW, CH);
        BuildBottomPanel(canvasRT, CW, CH);
        BuildRemoveButton(canvasRT, CW, CH);
        SelectCategory(0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TOP BAR  — sits at the top of the canvas
    // ─────────────────────────────────────────────────────────────────────
    void BuildTopBar(RectTransform canvasRT, float CW, float CH)
    {
        // Stretch to full canvas, then crop to top topH pixels
        GameObject bar = Make("TopBar", canvasRT);
        RectTransform rt = RT(bar);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, CH - topH);  // bottom of bar
        rt.offsetMax = new Vector2(0, 0);           // top of bar = canvas top
        BG(bar, barColor);

        // Category buttons inside the bar
        float btnW = Mathf.Round((CW - pad * 2f - gap * (jewelryManager.CategoryCount - 1))
                                  / jewelryManager.CategoryCount);
        float btnH = topH - pad;
        float btnY = pad * 0.5f;  // from bottom of bar

        for (int i = 0; i < jewelryManager.CategoryCount; i++)
        {
            int ci = i;
            string nm = jewelryManager.categories[i].categoryName;

            GameObject obj = Make("Cat_" + nm, rt);
            RectTransform brt = RT(obj);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.zero;
            brt.pivot = new Vector2(0f, 0f);
            float bx = pad + i * (btnW + gap);
            brt.anchoredPosition = new Vector2(bx, btnY);
            brt.sizeDelta = new Vector2(btnW, btnH);
            BG(obj, catNormal);

            Button btn = obj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            Label(obj.transform, nm, Mathf.RoundToInt(btnH * 0.35f),
                  FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            btn.onClick.AddListener(() => SelectCategory(ci));
            catBtns.Add(btn);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // BOTTOM PANEL  — sits at the bottom of the canvas
    // ─────────────────────────────────────────────────────────────────────
    void BuildBottomPanel(RectTransform canvasRT, float CW, float CH)
    {
        // Stretch to full canvas, then crop to bottom botH pixels
        GameObject panel = Make("BottomPanel", canvasRT);
        RectTransform prt = RT(panel);
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(0, 0);     // bottom of panel = canvas bottom
        prt.offsetMax = new Vector2(0, -(CH - botH)); // top of panel = botH above bottom
        BG(panel, barColor);

        // ScrollRect
        ScrollRect sr = panel.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.decelerationRate = 0.15f;
        sr.scrollSensitivity = 20f;

        // Viewport fills the panel
        GameObject vp = Make("Viewport", prt);
        RectTransform vrt = RT(vp);
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        Image vi = vp.AddComponent<Image>();
        vi.color = Color.clear;
        Mask msk = vp.AddComponent<Mask>();
        msk.showMaskGraphic = false;

        // Content — top-anchored, height set when items are spawned
        GameObject ct = Make("Content", RT(vp));
        contentRT = RT(ct);
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, botH);
        contentRT.anchoredPosition = Vector2.zero;

        sr.viewport = vrt;
        sr.content = contentRT;
    }

    // ─────────────────────────────────────────────────────────────────────
    // REMOVE BUTTON  — just above the bottom panel
    // ─────────────────────────────────────────────────────────────────────
    void BuildRemoveButton(RectTransform canvasRT, float CW, float CH)
    {
        float btnW = CW * 0.38f;
        float btnH = CH * 0.042f;

        GameObject obj = Make("RemoveBtn", canvasRT);
        RectTransform rt = RT(obj);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(btnW, btnH);
        rt.anchoredPosition = new Vector2(CW - pad, botH + pad);
        BG(obj, removeColor);

        Button btn = obj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => jewelryManager.RemoveAll());
        Label(obj.transform, "✕  Remove All",
              Mathf.RoundToInt(btnH * 0.4f),
              FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    // ─────────────────────────────────────────────────────────────────────
    // SELECT CATEGORY
    // ─────────────────────────────────────────────────────────────────────
    void SelectCategory(int idx)
    {
        activeCat = idx;

        // Update tab highlight
        for (int i = 0; i < catBtns.Count; i++)
        {
            Image bg = catBtns[i].GetComponent<Image>();
            if (bg) bg.color = (i == idx) ? catSelected : catNormal;
        }

        // Clear old items
        for (int c = contentRT.childCount - 1; c >= 0; c--)
            DestroyImmediate(contentRT.GetChild(c).gameObject);
        itemBtns.Clear();

        JewelryCategory cat = jewelryManager.categories[idx];
        JewelryItem[] items = cat.items;

        if (items == null || items.Length == 0)
        {
            Debug.LogWarning($"[JewelryUI] No items in '{cat.categoryName}'");
            return;
        }

        // Place items manually
        int placed = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            int ci = i;
            int col = placed % cols;
            int row = placed / cols;
            float x = pad + col * (cellW + gap);
            float y = pad + row * (cellH + gap);

            Button btn = SpawnItem(items[i], x, y);
            btn.onClick.AddListener(() =>
            {
                jewelryManager.EquipJewelryByIndex(activeCat, ci);
                HighlightItem(ci);
            });
            itemBtns.Add(btn);
            placed++;
        }

        // Set content height
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)placed / cols));
        float height = pad * 2f + rows * cellH + (rows - 1) * gap;
        height = Mathf.Max(height, botH);
        contentRT.sizeDelta = new Vector2(0f, height);
        contentRT.anchoredPosition = Vector2.zero;

        Debug.Log($"[JewelryUI] Cat={idx} Items={placed} Height={height}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // SPAWN ONE ITEM BUTTON
    // ─────────────────────────────────────────────────────────────────────
    Button SpawnItem(JewelryItem item, float x, float y)
    {
        GameObject obj = Make(item.itemName, contentRT);
        RectTransform rt = RT(obj);
        rt.anchorMin = new Vector2(0f, 1f);  // top-left anchor
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(cellW, cellH);
        rt.anchoredPosition = new Vector2(x, -y);   // y negative = downward

        Image bg = BG(obj, itemBg);
        if (item.thumbnailImage != null)
        {
            bg.sprite = item.thumbnailImage;
            bg.color = Color.white;
            bg.preserveAspect = true;
            bg.type = Image.Type.Simple;
        }

        Button btn = obj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;

        // Name strip at bottom of cell
        int stripH = Mathf.RoundToInt(cellH * 0.28f);
        GameObject strip = Make("Strip", RT(obj));
        RectTransform srt = RT(strip);
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.sizeDelta = new Vector2(0f, stripH);
        srt.anchoredPosition = Vector2.zero;
        BG(strip, new Color(0f, 0f, 0f, 0.72f));
        Label(strip.transform, item.itemName,
              Mathf.RoundToInt(stripH * 0.52f),
              FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);

        return btn;
    }

    void HighlightItem(int idx)
    {
        for (int i = 0; i < itemBtns.Count; i++)
        {
            Image bg = itemBtns[i].GetComponent<Image>();
            if (bg) bg.color = (i == idx) ? itemHighlight : itemBg;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────
    static GameObject Make(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
    static GameObject Make(string name, RectTransform parent) =>
        Make(name, parent.transform);

    static RectTransform RT(GameObject go) =>
        go.GetComponent<RectTransform>();

    Image BG(GameObject go, Color c)
    {
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = true;
        return img;
    }

    void Label(Transform parent, string text, int size,
               FontStyle style, Color col, TextAnchor align)
    {
        var go = Make("Lbl", parent);
        var rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 2f);
        rt.offsetMax = new Vector2(-3f, -2f);

        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = col;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
    }
}