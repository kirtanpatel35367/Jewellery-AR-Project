using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SharedJewelryUI — A single UI prefab that works across ALL three scenes:
///   • 360 View     (Jewelry3DScene)
///   • Place on Room (JewelryARScene)
///   • Try-On        (FaceTryOn)
///
/// HOW TO SET UP:
///   1. Create an empty GameObject called "SharedUI" in EACH of your 3 scenes.
///   2. Attach this script to it.
///   3. Set SceneMode to the correct enum value in each scene's Inspector.
///   4. Assign JewelryManager (only needed in PlaceOnRoom + TryOn scenes).
///   5. Optionally assign a Capture Camera for screenshot button.
///
/// FEATURES:
///   • Top bar: Back → Main Menu button + dynamic scene title + SDS brand
///   • Bottom panel: Category dropdown + horizontal item scroll + Remove All
///   • Screenshot/Capture button (top-right corner)
///   • 360 View mode hides Remove All (stateless view)
///   • Auto-hides Bangle/Ring categories in 360 View (no hand tracking there)
///   • AR session is reset/stopped when user taps Back, so the next AR scene
///     always starts with a clean camera state (no back-camera bleed-over).
/// </summary>
public class SharedJewelryUI : MonoBehaviour
{
    // ── Scene identity ────────────────────────────────────────────────
    public enum Mode { Scene360, PlaceOnRoom, TryOn }

    [Header("Scene Setup")]
    [Tooltip("Which scene is this UI instance running in?")]
    public Mode sceneMode = Mode.TryOn;

    [Tooltip("Human-readable title shown in the top bar")]
    public string sceneTitle = "Try-On";

    [Header("References (assign in Inspector)")]
    [Tooltip("Required for PlaceOnRoom and TryOn scenes")]
    public JewelryManager jewelryManager;

    [Tooltip("Main Menu scene name — used by Back button")]
    public string mainMenuSceneName = "MainMenuScene";

    [Tooltip("Optional: camera used for screenshot. Falls back to Camera.main")]
    public Camera captureCamera;

    // ── Layout constants (ref 1080×1920) ─────────────────────────────
    private const float TOP_H = 130f;
    private const float STRIP_H = 300f;
    private const float REMOVE_H = 72f;
    private const float CARD_W = 245f;
    private const float CARD_H = 265f;
    private const float CARD_GAP = 10f;
    private const float POPUP_W = 500f;
    private const float ROW_H = 88f;
    private const float ROW_GAP = 5f;
    private const float BTN_SIDE = 90f;   // square back/capture buttons

    // ── Brand colors ──────────────────────────────────────────────────
    private static readonly Color C_TOP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_STRIP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_GOLD = new Color(0.95f, 0.80f, 0.25f, 1.00f);
    private static readonly Color C_LABEL = new Color(0.13f, 0.13f, 0.18f, 1.00f);
    private static readonly Color C_BTN = new Color(0.18f, 0.18f, 0.26f, 1.00f);
    private static readonly Color C_REMOVE = new Color(0.58f, 0.10f, 0.10f, 1.00f);
    private static readonly Color C_POPUP_BG = new Color(0.10f, 0.10f, 0.14f, 0.99f);
    private static readonly Color C_ROW_OFF = new Color(0.18f, 0.18f, 0.25f, 1.00f);
    private static readonly Color C_ROW_ON = new Color(0.88f, 0.65f, 0.10f, 1.00f);
    private static readonly Color C_CARD_BG = new Color(0.14f, 0.14f, 0.20f, 1.00f);
    private static readonly Color C_CARD_SEL = new Color(0.90f, 0.70f, 0.15f, 1.00f);
    private static readonly Color C_OVERLAY = new Color(0.00f, 0.00f, 0.00f, 0.50f);

    // ── Runtime ───────────────────────────────────────────────────────
    private Font _font;
    private Text _catLabel;
    private RectTransform _stripContent;
    private GameObject _popupRoot;
    private GameObject _overlay;
    private bool _popupOpen;
    private int _activeCat = -1;

    private List<Image> _rowBgs = new List<Image>();
    private List<Text> _rowTxts = new List<Text>();
    private List<GameObject> _cards = new List<GameObject>();

    // Categories visible in this scene (360 hides Bangle/Ring)
    private List<int> _visibleCatIndices = new List<int>();

    // ═════════════════════════════════════════════════════════════════
    void Start()
    {
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        yield return new WaitForSeconds(0.15f);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) { Debug.LogError("[SharedJewelryUI] Font missing!"); yield break; }

