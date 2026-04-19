using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MAIN MENU NAVIGATOR
///
/// Builds the main hub UI in code (same style as JewelryUI).
/// Shows a jewellery item selector + three large view-mode buttons:
///
///   [ 360° View ]   [ Real World ]   [ Try On ]
///
/// Flow:
///   1. User picks a category from the MENU popup (same popup as JewelryUI)
///   2. User picks an item card from the horizontal strip
///   3. User taps one of the three view buttons
///   4. MainMenuNavigator writes to JewelrySelectionBridge and loads the target scene
///
/// Scene name constants at the top — rename to match your Build Settings.
/// </summary>
public class MainMenuNavigator : MonoBehaviour
{
    // ── Scene names — match exactly with your Build Settings ────────
    public const string SCENE_360         = "View360";
    public const string SCENE_REAL_WORLD  = "RealWorld";
    public const string SCENE_TRY_ON      = "TryOn";
    public const string SCENE_MAIN_MENU   = "MainMenu";

    [Header("Required")]
    [Tooltip("The JewelryManager that holds all categories/items")]
    public JewelryManager jewelryManager;

    // ── Layout ──────────────────────────────────────────────────────
    private const float TOP_H    = 140f;
    private const float MENU_W   = 170f;
    private const float STRIP_H  = 300f;
    private const float CARD_W   = 245f;
    private const float CARD_H   = 265f;
    private const float CARD_GAP = 10f;
    private const float BTN_AREA = 260f;   // height of the 3-button row above strip
    private const float ROW_H    = 88f;
    private const float ROW_GAP  = 5f;
    private const float POPUP_W  = 500f;

    // ── Colours (identical to JewelryUI) ────────────────────────────
    private static readonly Color C_TOP      = new Color(0.08f, 0.08f, 0.11f, 0.97f);
    private static readonly Color C_STRIP_BG = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_GOLD     = new Color(0.95f, 0.80f, 0.25f, 1.00f);
    private static readonly Color C_MENU_BG  = new Color(0.20f, 0.20f, 0.28f, 1.00f);
    private static readonly Color C_LABEL_BG = new Color(0.13f, 0.13f, 0.18f, 1.00f);
    private static readonly Color C_POPUP_BG = new Color(0.10f, 0.10f, 0.14f, 0.99f);
    private static readonly Color C_POPUP_BDR= new Color(0.90f, 0.75f, 0.20f, 1.00f);
    private static readonly Color C_ROW_OFF  = new Color(0.18f, 0.18f, 0.25f, 1.00f);
    private static readonly Color C_ROW_ON   = new Color(0.88f, 0.65f, 0.10f, 1.00f);
    private static readonly Color C_CARD_BG  = new Color(0.14f, 0.14f, 0.20f, 1.00f);
    private static readonly Color C_CARD_SEL = new Color(0.90f, 0.70f, 0.15f, 1.00f);
    private static readonly Color C_OVERLAY  = new Color(0.00f, 0.00f, 0.00f, 0.45f);

    private static readonly Color C_BTN_360    = new Color(0.15f, 0.40f, 0.85f, 1f);
    private static readonly Color C_BTN_REAL   = new Color(0.15f, 0.65f, 0.35f, 1f);
    private static readonly Color C_BTN_TRYON  = new Color(0.70f, 0.25f, 0.80f, 1f);
    private static readonly Color C_BTN_DIM    = new Color(0.20f, 0.20f, 0.28f, 1f);

    private static readonly Color[] ACCENTS = {
        new Color(0.95f,0.70f,0.10f), new Color(0.70f,0.30f,0.90f),
        new Color(0.15f,0.60f,0.95f), new Color(0.90f,0.30f,0.45f),
        new Color(0.20f,0.80f,0.45f), new Color(0.80f,0.65f,0.20f),
    };

    // ── Runtime ─────────────────────────────────────────────────────
    private Font             font;
    private Text             topLabel;
    private RectTransform    stripContent;
    private GameObject       popupRoot;
    private GameObject       overlay;
    private bool             popupOpen;
    private int              activeCat  = -1;
    private int              activeItem = -1;
    private List<Image>      rowBgImgs  = new List<Image>();
    private List<Text>       rowTxts    = new List<Text>();
    private List<GameObject> itemCards  = new List<GameObject>();

    // View-mode buttons (stored so we can dim them when nothing is selected)
    private GameObject btn360, btnReal, btnTryOn;

