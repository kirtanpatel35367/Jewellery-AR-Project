using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// JEWELRY VIEW 360 UI  — v2
///
/// FULLY MATCHES SharedJewelryUI visual design:
///   ┌──────────────────────────────────────────┐  TOP BAR 130px
///   │ [< BACK]  360° VIEW  ·  SDS  │  [ ] Cap │
///   ├──────────────────────────────────────────┤
///   │       SELECT JEWELLERY  ▼   Earrings     │  CAT ROW (inside top bar)
///   ├──────────────────────────────────────────┤
///   │                                          │
///   │           3-D Model viewport             │
///   │                                          │
///   ├──────────────────────────────────────────┤
///   │  ↻  Drag to rotate   •   Pinch to zoom  │  HINT BAR  56px
///   ├──────────────────────────────────────────┤
///   │  ←  scrollable item cards  →             │  ITEM STRIP  300px
///   ├──────────────────────────────────────────┤
///   │  ↺  RESET ROTATION                       │  RESET BAR  72px
///   └──────────────────────────────────────────┘
///
/// CHANGES vs old JewelryView360UI:
///   + Back button (top-left) → loads MainMenuScene
///   + Screenshot button (top-right)
///   + SDS brand label in top bar
///   + Scene title "360° VIEW" in top bar
///   + Category label row (SELECT JEWELLERY ▼ / active cat name)
///   + All colours/sizing identical to SharedJewelryUI
///   - Removed old "= MENU" button style
///   - Reset bar now matches SharedJewelryUI Remove bar style exactly
///
/// INSPECTOR FIELDS:
///   viewManager    → 360Manager GameObject (JewelryView360Manager)
///   jewelryManager → JewelryManager GameObject (JewelryManager)
///   mainMenuScene  → "MainMenuScene" (default)
/// </summary>
public class JewelryView360UI : MonoBehaviour
{
    [Header("Required")]
    public JewelryView360Manager viewManager;
    public JewelryManager jewelryManager;

    [Header("Navigation")]
    [Tooltip("Exact scene name from Build Settings")]
    public string mainMenuScene = "MainMenuScene";

    // ── Layout (ref 1080 × 1920) — identical to SharedJewelryUI ─────
    private const float TOP_H = 130f;
    private const float BTN_SIDE = 90f;    // back + screenshot square buttons
    private const float STRIP_H = 300f;
    private const float RESET_H = 72f;
    private const float HINT_H = 56f;
    private const float CARD_W = 245f;
    private const float CARD_H = 265f;
    private const float CARD_GAP = 10f;
    private const float POPUP_W = 500f;
    private const float ROW_H = 88f;
    private const float ROW_GAP = 5f;

    // ── Colours — identical to SharedJewelryUI ───────────────────────
    private static readonly Color C_TOP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_STRIP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_GOLD = new Color(0.95f, 0.80f, 0.25f, 1.00f);
    private static readonly Color C_LABEL = new Color(0.13f, 0.13f, 0.18f, 1.00f);
    private static readonly Color C_BTN = new Color(0.18f, 0.18f, 0.26f, 1.00f);
    private static readonly Color C_RESET = new Color(0.58f, 0.10f, 0.10f, 1.00f);
    private static readonly Color C_HINT = new Color(0.10f, 0.10f, 0.15f, 0.92f);
    private static readonly Color C_POPUP_BG = new Color(0.10f, 0.10f, 0.14f, 0.99f);
    private static readonly Color C_ROW_OFF = new Color(0.18f, 0.18f, 0.25f, 1.00f);
    private static readonly Color C_ROW_ON = new Color(0.88f, 0.65f, 0.10f, 1.00f);
    private static readonly Color C_CARD_BG = new Color(0.14f, 0.14f, 0.20f, 1.00f);
    private static readonly Color C_CARD_SEL = new Color(0.90f, 0.70f, 0.15f, 1.00f);
    private static readonly Color C_OVERLAY = new Color(0.00f, 0.00f, 0.00f, 0.50f);

    // ── Runtime ──────────────────────────────────────────────────────
    private Font _font;
    private Text _catLabel;      // shows active category name
    private RectTransform _stripContent;
    private GameObject _popupRoot;
    private GameObject _overlay;
    private bool _popupOpen;
    private int _activeCat = -1;
    private List<Image> _rowBgs = new List<Image>();
    private List<Text> _rowTxts = new List<Text>();
    private List<GameObject> _cards = new List<GameObject>();

