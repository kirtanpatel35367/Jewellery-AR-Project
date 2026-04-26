using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// SharedJewelryUI — single UI script that works across all three scenes.
///
///   Scene360      → 360 View  (Jewelry3DScene)
///   PlaceOnRoom   → AR plane placement (JewelryARScene)
///   TryOn         → Face / hand AR (FaceTryOnScene)
///
/// ── BUG FIX v4 ──────────────────────────────────────────────────────
///   Root cause of AR scene showing no UI: IsCategoryVisibleInMode() for
///   PlaceOnRoom mode ONLY showed JewelryType.PlaceOnRoom categories.
///   If your categories used any other JewelryType they were silently
///   filtered out, leaving the popup with zero rows.
///
///   FIX: PlaceOnRoom mode now shows ALL category types. The JewelryManager
///   and PlacementManager handle placement regardless of JewelryType in
///   that scene. You can optionally restrict this per-project by setting
///   restrictARSceneToPlaceOnRoomType = true in the Inspector.
///
/// ── HOW TO SET UP ───────────────────────────────────────────────────
///   1. Add an empty GameObject called "SharedUI" in each of your 3 scenes.
///   2. Attach this script and set SceneMode in the Inspector.
///   3. Assign JewelryManager (required in PlaceOnRoom + TryOn scenes).
///   4. (Optional) assign a CaptureCamera for screenshot.
///
/// ── FEATURES ────────────────────────────────────────────────────────
///   • Top bar: Back button + scene title + SDS brand + screenshot button
///   • Bottom strip: horizontal item card scroll (per selected category)
///   • Category popup (side drawer): tap "SELECT JEWELLERY ▼" to open
///   • Remove All button (hidden in 360 mode)
///   • Remove Selected button (PlaceOnRoom mode only — shown when item selected)
///   • Placement hint banner ("Tap a surface to place") fades after first place
///   • AR scanning overlay ("Scanning for surfaces…") fades once plane found
/// </summary>
public class SharedJewelryUI : MonoBehaviour
{
    // ── Scene identity ─────────────────────────────────────────────────
    public enum Mode { Scene360, PlaceOnRoom, TryOn }

    [Header("Scene Setup")]
    public Mode sceneMode = Mode.TryOn;
    public string sceneTitle = "Try-On";

    [Header("References")]
    public JewelryManager jewelryManager;
    public string mainMenuSceneName = "MainMenuScene";
    public Camera captureCamera;

    [Header("PlaceOnRoom Options")]
    [Tooltip("If true, the AR scene popup only shows categories with JewelryType.PlaceOnRoom.\n" +
             "If false (default), ALL categories are shown and all route to PlacementManager.")]
    public bool restrictARSceneToPlaceOnRoomType = false;

    // ── Layout constants (ref 1080×1920) ──────────────────────────────
    private const float TOP_H = 130f;
    private const float STRIP_H = 300f;
    private const float REMOVE_H = 72f;
    private const float CARD_W = 245f;
    private const float CARD_H = 265f;
    private const float CARD_GAP = 10f;
    private const float POPUP_W = 500f;
    private const float ROW_H = 88f;
    private const float ROW_GAP = 5f;
    private const float BTN_SIDE = 90f;
    private const float HINT_H = 80f;

    // ── Colors ─────────────────────────────────────────────────────────
    private static readonly Color C_TOP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_STRIP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_GOLD = new Color(0.95f, 0.80f, 0.25f, 1.00f);
    private static readonly Color C_LABEL = new Color(0.13f, 0.13f, 0.18f, 1.00f);
    private static readonly Color C_BTN = new Color(0.18f, 0.18f, 0.26f, 1.00f);
    private static readonly Color C_REMOVE = new Color(0.58f, 0.10f, 0.10f, 1.00f);
    private static readonly Color C_REM_SEL = new Color(0.80f, 0.40f, 0.05f, 1.00f);
    private static readonly Color C_POPUP_BG = new Color(0.10f, 0.10f, 0.14f, 0.99f);
    private static readonly Color C_ROW_OFF = new Color(0.18f, 0.18f, 0.25f, 1.00f);
    private static readonly Color C_ROW_ON = new Color(0.88f, 0.65f, 0.10f, 1.00f);
    private static readonly Color C_CARD_BG = new Color(0.14f, 0.14f, 0.20f, 1.00f);
    private static readonly Color C_CARD_SEL = new Color(0.90f, 0.70f, 0.15f, 1.00f);
    private static readonly Color C_OVERLAY = new Color(0.00f, 0.00f, 0.00f, 0.50f);
    private static readonly Color C_HINT_BG = new Color(0.05f, 0.05f, 0.08f, 0.90f);

