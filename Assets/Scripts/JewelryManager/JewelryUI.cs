using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// JEWELRY UI v5 — All issues fixed based on video analysis
///
/// FIXES:
///  1. POPUP NOT CLOSING — popup border GO was stored as `popup` variable but
///     the actual visible panel was its child. Rows' click listeners fired but
///     ClosePopup hid wrong object. Fixed: single root popup GO, no nested panels.
///
///  2. ITEMS NOT VISIBLE — stripContent had anchorMax.y=1 so it stretched
///     vertically to fill viewport, but cards had anchorMax.y=1 too which made
///     their height=0 against a zero-height parent. Fixed: cards now use
///     FIXED pixel height (CARD_H constant), not anchor-stretching.
///
///  3. CAMERA HANG — eliminated by not calling SetActive on tracking systems.
///     CameraManager v4 only changes requestedFacingDirection.
/// </summary>
public class JewelryUI : MonoBehaviour
{
    [Header("Required")]
    public JewelryManager jewelryManager;
    public CameraManager cameraManager;

    // ── Layout (ref 1080 x 1920) ────────────────────────────────────
    private const float TOP_H = 140f;
    private const float MENU_W = 170f;   // menu button width
    private const float STRIP_H = 300f;   // bottom strip height
    private const float REMOVE_H = 72f;
    private const float CARD_W = 245f;
    private const float CARD_H = 265f;   // FIXED height — avoids anchor stretch bug
    private const float CARD_GAP = 10f;
    private const float POPUP_W = 500f;
    private const float ROW_H = 88f;
    private const float ROW_GAP = 5f;

    // ── Colors ──────────────────────────────────────────────────────
    private static readonly Color C_TOP = new Color(0.08f, 0.08f, 0.11f, 0.97f);
    private static readonly Color C_STRIP_BG = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_REMOVE = new Color(0.58f, 0.10f, 0.10f, 1.00f);
    private static readonly Color C_GOLD = new Color(0.95f, 0.80f, 0.25f, 1.00f);
    private static readonly Color C_MENU_BG = new Color(0.20f, 0.20f, 0.28f, 1.00f);
    private static readonly Color C_LABEL_BG = new Color(0.13f, 0.13f, 0.18f, 1.00f);
    private static readonly Color C_POPUP_BG = new Color(0.10f, 0.10f, 0.14f, 0.99f);
    private static readonly Color C_POPUP_BDR = new Color(0.90f, 0.75f, 0.20f, 1.00f);
    private static readonly Color C_ROW_OFF = new Color(0.18f, 0.18f, 0.25f, 1.00f);
    private static readonly Color C_ROW_ON = new Color(0.88f, 0.65f, 0.10f, 1.00f);
    private static readonly Color C_CARD_BG = new Color(0.14f, 0.14f, 0.20f, 1.00f);
    private static readonly Color C_CARD_SEL = new Color(0.90f, 0.70f, 0.15f, 1.00f);
    private static readonly Color C_OVERLAY = new Color(0.00f, 0.00f, 0.00f, 0.45f);

    private static readonly Color[] ACCENTS = {
        new Color(0.95f,0.70f,0.10f), new Color(0.70f,0.30f,0.90f),
        new Color(0.15f,0.60f,0.95f), new Color(0.90f,0.30f,0.45f),
        new Color(0.20f,0.80f,0.45f), new Color(0.80f,0.65f,0.20f),
    };

    // ── Runtime ─────────────────────────────────────────────────────
    private Font font;
    private Text topLabel;
    private RectTransform stripContent;
    private GameObject popupRoot;      // single root that gets SetActive
    private GameObject overlay;
    private bool popupOpen;
    private int activeCat = -1;
    private List<Image> rowBgImgs = new List<Image>();
    private List<Text> rowTxts = new List<Text>();
    private List<GameObject> itemCards = new List<GameObject>();

    // ════════════════════════════════════════════════════════════════
    void Start()
    {
        if (jewelryManager == null)
        {
            Debug.LogError("[JewelryUI] JewelryManager NOT assigned in Inspector!");
            return;
        }
        if (cameraManager == null)
            cameraManager = FindObjectOfType<CameraManager>();

        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        yield return new WaitForSeconds(0.2f);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) { Debug.LogError("[JewelryUI] Font not found!"); yield break; }