    // ════════════════════════════════════════════════════════════════
    void Start()
    {
        if (jewelryManager == null)
        {
            Debug.LogError("[360UI] JewelryManager not assigned in Inspector!");
            return;
        }
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        yield return new WaitForSeconds(0.2f);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) { Debug.LogError("[360UI] Font not found!"); yield break; }

        // ── Canvas ─────────────────────────────────────────────────
        var cgo = new GameObject("Canvas360UI");
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
        BuildResetBar(cv);
        BuildHintBar(cv);
        BuildItemStrip(cv);
        BuildOverlay(cv);
        BuildPopup(cv);

        Debug.Log("[360UI] Built OK — matching SharedJewelryUI style.");
    }

    // ════════════════════════════════════════════════════════════════
    //  TOP BAR  — identical layout to SharedJewelryUI.BuildTopBar
    // ════════════════════════════════════════════════════════════════
    void BuildTopBar(RectTransform cv)
    {
        var bar = MkPanel("TopBar", cv, C_TOP,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, TOP_H), Vector2.zero);

        // ── Back button (left) ──────────────────────────────────────
        var back = MkPanel("BackBtn", Rt(bar), C_BTN,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(BTN_SIDE, 0), Vector2.zero);
        MkText(back, "< BACK", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(back, GoBack);

        // ── Screenshot button (right) ───────────────────────────────
        var cap = MkPanel("CaptureBtn", Rt(bar), C_BTN,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(BTN_SIDE, 0), Vector2.zero);
        MkText(cap, "[ ]", 28, FontStyle.Bold, C_GOLD, TextAnchor.MiddleCenter);
        MkBtn(cap, TakeScreenshot);

        // ── Centre area (title + category row) ─────────────────────
        var centre = MkPanel("Centre", Rt(bar), new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(centre).offsetMin = new Vector2(BTN_SIDE + 4f, 0f);
        Rt(centre).offsetMax = new Vector2(-(BTN_SIDE + 4f), 0f);

        // SDS brand (top-right of centre)
        var brand = MkPanel("Brand", Rt(centre), new Color(0, 0, 0, 0),
            new Vector2(1, 0.5f), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(120, 0), Vector2.zero);
        MkText(brand, "SDS", 24, FontStyle.Bold, C_GOLD, TextAnchor.MiddleRight);

        // Scene title (top half, left of SDS)
        var titleGo = MkPanel("Title", Rt(centre), new Color(0, 0, 0, 0),
            new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(titleGo).offsetMax = new Vector2(-130, 0);
        MkText(titleGo, "360° VIEW", 28, FontStyle.Bold,
               new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft, pad: 12f);

        // Category row (bottom half) — "SELECT JEWELLERY ▼ | active cat"
        var catRow = MkPanel("CatRow", Rt(centre), C_LABEL,
            new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(catRow).offsetMin = new Vector2(0, 6f);
        Rt(catRow).offsetMax = new Vector2(0, -2f);

        MkText(catRow, "SELECT JEWELLERY  \u25bc", 20, FontStyle.Normal,
               new Color(0.60f, 0.60f, 0.65f), TextAnchor.MiddleLeft, pad: 12f);
        MkBtn(catRow, TogglePopup);

        // Active category name (right side of cat row)
        var catNameGo = MkPanel("CatName", Rt(catRow), new Color(0, 0, 0, 0),
            new Vector2(0.4f, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            Vector2.zero, Vector2.zero);
        _catLabel = MkText(catNameGo, "— tap to select —", 20, FontStyle.Bold,
                           C_GOLD, TextAnchor.MiddleRight, pad: 12f);
    }

    // ════════════════════════════════════════════════════════════════
    //  RESET BAR — same height/style as SharedJewelryUI Remove bar
    // ════════════════════════════════════════════════════════════════
    void BuildResetBar(RectTransform cv)
    {
        var bar = MkPanel("ResetBar", cv, C_RESET,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, RESET_H), Vector2.zero);
        MkText(bar, "\u21ba  RESET ROTATION", 32, FontStyle.Bold,
               Color.white, TextAnchor.MiddleCenter);
        MkBtn(bar, ResetRotation);
    }

    // ════════════════════════════════════════════════════════════════
    //  HINT BAR — 360-specific instruction, sits above item strip
    // ════════════════════════════════════════════════════════════════
    void BuildHintBar(RectTransform cv)
    {
        var hb = MkPanel("HintBar", cv, C_HINT,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, HINT_H),
            new Vector2(0, STRIP_H + RESET_H));
        MkText(hb, "\u21bb  Drag to rotate   \u2022   Pinch to zoom",
               24, FontStyle.Normal,
               new Color(0.60f, 0.60f, 0.70f), TextAnchor.MiddleCenter);
    }

    // ════════════════════════════════════════════════════════════════
    //  ITEM STRIP — identical to SharedJewelryUI
    // ════════════════════════════════════════════════════════════════
    void BuildItemStrip(RectTransform cv)
    {
        var strip = MkPanel("ItemStrip", cv, C_STRIP,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, STRIP_H),
            new Vector2(0, RESET_H));

        var sr = strip.AddComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = false;
        sr.inertia = true; sr.decelerationRate = 0.12f;
        sr.scrollSensitivity = 60f;

        var vpGo = new GameObject("VP", typeof(RectTransform));
        vpGo.transform.SetParent(Rt(strip), false);
        var vpRt = vpGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.pivot = Vector2.zero; vpRt.sizeDelta = Vector2.zero;
        vpRt.offsetMin = new Vector2(6, 6);
        vpRt.offsetMax = new Vector2(-6, -6);
        vpGo.AddComponent<RectMask2D>();

        var cgo = new GameObject("Content", typeof(RectTransform));
        cgo.transform.SetParent(vpGo.transform, false);
        _stripContent = cgo.GetComponent<RectTransform>();
        _stripContent.anchorMin = new Vector2(0, 0.5f);
        _stripContent.anchorMax = new Vector2(0, 0.5f);
        _stripContent.pivot = new Vector2(0, 0.5f);
        _stripContent.anchoredPosition = Vector2.zero;
        _stripContent.sizeDelta = new Vector2(0, CARD_H);

        sr.content = _stripContent;
        sr.viewport = vpRt;
    }

    // ════════════════════════════════════════════════════════════════
    //  OVERLAY
    // ════════════════════════════════════════════════════════════════
    void BuildOverlay(RectTransform cv)
    {
        _overlay = MkPanel("Overlay", cv, C_OVERLAY,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        MkBtn(_overlay, ClosePopup);
        _overlay.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP — identical structure to SharedJewelryUI
    //  (360 mode: shows ALL categories — no Bangle/Ring filtering needed
    //   because this is a display-only scene, not a placement scene)
    // ════════════════════════════════════════════════════════════════
    void BuildPopup(RectTransform cv)
    {
        int n = jewelryManager.categories?.Length ?? 0;

        _popupRoot = MkPanel("PopupRoot", cv, C_POPUP_BG,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(POPUP_W, 0), Vector2.zero);
        Rt(_popupRoot).offsetMin = new Vector2(0, STRIP_H + RESET_H + HINT_H);
        Rt(_popupRoot).offsetMax = new Vector2(POPUP_W, -TOP_H);

        // Gold left-border accent
        MkPanel("Border", Rt(_popupRoot), C_GOLD,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(4, 0), Vector2.zero);

        _rowBgs.Clear();
        _rowTxts.Clear();

        float y = -ROW_GAP;
        for (int i = 0; i < n; i++)
        {
            var cat = jewelryManager.categories[i];
            if (cat == null) continue;
            int ci = i;

            var row = MkPanel("Row_" + i, Rt(_popupRoot), C_ROW_OFF,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, ROW_H), new Vector2(0, y));
            Rt(row).offsetMin = new Vector2(8, 0);
            Rt(row).offsetMax = new Vector2(-8, 0);
            Rt(row).anchoredPosition = new Vector2(0, y);
            Rt(row).sizeDelta = new Vector2(0, ROW_H);

            _rowBgs.Add(row.GetComponent<Image>());

            var txt = MkText(row, cat.categoryName, 30, FontStyle.Bold,
                             Color.white, TextAnchor.MiddleLeft, pad: 20f);
            _rowTxts.Add(txt);

            MkBtn(row, () => OnCategoryTapped(ci));
            y -= (ROW_H + ROW_GAP);
        }

        _popupRoot.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP TOGGLE
    // ════════════════════════════════════════════════════════════════
    void TogglePopup() { if (_popupOpen) ClosePopup(); else OpenPopup(); }

    void OpenPopup()
    {
        _popupOpen = true;
        _overlay.SetActive(true);
        _popupRoot.SetActive(true);
    }

    void ClosePopup()
    {
        _popupOpen = false;
        _popupRoot.SetActive(false);
        _overlay.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  CATEGORY TAPPED
    // ════════════════════════════════════════════════════════════════
    void OnCategoryTapped(int idx)
    {
        if (jewelryManager.categories == null ||
            idx < 0 || idx >= jewelryManager.categories.Length) return;

        _activeCat = idx;
        var cat = jewelryManager.categories[idx];

        // Update category label in top bar
        if (_catLabel != null) _catLabel.text = cat.categoryName.ToUpper();

        // Highlight selected row
        for (int i = 0; i < _rowBgs.Count; i++)
        {
            bool on = (i == idx);
            if (_rowBgs[i]) _rowBgs[i].color = on ? C_ROW_ON : C_ROW_OFF;
            if (_rowTxts[i]) _rowTxts[i].color = on
                ? new Color(0.05f, 0.05f, 0.05f) : Color.white;
        }

        // Write to bridge so 360Manager can read category context
        JewelrySelectionBridge.SelectedCategory = cat;

        ClosePopup();
        StartCoroutine(SpawnCards(cat));
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWN ITEM CARDS — identical to SharedJewelryUI
    // ════════════════════════════════════════════════════════════════
    IEnumerator SpawnCards(JewelryCategory cat)
    {
        foreach (var c in _cards) if (c) Destroy(c);
        _cards.Clear();
        _stripContent.sizeDelta = new Vector2(0, CARD_H);

        yield return null;

        if (cat.items == null || cat.items.Length == 0)
        {
            Debug.LogWarning($"[360UI] '{cat.categoryName}' has no items.");
            yield break;
        }

        int count = cat.items.Length;
        float totalW = count * CARD_W + (count + 1) * CARD_GAP;
        _stripContent.sizeDelta = new Vector2(totalW, CARD_H);

        yield return null;

        for (int i = 0; i < cat.items.Length; i++)
        {
            var item = cat.items[i];
            if (item == null) continue;
            int ci = i;
            float xP = CARD_GAP + i * (CARD_W + CARD_GAP);

            var card = MkPanel("Card" + i, _stripContent, C_CARD_BG,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(CARD_W, CARD_H),
                new Vector2(xP, 0));

            // Thumbnail
            var thumb = MkPanel("Thumb", Rt(card), C_CARD_BG,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
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

            // Name strip at bottom of card
            var ns = MkPanel("NS", Rt(card), new Color(0, 0, 0, 0.88f),
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0, 48), Vector2.zero);
            MkText(ns, item.itemName, 19, FontStyle.Bold,
                   Color.white, TextAnchor.MiddleCenter);

            // Click → tell 360Manager to show this item
            Image cardImg = card.GetComponent<Image>();
            MkBtn(card, () =>
            {
                foreach (var cc in _cards)
                    if (cc) cc.GetComponent<Image>().color = C_CARD_BG;
                cardImg.color = C_CARD_SEL;

                // Update top bar label to item name
                if (_catLabel != null) _catLabel.text = item.itemName.ToUpper();

                // Write to bridge
                JewelrySelectionBridge.SelectedItem = item;
                JewelrySelectionBridge.SelectedItemIndex = ci;

                // Refresh the 3D model
                if (viewManager != null)
                    viewManager.RefreshModel();
                else
                    Debug.LogWarning("[360UI] viewManager not assigned!");
            });

            _cards.Add(card);
        }

        Debug.Log($"[360UI] {_cards.Count} cards for '{cat.categoryName}'");
    }

    // ════════════════════════════════════════════════════════════════
    //  RESET ROTATION
    // ════════════════════════════════════════════════════════════════
    void ResetRotation()
    {
        if (viewManager != null && viewManager.displayPivot != null)
        {
            viewManager.displayPivot.localRotation = Quaternion.identity;
            Debug.Log("[360UI] Rotation reset.");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  BACK → MAIN MENU
    // ════════════════════════════════════════════════════════════════
    void GoBack()
    {
        if (string.IsNullOrEmpty(mainMenuScene))
        {
            Debug.LogWarning("[360UI] mainMenuScene is empty — set it in Inspector.");
            return;
        }
        SceneManager.LoadScene(mainMenuScene);
    }

    // ════════════════════════════════════════════════════════════════
    //  SCREENSHOT
    // ════════════════════════════════════════════════════════════════
    void TakeScreenshot()
    {
        string fileName = $"SDS_360_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        ScreenCapture.CaptureScreenshot(fileName);
        Debug.Log($"[360UI] Screenshot: {fileName}");
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS — identical to SharedJewelryUI
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
        t.text = text; t.font = _font; t.fontSize = size; t.fontStyle = style;
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
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = cb;
        btn.onClick.AddListener(fn);
    }
}