    // ── Runtime state ──────────────────────────────────────────────────
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

    private List<int> _visibleCatIndices = new List<int>();

    // PlaceOnRoom extras
    private GameObject _hintBanner;
    private Text _hintText;
    private bool _hintDismissed;
    private GameObject _removeSel;

    // ═════════════════════════════════════════════════════════════════
    //  BUILD
    // ═════════════════════════════════════════════════════════════════

    void Start() => StartCoroutine(Build());

    IEnumerator Build()
    {
        yield return new WaitForSeconds(0.15f);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) { Debug.LogError("[SharedJewelryUI] Built-in font not found!"); yield break; }

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
        if (sceneMode != Mode.Scene360) BuildRemoveBar(cv);
        BuildOverlay(cv);
        BuildPopup(cv);
        BuildVisibleCategoryList();

        if (sceneMode == Mode.PlaceOnRoom)
        {
            BuildHintBanner(cv);
            WireUpPlacementEvents();
        }

        Debug.Log("[SharedJewelryUI] Built for mode: " + sceneMode +
                  " | visible categories: " + _visibleCatIndices.Count);
    }

    // ═════════════════════════════════════════════════════════════════
    //  TOP BAR
    // ═════════════════════════════════════════════════════════════════

    void BuildTopBar(RectTransform cv)
    {
        var bar = MkPanel("TopBar", cv, C_TOP,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, TOP_H), Vector2.zero);

        var back = MkPanel("BackBtn", Rt(bar), C_BTN,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(BTN_SIDE, 0), Vector2.zero);
        MkText(back, "< BACK", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        MkBtn(back, GoToMainMenu);

        // Screenshot button removed

        var centre = MkPanel("Centre", Rt(bar), new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(centre).offsetMin = new Vector2(BTN_SIDE + 4f, 0);
        Rt(centre).offsetMax = new Vector2(-4f, 0);

        var brand = MkPanel("Brand", Rt(centre), new Color(0, 0, 0, 0),
            new Vector2(1, 0.5f), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(120, 0), Vector2.zero);
        MkText(brand, "SDS", 24, FontStyle.Bold, C_GOLD, TextAnchor.MiddleRight);

        var titleGo = MkPanel("Title", Rt(centre), new Color(0, 0, 0, 0),
            new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(titleGo).offsetMax = new Vector2(-130, 0);
        MkText(titleGo, sceneTitle.ToUpper(), 28, FontStyle.Bold,
               new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft, pad: 12f);

        var catRow = MkPanel("CatRow", Rt(centre), C_LABEL,
            new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(catRow).offsetMin = new Vector2(0, 6);
        Rt(catRow).offsetMax = new Vector2(0, -2);

        MkText(catRow, "SELECT JEWELLERY  \u25BC", 20, FontStyle.Normal,
               new Color(0.60f, 0.60f, 0.65f), TextAnchor.MiddleLeft, pad: 12f);
        MkBtn(catRow, TogglePopup);

        var catNameGo = MkPanel("CatName", Rt(catRow), new Color(0, 0, 0, 0),
            new Vector2(0.4f, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            Vector2.zero, Vector2.zero);
        _catLabel = MkText(catNameGo, "\u2014 tap to select \u2014", 20, FontStyle.Bold,
                           C_GOLD, TextAnchor.MiddleRight, pad: 12f);
    }

    // ═════════════════════════════════════════════════════════════════
    //  BOTTOM ITEM STRIP
    // ═════════════════════════════════════════════════════════════════

    void BuildItemStrip(RectTransform cv)
    {
        float bottomOffset = (sceneMode != Mode.Scene360) ? REMOVE_H : 0f;
        if (sceneMode == Mode.PlaceOnRoom) bottomOffset += HINT_H;

        var strip = MkPanel("ItemStrip", cv, C_STRIP,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, STRIP_H), new Vector2(0, bottomOffset));

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

    // ═════════════════════════════════════════════════════════════════
    //  REMOVE BARS
    // ═════════════════════════════════════════════════════════════════

    void BuildRemoveBar(RectTransform cv)
    {
        // Remove All
        float removeAllY = (sceneMode == Mode.PlaceOnRoom) ? HINT_H : 0f;
        var bar = MkPanel("RemoveBar", cv, C_REMOVE,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, REMOVE_H), new Vector2(0, removeAllY));

        if (sceneMode == Mode.PlaceOnRoom)
        {
            // Split: left = Remove Selected, right = Remove All
            var remSelGo = MkPanel("RemSel", Rt(bar), C_REM_SEL,
                Vector2.zero, new Vector2(0.5f, 1), new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero);
            Rt(remSelGo).offsetMax = new Vector2(-1, 0);
            MkText(remSelGo, "\u2715 Remove Selected", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            MkBtn(remSelGo, RemoveSelected);
            _removeSel = remSelGo;

            var remAllGo = MkPanel("RemAll", Rt(bar), C_REMOVE,
                new Vector2(0.5f, 0), Vector2.one, new Vector2(1, 0.5f),
                Vector2.zero, Vector2.zero);
            MkText(remAllGo, "\u2715 Remove All", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            MkBtn(remAllGo, () => { if (jewelryManager != null) jewelryManager.RemoveAll(); });
        }
        else
        {
            MkText(bar, "\u2715  REMOVE ALL", 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            MkBtn(bar, () => { if (jewelryManager != null) jewelryManager.RemoveAll(); });
        }
    }

    void RemoveSelected()
    {
        if (jewelryManager != null && jewelryManager.placementManager != null)
            jewelryManager.placementManager.RemoveSelected();
    }

    // ═════════════════════════════════════════════════════════════════
    //  PLACEMENT HINT BANNER  (PlaceOnRoom only)
    // ═════════════════════════════════════════════════════════════════

    void BuildHintBanner(RectTransform cv)
    {
        _hintBanner = MkPanel("HintBanner", cv, C_HINT_BG,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, HINT_H), Vector2.zero);
        _hintText = MkText(_hintBanner,
            "\U0001F4F1 Scanning for surfaces... move your device slowly",
            24, FontStyle.Normal, new Color(0.75f, 0.75f, 0.80f),
            TextAnchor.MiddleCenter);
    }

    void WireUpPlacementEvents()
    {
        if (jewelryManager == null || jewelryManager.placementManager == null) return;
        var pm = jewelryManager.placementManager;
        pm.OnItemPlaced += () =>
        {
            if (!_hintDismissed)
            {
                _hintDismissed = true;
                StartCoroutine(FadeOutHint());
            }
        };
    }

    IEnumerator FadeOutHint()
    {
        if (_hintBanner == null) yield break;
        var img = _hintBanner.GetComponent<Image>();
        float t = 0f;
        float dur = 0.6f;
        Color startC = img.color;
        Color startT = _hintText.color;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = 1f - (t / dur);
            img.color = new Color(startC.r, startC.g, startC.b, startC.a * a);
            _hintText.color = new Color(startT.r, startT.g, startT.b, startT.a * a);
            yield return null;
        }
        _hintBanner.SetActive(false);
    }

    /// <summary>
    /// Call from ARPlaneManager.planesChanged to update the scanning hint text.
    /// Wire this up in your ARScene controller script.
    /// </summary>
    public void NotifyPlanesDetected(int count)
    {
        if (_hintText == null || _hintDismissed) return;
        _hintText.text = count > 0
            ? "\u2713 Surface ready — tap to place jewelry"
            : "\U0001F4F1 Scanning for surfaces... move your device slowly";
        _hintText.color = count > 0
            ? new Color(0.55f, 0.90f, 0.55f)
            : new Color(0.75f, 0.75f, 0.80f);
    }

    // ═════════════════════════════════════════════════════════════════
    //  OVERLAY + POPUP
    // ═════════════════════════════════════════════════════════════════

    void BuildOverlay(RectTransform cv)
    {
        _overlay = MkPanel("Overlay", cv, C_OVERLAY,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        _overlay.SetActive(false);
        MkBtn(_overlay, ClosePopup);
    }

    void BuildPopup(RectTransform cv)
    {
        _popupRoot = MkPanel("Popup", cv, C_POPUP_BG,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(POPUP_W, 0), Vector2.zero);
        Rt(_popupRoot).offsetMin = new Vector2(0, STRIP_H);
        Rt(_popupRoot).offsetMax = new Vector2(POPUP_W, -TOP_H);

        MkPanel("Border", Rt(_popupRoot), C_GOLD,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(4, 0), Vector2.zero);

        if (jewelryManager == null || jewelryManager.categories == null)
        { _popupRoot.SetActive(false); return; }

        float y = -ROW_GAP;
        for (int i = 0; i < jewelryManager.categories.Length; i++)
        {
            var cat = jewelryManager.categories[i];
            int ci = i;

            // ── BUG FIX: use the corrected filter ──
            if (!IsCategoryVisibleInMode(cat.type)) continue;

            var row = MkPanel("Row_" + i, Rt(_popupRoot), C_ROW_OFF,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, ROW_H), new Vector2(0, y));
            Rt(row).offsetMin = new Vector2(8, 0);
            Rt(row).offsetMax = new Vector2(-12, 0);
            Rt(row).anchoredPosition = new Vector2(0, y);
            Rt(row).sizeDelta = new Vector2(0, ROW_H);

            _rowBgs.Add(row.GetComponent<Image>());
            _rowTxts.Add(MkText(row, cat.categoryName, 30, FontStyle.Bold,
                                Color.white, TextAnchor.MiddleLeft, pad: 20f));
            MkBtn(row, () => OnCategoryTapped(ci));

            y -= ROW_H + ROW_GAP;
        }

        // Warn if no categories were shown
        if (_rowBgs.Count == 0)
        {
            Debug.LogWarning("[SharedJewelryUI] No categories visible for mode: " + sceneMode +
                "\nCheck that your JewelryCategory types match the scene mode, " +
                "or set 'restrictARSceneToPlaceOnRoomType = false'.");
        }

        _popupRoot.SetActive(false);
    }

    // ── Category visibility filter ─────────────────────────────────────
    //
    //  ROOT BUG FIX IS HERE:
    //  PlaceOnRoom mode previously returned true ONLY for JewelryType.PlaceOnRoom.
    //  Default is now to show ALL categories and let PlacementManager handle placement.
    //  Toggle restrictARSceneToPlaceOnRoomType = true in Inspector to opt back in.
    //
    bool IsCategoryVisibleInMode(JewelryType type)
    {
        switch (sceneMode)
        {
            case Mode.Scene360:
                return type == JewelryType.Earrings || type == JewelryType.Necklace;

            case Mode.PlaceOnRoom:
                // FIX: show all categories unless explicitly restricted
                if (restrictARSceneToPlaceOnRoomType)
                    return type == JewelryType.PlaceOnRoom;
                return true;   // ← was 'return type == JewelryType.PlaceOnRoom;' — this was the bug

            case Mode.TryOn:
            default:
                return type == JewelryType.Earrings
                    || type == JewelryType.Necklace
                    || type == JewelryType.Bangle
                    || type == JewelryType.Ring;
        }
    }

    void BuildVisibleCategoryList()
    {
        _visibleCatIndices.Clear();
        if (jewelryManager == null || jewelryManager.categories == null) return;
        for (int i = 0; i < jewelryManager.categories.Length; i++)
            if (IsCategoryVisibleInMode(jewelryManager.categories[i].type))
                _visibleCatIndices.Add(i);
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
    //  CATEGORY TAPPED
    // ═════════════════════════════════════════════════════════════════

    void OnCategoryTapped(int catIdx)
    {
        if (jewelryManager == null) return;
        if (catIdx < 0 || catIdx >= jewelryManager.categories.Length) return;

        _activeCat = catIdx;
        var cat = jewelryManager.categories[catIdx];

        if (sceneMode == Mode.TryOn || sceneMode == Mode.PlaceOnRoom)
            jewelryManager.OnCategorySelected(catIdx);

        if (_catLabel != null) _catLabel.text = cat.categoryName.ToUpper();

        int rowIdx = _visibleCatIndices.IndexOf(catIdx);
        for (int r = 0; r < _rowBgs.Count; r++)
        {
            bool on = (r == rowIdx);
            if (_rowBgs[r]) _rowBgs[r].color = on ? C_ROW_ON : C_ROW_OFF;
            if (_rowTxts[r]) _rowTxts[r].color = on ? new Color(0.05f, 0.05f, 0.05f) : Color.white;
        }

        ClosePopup();
        StartCoroutine(SpawnCards(cat));
    }

    // ═════════════════════════════════════════════════════════════════
    //  ITEM CARDS
    // ═════════════════════════════════════════════════════════════════

    IEnumerator SpawnCards(JewelryCategory cat)
    {
        foreach (var c in _cards) if (c) Destroy(c);
        _cards.Clear();
        _stripContent.sizeDelta = new Vector2(0, CARD_H);
        yield return null;

        if (cat.items == null || cat.items.Length == 0)
        {
            Debug.LogWarning("[SharedJewelryUI] No items in '" + cat.categoryName + "'");
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

            var ns = MkPanel("NameStrip", Rt(card), new Color(0, 0, 0, 0.88f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, 48), Vector2.zero);
            MkText(ns, item.itemName, 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

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
                else
                {
                    On360ItemSelected(_activeCat, ci);
                }
            });

            _cards.Add(card);
        }

        Debug.Log("[SharedJewelryUI] " + _cards.Count + " cards for '" + cat.categoryName + "'");
    }

    // ═════════════════════════════════════════════════════════════════
    //  360 VIEW HANDLER
    // ═════════════════════════════════════════════════════════════════

    protected virtual void On360ItemSelected(int catIdx, int itemIdx)
    {
        if (jewelryManager == null) return;
        var viewer = FindObjectOfType<Jewelry3DViewer>();
        if (viewer != null)
            viewer.DisplayItem(jewelryManager.categories[catIdx].items[itemIdx]);
        else
            Debug.LogWarning("[SharedJewelryUI] Jewelry3DViewer not found in scene.");
    }

    // ═════════════════════════════════════════════════════════════════
    //  BACK BUTTON
    // ═════════════════════════════════════════════════════════════════

    void GoToMainMenu()
    {
        if (jewelryManager != null) jewelryManager.RemoveAll();

        var resetter = FindObjectOfType<ARSessionResetter>();
        if (resetter != null)
        {
            resetter.ResetAndStopSession();
        }
        else
        {
            var arSession = FindObjectOfType<ARSession>();
            if (arSession != null)
            {
                arSession.Reset();
                arSession.enabled = false;
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ═════════════════════════════════════════════════════════════════
    //  SCREENSHOT
    // ═════════════════════════════════════════════════════════════════

    void TakeScreenshot()
    {
        string fileName = "SDS_Jewelry_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        ScreenCapture.CaptureScreenshot(fileName);
        Debug.Log("[SharedJewelryUI] Screenshot saved: " + fileName);
    }

    // ═════════════════════════════════════════════════════════════════
    //  UI BUILDER HELPERS
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
        t.text = text; t.font = _font; t.fontSize = size;
        t.fontStyle = style; t.color = color; t.alignment = align;
        t.raycastTarget = false;
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

// ═════════════════════════════════════════════════════════════════════
//  Jewelry3DViewer — 360 View scene model display stub.
// ═════════════════════════════════════════════════════════════════════
public class Jewelry3DViewer : MonoBehaviour
{
    [Tooltip("Root transform where the 3D model is placed for the rotation preview")]
    public Transform displayMount;

    private GameObject _current;

    public void DisplayItem(JewelryItem item)
    {
        if (_current != null) Destroy(_current);
        if (item == null || item.jewelryReference == null || !item.jewelryReference.RuntimeKeyIsValid()) return;

        item.jewelryReference.InstantiateAsync(displayMount).Completed += (op) => 
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                _current = op.Result;
                _current.transform.localPosition = Vector3.zero;
                _current.transform.localRotation = Quaternion.identity;
                Debug.Log("[Jewelry3DViewer] Displaying via Addressables: " + item.itemName);
            }
        };
    }
}