        // Canvas
        var cgo = new GameObject("JewelryCanvas");
        DontDestroyOnLoad(cgo);
        var cvs = cgo.AddComponent<Canvas>();
        cvs.renderMode = RenderMode.ScreenSpaceOverlay;
        cvs.sortingOrder = 30000;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        var cv = cvs.GetComponent<RectTransform>();
        BuildTopBar(cv);
        BuildRemoveBar(cv);
        BuildItemStrip(cv);
        BuildOverlay(cv);
        BuildPopup(cv);

        Debug.Log("[JewelryUI] Built OK.");
    }

    // ════════════════════════════════════════════════════════════════
    //  TOP BAR
    // ════════════════════════════════════════════════════════════════
    void BuildTopBar(RectTransform cv)
    {
        var bar = MkPanel("TopBar", cv, C_TOP,
            ancMin: new Vector2(0, 1), ancMax: new Vector2(1, 1),
            pivot: new Vector2(0.5f, 1),
            size: new Vector2(0, TOP_H), pos: Vector2.zero);

        // Menu button
        var mb = MkPanel("MenuBtn", Rt(bar), C_MENU_BG,
            ancMin: new Vector2(0, 0), ancMax: new Vector2(0, 1),
            pivot: new Vector2(0, 0.5f),
            size: new Vector2(MENU_W, 0), pos: Vector2.zero);
        MkText(mb, "= MENU", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(mb, TogglePopup);

        // Category label
        var lb = MkPanel("LabelBg", Rt(bar), C_LABEL_BG,
            ancMin: new Vector2(0, 0), ancMax: new Vector2(1, 1),
            pivot: new Vector2(0, 0.5f),
            size: Vector2.zero, pos: Vector2.zero);
        Rt(lb).offsetMin = new Vector2(MENU_W + 8f, 10f);
        Rt(lb).offsetMax = new Vector2(-8f, -10f);
        topLabel = MkText(lb, "", 38, FontStyle.Bold, C_GOLD, TextAnchor.MiddleLeft, pad: 16f);
    }

    // ════════════════════════════════════════════════════════════════
    //  REMOVE BAR
    // ════════════════════════════════════════════════════════════════
    void BuildRemoveBar(RectTransform cv)
    {
        var bar = MkPanel("RemoveBar", cv, C_REMOVE,
            ancMin: new Vector2(0, 0), ancMax: new Vector2(1, 0),
            pivot: new Vector2(0.5f, 0),
            size: new Vector2(0, REMOVE_H), pos: new Vector2(0, STRIP_H));
        MkText(bar, "X   REMOVE ALL", 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(bar, () => jewelryManager.RemoveAll());
    }

    // ════════════════════════════════════════════════════════════════
    //  ITEM STRIP (horizontal scroll)
    // ════════════════════════════════════════════════════════════════
    void BuildItemStrip(RectTransform cv)
    {
        // Outer container — bottom of screen
        var strip = MkPanel("Strip", cv, C_STRIP_BG,
            ancMin: new Vector2(0, 0), ancMax: new Vector2(1, 0),
            pivot: new Vector2(0.5f, 0),
            size: new Vector2(0, STRIP_H), pos: Vector2.zero);
        var srt = Rt(strip);

        var sr = strip.AddComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = false;
        sr.inertia = true; sr.decelerationRate = 0.12f;
        sr.scrollSensitivity = 60f;

        // Viewport — plain RectTransform, no Image needed; RectMask2D handles clipping
        var vpGo = new GameObject("VP", typeof(RectTransform));
        vpGo.transform.SetParent(srt, false);
        var vpRt = vpGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.pivot = new Vector2(0, 0);
        vpRt.sizeDelta = Vector2.zero;
        vpRt.anchoredPosition = Vector2.zero;
        vpRt.offsetMin = new Vector2(6, 6);
        vpRt.offsetMax = new Vector2(-6, -6);
        vpGo.AddComponent<RectMask2D>();
        var vp = vpGo;

        // Content — anchored left, fixed height = CARD_H, width set per-load
        var cgo = new GameObject("Content", typeof(RectTransform));
        cgo.transform.SetParent(vp.transform, false);
        stripContent = cgo.GetComponent<RectTransform>();
        stripContent.anchorMin = new Vector2(0, 0.5f);  // left-centre anchor
        stripContent.anchorMax = new Vector2(0, 0.5f);
        stripContent.pivot = new Vector2(0, 0.5f);
        stripContent.anchoredPosition = Vector2.zero;
        stripContent.sizeDelta = new Vector2(0, CARD_H);  // fixed height

        sr.content = stripContent;
        sr.viewport = vpRt;
    }

    // ════════════════════════════════════════════════════════════════
    //  OVERLAY
    // ════════════════════════════════════════════════════════════════
    void BuildOverlay(RectTransform cv)
    {
        overlay = MkPanel("Overlay", cv, C_OVERLAY,
            ancMin: Vector2.zero, ancMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f),
            size: Vector2.zero, pos: Vector2.zero);
        MkBtn(overlay, ClosePopup);
        overlay.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP — single root GO, all rows inside it directly
    // ════════════════════════════════════════════════════════════════
    void BuildPopup(RectTransform cv)
    {
        int n = jewelryManager.categories?.Length ?? 0;
        float totalH = ROW_GAP + n * (ROW_H + ROW_GAP);

        // Single root panel — THIS is what gets hidden/shown
        popupRoot = MkPanel("PopupRoot", cv, C_POPUP_BDR,
            ancMin: new Vector2(0, 1), ancMax: new Vector2(0, 1),
            pivot: new Vector2(0, 1),
            size: new Vector2(POPUP_W, totalH + 4f),
            pos: new Vector2(2f, -TOP_H - 2f));

        // Inner background (2px inset for gold border effect)
        var inner = MkPanel("PopupInner", Rt(popupRoot), C_POPUP_BG,
            ancMin: Vector2.zero, ancMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f),
            size: Vector2.zero, pos: Vector2.zero);
        Rt(inner).offsetMin = new Vector2(2, 2);
        Rt(inner).offsetMax = new Vector2(-2, -2);

        rowBgImgs.Clear();
        rowTxts.Clear();

        for (int i = 0; i < n; i++)
        {
            var cat = jewelryManager.categories[i];
            if (cat == null) continue;

            int ci = i;
            Color accent = ACCENTS[i % ACCENTS.Length];
            // Y position from top of inner panel
            float yPos = -(ROW_GAP + i * (ROW_H + ROW_GAP));

            // Row — child of inner panel
            var row = MkPanel("Row" + i, Rt(inner), C_ROW_OFF,
                ancMin: new Vector2(0, 1), ancMax: new Vector2(1, 1),
                pivot: new Vector2(0.5f, 1),
                size: new Vector2(0, ROW_H), pos: new Vector2(0, yPos));

            // Accent stripe
            MkPanel("Ac", Rt(row), accent,
                ancMin: new Vector2(0, 0), ancMax: new Vector2(0, 1),
                pivot: new Vector2(0, 0.5f),
                size: new Vector2(10, 0), pos: Vector2.zero);

            // Text label
            var txt = MkText(row, cat.categoryName.ToUpper(),
                             34, FontStyle.Bold, Color.white,
                             TextAnchor.MiddleLeft, pad: 24f);

            rowBgImgs.Add(row.GetComponent<Image>());
            rowTxts.Add(txt);

            // Click — IMPORTANT: listener references ci (captured), not i
            MkBtn(row, () =>
            {
                OnCategoryTapped(ci);
                ClosePopup();
            });
        }

        popupRoot.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP TOGGLE
    // ════════════════════════════════════════════════════════════════
    void TogglePopup()
    {
        if (popupOpen) ClosePopup();
        else OpenPopup();
    }

    void OpenPopup()
    {
        popupOpen = true;
        overlay.SetActive(true);
        popupRoot.SetActive(true);
    }

    void ClosePopup()
    {
        popupOpen = false;
        popupRoot.SetActive(false);
        overlay.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  CATEGORY TAPPED
    // ════════════════════════════════════════════════════════════════
    void OnCategoryTapped(int idx)
    {
        if (jewelryManager.categories == null ||
            idx < 0 || idx >= jewelryManager.categories.Length) return;

        activeCat = idx;
        var cat = jewelryManager.categories[idx];

        Debug.Log($"[JewelryUI] Tapped: {cat.categoryName}");

        // 1. Switch camera (instant, no hang)
        if (cameraManager == null) cameraManager = FindObjectOfType<CameraManager>();
        cameraManager?.SwitchForJewelryType(cat.type);

        // 2. Update label
        if (topLabel != null) topLabel.text = cat.categoryName.ToUpper();

        // 3. Highlight selected row
        for (int i = 0; i < rowBgImgs.Count; i++)
        {
            bool on = (i == idx);
            if (rowBgImgs[i] != null) rowBgImgs[i].color = on ? C_ROW_ON : C_ROW_OFF;
            if (rowTxts[i] != null) rowTxts[i].color = on
                ? new Color(0.05f, 0.05f, 0.05f) : Color.white;
        }

        // 4. Load items after 2-frame layout delay
        StartCoroutine(SpawnCards(cat));
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWN ITEM CARDS
    // ════════════════════════════════════════════════════════════════
    IEnumerator SpawnCards(JewelryCategory cat)
    {
        // Destroy previous cards
        foreach (var c in itemCards) if (c) Destroy(c);
        itemCards.Clear();
        stripContent.sizeDelta = new Vector2(0, CARD_H);

        yield return null; // wait one frame

        if (cat.items == null || cat.items.Length == 0)
        {
            Debug.LogWarning($"[JewelryUI] '{cat.categoryName}' has no items.");
            yield break;
        }

        // Count valid items (prefab required to equip, but show card regardless)
        int count = cat.items.Length;

        if (count == 0) { Debug.LogWarning($"[JewelryUI] No items in category."); yield break; }

        // Set content width
        float totalW = count * CARD_W + (count + 1) * CARD_GAP;
        stripContent.sizeDelta = new Vector2(totalW, CARD_H);

        yield return null; // wait for layout

        int col = 0;
        for (int i = 0; i < cat.items.Length; i++)
        {
            var item = cat.items[i];
            if (item == null) continue;
            if (item.jewelryPrefab == null)
                Debug.LogWarning($"[JewelryUI] Item '{item.itemName}' has no prefab assigned!");
            int ci = i;

            float xPos = CARD_GAP + col * (CARD_W + CARD_GAP);

            // ── Card root — FIXED size, positioned from left ──────────
            var card = MkPanel("Card" + i, stripContent, C_CARD_BG,
                ancMin: new Vector2(0, 0.5f),   // left-centre anchor
                ancMax: new Vector2(0, 0.5f),
                pivot: new Vector2(0, 0.5f),
                size: new Vector2(CARD_W, CARD_H),
                pos: new Vector2(xPos, 0));

            // ── Thumbnail (fills card minus 50px name strip at bottom) ─
            var thumb = MkPanel("Thumb", Rt(card), C_CARD_BG,
                ancMin: Vector2.zero, ancMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f),
                size: Vector2.zero, pos: Vector2.zero);
            Rt(thumb).offsetMin = new Vector2(4, 50);
            Rt(thumb).offsetMax = new Vector2(-4, -4);

            if (item.thumbnailImage != null)
            {
                var img = thumb.GetComponent<Image>();
                img.sprite = item.thumbnailImage;
                img.color = Color.white;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
            }

            // ── Name strip at card bottom — fixed 48px height ─────────
            var ns = MkPanel("NS", Rt(card), new Color(0, 0, 0, 0.88f),
                ancMin: new Vector2(0, 0), ancMax: new Vector2(1, 0),
                pivot: new Vector2(0.5f, 0),
                size: new Vector2(0, 48), pos: Vector2.zero);
            MkText(ns, item.itemName, 19, FontStyle.Bold,
                   Color.white, TextAnchor.MiddleCenter);

            // ── Click ─────────────────────────────────────────────────
            Image cardImg = card.GetComponent<Image>();
            MkBtn(card, () =>
            {
                foreach (var cc in itemCards)
                    if (cc) cc.GetComponent<Image>().color = C_CARD_BG;
                cardImg.color = C_CARD_SEL;
                jewelryManager.EquipJewelryByIndex(activeCat, ci);
            });

            itemCards.Add(card);
            col++;
        }

        Debug.Log($"[JewelryUI] {itemCards.Count} cards for '{cat.categoryName}'");
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════

    RectTransform Rt(GameObject go) => go.GetComponent<RectTransform>();

    GameObject MkPanel(string name, RectTransform parent, Color color,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = color;
        return go;
    }

    Text MkText(GameObject parent, string text, int size, FontStyle style,
                Color color, TextAnchor align, float pad = 0f)
    {
        var go = new GameObject("T", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, 2f); rt.offsetMax = new Vector2(-4f, -2f);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = font; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = align; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    void MkBtn(GameObject go, UnityEngine.Events.UnityAction fn)
    {
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = cb;
        btn.onClick.AddListener(fn);
    }
}