        // ── Canvas ────────────────────────────────────────────────────
        var cgo = new GameObject("SharedJewelryCanvas");
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
        BuildItemStrip(cv);
        if (sceneMode != Mode.Scene360)
            BuildRemoveBar(cv);
        BuildOverlay(cv);
        BuildPopup(cv);
        BuildVisibleCategoryList();

        Debug.Log($"[SharedJewelryUI] Built for mode: {sceneMode}");
    }

    // ═════════════════════════════════════════════════════════════════
    //  TOP BAR
    // ═════════════════════════════════════════════════════════════════
    void BuildTopBar(RectTransform cv)
    {
        var bar = MkPanel("TopBar", cv, C_TOP,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, TOP_H), Vector2.zero);

        // ── Back button (left) ─────────────────────────────────────
        var back = MkPanel("BackBtn", Rt(bar), C_BTN,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(BTN_SIDE, 0), Vector2.zero);
        MkText(back, "< BACK", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(back, GoToMainMenu);

        // ── Capture button (right) ──────────────────────────────────
        var cap = MkPanel("CaptureBtn", Rt(bar), C_BTN,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(BTN_SIDE, 0), Vector2.zero);
        MkText(cap, "[ ]", 28, FontStyle.Bold, C_GOLD, TextAnchor.MiddleCenter);
        MkBtn(cap, TakeScreenshot);

        // ── Scene title + category label (centre) ───────────────────
        var centre = MkPanel("Centre", Rt(bar), new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(centre).offsetMin = new Vector2(BTN_SIDE + 4f, 0);
        Rt(centre).offsetMax = new Vector2(-(BTN_SIDE + 4f), 0);

        // Brand tag top-right of centre area
        var brand = MkPanel("Brand", Rt(centre), new Color(0, 0, 0, 0),
            new Vector2(1, 0.5f), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(120, 0), Vector2.zero);
        MkText(brand, "SDS", 24, FontStyle.Bold, C_GOLD, TextAnchor.MiddleRight);

        // Scene title (top half)
        var titleGo = MkPanel("Title", Rt(centre), new Color(0, 0, 0, 0),
            new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(titleGo).offsetMax = new Vector2(-130, 0); // leave room for SDS brand
        MkText(titleGo, sceneTitle.ToUpper(), 28, FontStyle.Bold,
               new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft, pad: 12f);

        // Category label (bottom half) — "SELECT JEWELLERY  ▼  Earrings"
        var catRow = MkPanel("CatRow", Rt(centre), C_LABEL,
            new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 0));
        Rt(catRow).offsetMin = new Vector2(0, 6);
        Rt(catRow).offsetMax = new Vector2(0, -2);

        // "SELECT JEWELLERY" label + dropdown arrow
        MkText(catRow, "SELECT JEWELLERY  ▼", 20, FontStyle.Normal,
               new Color(0.60f, 0.60f, 0.65f), TextAnchor.MiddleLeft, pad: 12f);
        MkBtn(catRow, TogglePopup);

        // active category name shown right-side
        var catNameGo = MkPanel("CatName", Rt(catRow), new Color(0, 0, 0, 0),
            new Vector2(0.4f, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            Vector2.zero, Vector2.zero);
        _catLabel = MkText(catNameGo, "— tap to select —", 20, FontStyle.Bold,
                           C_GOLD, TextAnchor.MiddleRight, pad: 12f);
    }

    // ═════════════════════════════════════════════════════════════════
    //  BOTTOM ITEM STRIP (horizontal scroll)
    // ═════════════════════════════════════════════════════════════════
    void BuildItemStrip(RectTransform cv)
    {
        float bottomOffset = (sceneMode != Mode.Scene360) ? REMOVE_H : 0f;

        var strip = MkPanel("ItemStrip", cv, C_STRIP,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, STRIP_H), new Vector2(0, bottomOffset));
        var srt = Rt(strip);

        var sr = strip.AddComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = false;
        sr.inertia = true; sr.decelerationRate = 0.12f;
        sr.scrollSensitivity = 60f;

        var vpGo = new GameObject("VP", typeof(RectTransform));
        vpGo.transform.SetParent(srt, false);
        var vpRt = vpGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.pivot = Vector2.zero;
        vpRt.sizeDelta = Vector2.zero;
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

    // ═════════════════════════════════════════════════════════════════
    //  REMOVE ALL BAR (hidden in 360 mode)
    // ═════════════════════════════════════════════════════════════════
    void BuildRemoveBar(RectTransform cv)
    {
        var bar = MkPanel("RemoveBar", cv, C_REMOVE,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, REMOVE_H), Vector2.zero);
        MkText(bar, "✕  REMOVE ALL", 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(bar, () =>
        {
            if (jewelryManager != null) jewelryManager.RemoveAll();
        });
    }

    // ═════════════════════════════════════════════════════════════════
    //  OVERLAY
    // ═════════════════════════════════════════════════════════════════
    void BuildOverlay(RectTransform cv)
    {
        _overlay = MkPanel("Overlay", cv, C_OVERLAY,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        _overlay.SetActive(false);
        MkBtn(_overlay, ClosePopup);
    }

    // ═════════════════════════════════════════════════════════════════
    //  CATEGORY POPUP
    // ═════════════════════════════════════════════════════════════════
    void BuildPopup(RectTransform cv)
    {
        _popupRoot = MkPanel("Popup", cv, C_POPUP_BG,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(POPUP_W, 0), Vector2.zero);
        Rt(_popupRoot).offsetMin = new Vector2(0, STRIP_H);
        Rt(_popupRoot).offsetMax = new Vector2(POPUP_W, -TOP_H);

        // Gold left-border accent
        var bdr = MkPanel("Border", Rt(_popupRoot), C_GOLD,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(4, 0), Vector2.zero);
        bdr.GetComponent<Button>()?.onClick.RemoveAllListeners();

        if (jewelryManager == null || jewelryManager.categories == null)
        {
            _popupRoot.SetActive(false);
            return;
        }

        // Build a row per category
        float y = -ROW_GAP;
        for (int i = 0; i < jewelryManager.categories.Length; i++)
        {
            var cat = jewelryManager.categories[i];
            int ci = i;

            // 360 View skips hand-jewelry categories
            if (sceneMode == Mode.Scene360 &&
                (cat.type == JewelryType.Bangle || cat.type == JewelryType.Ring))
                continue;

            var row = MkPanel("Row_" + i, Rt(_popupRoot), C_ROW_OFF,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(-12, ROW_H), new Vector2(0, y));
            Rt(row).offsetMin = new Vector2(8, 0);
            Rt(row).offsetMax = new Vector2(-12, 0);
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

    // ═════════════════════════════════════════════════════════════════
    //  Compute which category indices are visible for THIS scene mode
    // ═════════════════════════════════════════════════════════════════
    void BuildVisibleCategoryList()
    {
        _visibleCatIndices.Clear();
        if (jewelryManager == null || jewelryManager.categories == null) return;

        for (int i = 0; i < jewelryManager.categories.Length; i++)
        {
            var type = jewelryManager.categories[i].type;
            if (sceneMode == Mode.Scene360 &&
                (type == JewelryType.Bangle || type == JewelryType.Ring))
                continue;
            _visibleCatIndices.Add(i);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  POPUP TOGGLE
    // ═════════════════════════════════════════════════════════════════
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

    // ═════════════════════════════════════════════════════════════════
    //  CATEGORY SELECTED
    // ═════════════════════════════════════════════════════════════════
    void OnCategoryTapped(int catIdx)
    {
        if (jewelryManager == null) return;
        if (catIdx < 0 || catIdx >= jewelryManager.categories.Length) return;

        _activeCat = catIdx;
        var cat = jewelryManager.categories[catIdx];

        // Camera switch (TryOn only — JewelryManager handles it)
        if (sceneMode == Mode.TryOn)
            jewelryManager.OnCategorySelected(catIdx);

        // Update label
        if (_catLabel != null) _catLabel.text = cat.categoryName.ToUpper();

        // Highlight rows
        // Row index differs from catIdx because 360 skips some categories.
        // Match by rebuilding row highlight based on _visibleCatIndices.
        int rowIdx = _visibleCatIndices.IndexOf(catIdx);
        for (int r = 0; r < _rowBgs.Count; r++)
        {
            bool on = (r == rowIdx);
            if (_rowBgs[r]) _rowBgs[r].color = on ? C_ROW_ON : C_ROW_OFF;
            if (_rowTxts[r]) _rowTxts[r].color = on
                ? new Color(0.05f, 0.05f, 0.05f) : Color.white;
        }

        ClosePopup();
        StartCoroutine(SpawnCards(cat));
    }

    // ═════════════════════════════════════════════════════════════════
    //  SPAWN ITEM CARDS
    // ═════════════════════════════════════════════════════════════════
    IEnumerator SpawnCards(JewelryCategory cat)
    {
        foreach (var c in _cards) if (c) Destroy(c);
        _cards.Clear();
        _stripContent.sizeDelta = new Vector2(0, CARD_H);

        yield return null;

        if (cat.items == null || cat.items.Length == 0)
        {
            Debug.LogWarning($"[SharedJewelryUI] No items in '{cat.categoryName}'");
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
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(CARD_W, CARD_H), new Vector2(xP, 0));

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
            }

            // Name strip
            var ns = MkPanel("NameStrip", Rt(card), new Color(0, 0, 0, 0.88f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, 48), Vector2.zero);
            MkText(ns, item.itemName, 19, FontStyle.Bold,
                   Color.white, TextAnchor.MiddleCenter);

            // Click → equip
            Image cardImg = card.GetComponent<Image>();
            MkBtn(card, () =>
            {
                foreach (var cc in _cards)
                    if (cc) cc.GetComponent<Image>().color = C_CARD_BG;
                cardImg.color = C_CARD_SEL;

                if (sceneMode == Mode.TryOn || sceneMode == Mode.PlaceOnRoom)
                {
                    if (jewelryManager != null)
                        jewelryManager.EquipJewelryByIndex(_activeCat, ci);
                }
                else // 360 View
                {
                    On360ItemSelected(_activeCat, ci);
                }
            });

            _cards.Add(card);
        }

        Debug.Log($"[SharedJewelryUI] {_cards.Count} cards for '{cat.categoryName}'");
    }

    // ═════════════════════════════════════════════════════════════════
    //  360 VIEW — item selection hook
    //  Override or expand this for your 3D rotation viewer logic.
    // ═════════════════════════════════════════════════════════════════
    protected virtual void On360ItemSelected(int catIdx, int itemIdx)
    {
        if (jewelryManager == null) return;
        // In 360 mode, show the 3D prefab in the rotation viewer.
        // Find your Jewelry3DViewer component and call its display method.
        var viewer = FindObjectOfType<Jewelry3DViewer>();
        if (viewer != null)
        {
            var item = jewelryManager.categories[catIdx].items[itemIdx];
            viewer.DisplayItem(item);
        }
        else
        {
            Debug.LogWarning("[SharedJewelryUI] Jewelry3DViewer not found in scene.");
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  BACK → MAIN MENU
    //
    //  UPDATED: Resets and stops the AR session before loading the main
    //  menu so the next AR scene always starts with a clean camera state.
    //  Without this, back-camera state from JewelryARScene would bleed
    //  into FaceTryOn (and vice-versa).
    // ═════════════════════════════════════════════════════════════════
    void GoToMainMenu()
    {
        // 1. Clean up all active jewelry / pending coroutines.
        if (jewelryManager != null)
            jewelryManager.RemoveAll();

        // 2. Reset & stop the AR session BEFORE loading the new scene.
        //    Priority: use ARSessionResetter if present (recommended),
        //    otherwise fall back to direct ARSession manipulation.
        var resetter = FindObjectOfType<ARSessionResetter>();
        if (resetter != null)
        {
            resetter.ResetAndStopSession();
        }
        else
        {
            // Fallback path — works even without ARSessionResetter attached.
            var arSession = FindObjectOfType<ARSession>();
            if (arSession != null)
            {
                arSession.Reset();
                arSession.enabled = false;
                Debug.Log("[SharedJewelryUI] ARSession reset on Back (fallback path).");
            }
        }

        // 3. Load the main menu.
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ═════════════════════════════════════════════════════════════════
    //  SCREENSHOT
    // ═════════════════════════════════════════════════════════════════
    void TakeScreenshot()
    {
        string fileName = $"SDS_Jewelry_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

#if UNITY_ANDROID || UNITY_IOS
        string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        ScreenCapture.CaptureScreenshot(fileName);
        Debug.Log($"[SharedJewelryUI] Screenshot saved: {fileName}");
#else
        ScreenCapture.CaptureScreenshot(fileName);
        Debug.Log($"[SharedJewelryUI] Screenshot: {fileName}");
#endif
    }

    // ═════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════════
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

/// <summary>
/// Stub interface for your 360 View scene's 3D item display component.
/// Replace with your actual implementation in Jewelry3DScene.
/// </summary>
public class Jewelry3DViewer : MonoBehaviour
{
    [Tooltip("Root transform where the 3D jewelry model is placed for rotation preview")]
    public Transform displayMount;

    private GameObject _current;

    public void DisplayItem(JewelryItem item)
    {
        if (_current != null) Destroy(_current);
        if (item?.jewelryPrefab == null) return;
        _current = Instantiate(item.jewelryPrefab, displayMount);
        _current.transform.localPosition = Vector3.zero;
        _current.transform.localRotation = Quaternion.identity;
        Debug.Log($"[Jewelry3DViewer] Showing: {item.itemName}");
    }
}