    // ════════════════════════════════════════════════════════════════
    void Start()
    {
        if (jewelryManager == null)
        { Debug.LogError("[MainMenu] JewelryManager not assigned!"); return; }

        // Set return scene in bridge
        JewelrySelectionBridge.ReturnScene = SCENE_MAIN_MENU;

        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        yield return new WaitForSeconds(0.1f);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) { Debug.LogError("[MainMenu] Font not found!"); yield break; }

        var cgo = new GameObject("MainMenuCanvas");
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
        BuildViewButtons(cv);
        BuildItemStrip(cv);
        BuildOverlay(cv);
        BuildPopup(cv);

        RefreshViewButtons();
        Debug.Log("[MainMenu] Built OK.");
    }

    // ════════════════════════════════════════════════════════════════
    //  TOP BAR
    // ════════════════════════════════════════════════════════════════
    void BuildTopBar(RectTransform cv)
    {
        var bar = MkPanel("TopBar", cv, C_TOP,
            new Vector2(0,1), new Vector2(1,1),
            new Vector2(0.5f,1), new Vector2(0, TOP_H), Vector2.zero);

        var mb = MkPanel("MenuBtn", Rt(bar), C_MENU_BG,
            new Vector2(0,0), new Vector2(0,1),
            new Vector2(0,0.5f), new Vector2(MENU_W, 0), Vector2.zero);
        MkText(mb, "= MENU", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(mb, TogglePopup);

        var lb = MkPanel("LabelBg", Rt(bar), C_LABEL_BG,
            new Vector2(0,0), new Vector2(1,1),
            new Vector2(0,0.5f), Vector2.zero, Vector2.zero);
        Rt(lb).offsetMin = new Vector2(MENU_W + 8f, 10f);
        Rt(lb).offsetMax = new Vector2(-8f, -10f);
        topLabel = MkText(lb, "SELECT JEWELLERY", 34, FontStyle.Bold, C_GOLD,
                          TextAnchor.MiddleLeft, 16f);
    }

    // ════════════════════════════════════════════════════════════════
    //  THREE VIEW BUTTONS  (above the strip)
    // ════════════════════════════════════════════════════════════════
    void BuildViewButtons(RectTransform cv)
    {
        // Container sits just above the item strip
        var area = MkPanel("ViewBtnArea", cv, new Color(0.07f, 0.07f, 0.10f, 0.95f),
            new Vector2(0,0), new Vector2(1,0),
            new Vector2(0.5f,0), new Vector2(0, BTN_AREA), new Vector2(0, STRIP_H));

        float pad   = 20f;
        float gap   = 14f;
        float btnW  = (1080f - pad * 2f - gap * 2f) / 3f;
        float btnH  = BTN_AREA - 40f;

        float[] xPositions = {
            pad + btnW * 0f + gap * 0f + btnW * 0.5f - 1080f * 0.5f,
            pad + btnW * 1f + gap * 1f + btnW * 0.5f - 1080f * 0.5f,
            pad + btnW * 2f + gap * 2f + btnW * 0.5f - 1080f * 0.5f,
        };

        btn360   = BuildViewBtn(area, "360°\nVIEW",   C_BTN_360,  new Vector2(xPositions[0], 0), btnW, btnH);
        btnReal  = BuildViewBtn(area, "REAL\nWORLD",  C_BTN_REAL, new Vector2(xPositions[1], 0), btnW, btnH);
        btnTryOn = BuildViewBtn(area, "TRY\nON",      C_BTN_TRYON,new Vector2(xPositions[2], 0), btnW, btnH);

        MkBtn(btn360,   () => NavigateTo(SCENE_360));
        MkBtn(btnReal,  () => NavigateTo(SCENE_REAL_WORLD));
        MkBtn(btnTryOn, () => NavigateTo(SCENE_TRY_ON));
    }

    GameObject BuildViewBtn(GameObject parent, string label, Color col,
                            Vector2 pos, float w, float h)
    {
        var btn = MkPanel("Btn", Rt(parent), col,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(w, h), pos);
        MkText(btn, label, 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        return btn;
    }

    void RefreshViewButtons()
    {
        bool hasItem = JewelrySelectionBridge.HasValidItem;
        Color enabledCol360   = C_BTN_360;
        Color enabledColReal  = C_BTN_REAL;
        Color enabledColTryOn = C_BTN_TRYON;

        if (btn360)   btn360  .GetComponent<Image>().color = hasItem ? enabledCol360   : C_BTN_DIM;
        if (btnReal)  btnReal .GetComponent<Image>().color = hasItem ? enabledColReal  : C_BTN_DIM;
        if (btnTryOn) btnTryOn.GetComponent<Image>().color = hasItem ? enabledColTryOn : C_BTN_DIM;
    }

    // ════════════════════════════════════════════════════════════════
    //  ITEM STRIP
    // ════════════════════════════════════════════════════════════════
    void BuildItemStrip(RectTransform cv)
    {
        var strip = MkPanel("Strip", cv, C_STRIP_BG,
            new Vector2(0,0), new Vector2(1,0),
            new Vector2(0.5f,0), new Vector2(0, STRIP_H), Vector2.zero);
        var srt = Rt(strip);

        var sr = strip.AddComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = false;
        sr.inertia = true; sr.decelerationRate = 0.12f;
        sr.scrollSensitivity = 60f;

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

        var cgo = new GameObject("Content", typeof(RectTransform));
        cgo.transform.SetParent(vpGo.transform, false);
        stripContent = cgo.GetComponent<RectTransform>();
        stripContent.anchorMin = new Vector2(0, 0.5f);
        stripContent.anchorMax = new Vector2(0, 0.5f);
        stripContent.pivot = new Vector2(0, 0.5f);
        stripContent.anchoredPosition = Vector2.zero;
        stripContent.sizeDelta = new Vector2(0, CARD_H);

        sr.content = stripContent;
        sr.viewport = vpRt;
    }

    // ════════════════════════════════════════════════════════════════
    //  OVERLAY
    // ════════════════════════════════════════════════════════════════
    void BuildOverlay(RectTransform cv)
    {
        overlay = MkPanel("Overlay", cv, C_OVERLAY,
            Vector2.zero, Vector2.one,
            new Vector2(0.5f,0.5f), Vector2.zero, Vector2.zero);
        MkBtn(overlay, ClosePopup);
        overlay.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP
    // ════════════════════════════════════════════════════════════════
    void BuildPopup(RectTransform cv)
    {
        int n = jewelryManager.categories?.Length ?? 0;
        float totalH = ROW_GAP + n * (ROW_H + ROW_GAP);

        popupRoot = MkPanel("PopupRoot", cv, C_POPUP_BDR,
            new Vector2(0,1), new Vector2(0,1),
            new Vector2(0,1), new Vector2(POPUP_W, totalH + 4f),
            new Vector2(2f, -TOP_H - 2f));

        var inner = MkPanel("PopupInner", Rt(popupRoot), C_POPUP_BG,
            Vector2.zero, Vector2.one,
            new Vector2(0.5f,0.5f), Vector2.zero, Vector2.zero);
        Rt(inner).offsetMin = new Vector2(2,2);
        Rt(inner).offsetMax = new Vector2(-2,-2);

        rowBgImgs.Clear(); rowTxts.Clear();

        for (int i = 0; i < n; i++)
        {
            var cat = jewelryManager.categories[i];
            if (cat == null) continue;
            int ci = i;
            Color accent = ACCENTS[i % ACCENTS.Length];
            float yPos = -(ROW_GAP + i * (ROW_H + ROW_GAP));

            var row = MkPanel("Row" + i, Rt(inner), C_ROW_OFF,
                new Vector2(0,1), new Vector2(1,1),
                new Vector2(0.5f,1), new Vector2(0, ROW_H), new Vector2(0, yPos));

            MkPanel("Ac", Rt(row), accent,
                new Vector2(0,0), new Vector2(0,1),
                new Vector2(0,0.5f), new Vector2(10,0), Vector2.zero);

            var txt = MkText(row, cat.categoryName.ToUpper(), 34, FontStyle.Bold,
                             Color.white, TextAnchor.MiddleLeft, 24f);
            rowBgImgs.Add(row.GetComponent<Image>());
            rowTxts.Add(txt);

            MkBtn(row, () => { OnCategoryTapped(ci); ClosePopup(); });
        }

        popupRoot.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP TOGGLE
    // ════════════════════════════════════════════════════════════════
    void TogglePopup() { if (popupOpen) ClosePopup(); else OpenPopup(); }
    void OpenPopup()  { popupOpen = true;  overlay.SetActive(true);  popupRoot.SetActive(true); }
    void ClosePopup() { popupOpen = false; popupRoot.SetActive(false); overlay.SetActive(false); }

    // ════════════════════════════════════════════════════════════════
    //  CATEGORY TAPPED
    // ════════════════════════════════════════════════════════════════
    void OnCategoryTapped(int idx)
    {
        if (jewelryManager.categories == null ||
            idx < 0 || idx >= jewelryManager.categories.Length) return;

        activeCat  = idx;
        activeItem = -1;
        JewelrySelectionBridge.SelectedCategory = jewelryManager.categories[idx];
        JewelrySelectionBridge.SelectedItem     = null;

        var cat = jewelryManager.categories[idx];
        if (topLabel) topLabel.text = cat.categoryName.ToUpper();

        for (int i = 0; i < rowBgImgs.Count; i++)
        {
            bool on = (i == idx);
            if (rowBgImgs[i]) rowBgImgs[i].color = on ? C_ROW_ON : C_ROW_OFF;
            if (rowTxts[i])   rowTxts[i].color   = on ? new Color(0.05f, 0.05f, 0.05f) : Color.white;
        }

        StartCoroutine(SpawnCards(cat));
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWN ITEM CARDS
    // ════════════════════════════════════════════════════════════════
    IEnumerator SpawnCards(JewelryCategory cat)
    {
        foreach (var c in itemCards) if (c) Destroy(c);
        itemCards.Clear();
        stripContent.sizeDelta = new Vector2(0, CARD_H);

        yield return null;

        if (cat.items == null || cat.items.Length == 0) yield break;

        int count = cat.items.Length;
        float totalW = count * CARD_W + (count + 1) * CARD_GAP;
        stripContent.sizeDelta = new Vector2(totalW, CARD_H);

        yield return null;

        for (int i = 0; i < count; i++)
        {
            var item = cat.items[i];
            if (item == null) continue;
            int ci = i;

            float xPos = CARD_GAP + ci * (CARD_W + CARD_GAP);

            var card = MkPanel("Card" + i, stripContent, C_CARD_BG,
                new Vector2(0,0.5f), new Vector2(0,0.5f),
                new Vector2(0,0.5f), new Vector2(CARD_W, CARD_H),
                new Vector2(xPos, 0));

            var thumb = MkPanel("Thumb", Rt(card), C_CARD_BG,
                Vector2.zero, Vector2.one,
                new Vector2(0.5f,0.5f), Vector2.zero, Vector2.zero);
            Rt(thumb).offsetMin = new Vector2(4, 50);
            Rt(thumb).offsetMax = new Vector2(-4, -4);

            if (item.thumbnailImage != null)
            {
                var img = thumb.GetComponent<Image>();
                img.sprite = item.thumbnailImage;
                img.color = Color.white;
                img.preserveAspect = true;
            }

            var ns = MkPanel("NS", Rt(card), new Color(0,0,0,0.88f),
                new Vector2(0,0), new Vector2(1,0),
                new Vector2(0.5f,0), new Vector2(0, 48), Vector2.zero);
            MkText(ns, item.itemName, 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            Image cardImg = card.GetComponent<Image>();
            MkBtn(card, () =>
            {
                foreach (var cc in itemCards)
                    if (cc) cc.GetComponent<Image>().color = C_CARD_BG;
                cardImg.color = C_CARD_SEL;
                activeItem = ci;
                JewelrySelectionBridge.SelectedItem      = item;
                JewelrySelectionBridge.SelectedItemIndex = ci;
                RefreshViewButtons();
            });

            itemCards.Add(card);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  NAVIGATE
    // ════════════════════════════════════════════════════════════════
    void NavigateTo(string sceneName)
    {
        if (!JewelrySelectionBridge.HasValidItem)
        {
            Debug.LogWarning("[MainMenu] Please select an item first.");
            // Optional: flash a message — left as a simple log for now
            return;
        }
        JewelrySelectionBridge.ReturnScene = SCENE_MAIN_MENU;
        SceneManager.LoadScene(sceneName);
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

    Text MkText(GameObject parent, string txt, int size, FontStyle style,
                Color color, TextAnchor align, float pad = 0f)
    {
        var go = new GameObject("T", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, 2f); rt.offsetMax = new Vector2(-4f, -2f);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = font; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = align; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Truncate;
        return t;
    }

    void MkBtn(GameObject go, UnityEngine.Events.UnityAction fn)
    {
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = cb;
        btn.onClick.AddListener(fn);
    }
}
