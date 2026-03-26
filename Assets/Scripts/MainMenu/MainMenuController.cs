using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// MainMenuController v2 — BUG FIX: buttons now navigate correctly.
///
/// ROOT CAUSE of "button click does nothing":
///   Child Image panels (Accent, IconBg, Label, Sub, Arrow, Divider, BG)
///   all had raycastTarget = true by default. They sat on top of the Button
///   and swallowed all pointer events before they reached the Button component.
///
/// FIX:
///   • MkPanel has a new optional param: raycast (default false).
///   • Only the root button panel has raycast:true — every decorative
///     child panel has raycast:false so clicks pass through to the Button.
///   • Full-screen BG panel also raycast:false.
///   • EventSystem auto-created if missing.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names (must match Build Settings exactly)")]
    public string scene360View = "Jewelry3DScene";
    public string scenePlaceOnRoom = "JewelryARScene";
    public string sceneTryOn = "FaceTryOn";

    // ── Colors ────────────────────────────────────────────────────────
    private static readonly Color C_BG = new Color(0.05f, 0.08f, 0.12f, 1f);
    private static readonly Color C_TOP = new Color(0.06f, 0.06f, 0.09f, 0.97f);
    private static readonly Color C_GOLD = new Color(0.95f, 0.80f, 0.25f, 1f);
    private static readonly Color C_BTN_BG = new Color(0.12f, 0.12f, 0.18f, 1f);
    private static readonly Color C_BTN_LINE = new Color(0.25f, 0.22f, 0.12f, 1f);
    private static readonly Color C_ICON_BG = new Color(0.18f, 0.16f, 0.08f, 1f);
    private static readonly Color C_SUB = new Color(0.55f, 0.55f, 0.60f, 1f);

    // ── Layout (ref 1080x1920) ────────────────────────────────────────
    private const float TOP_H = 130f;
    private const float BTN_H = 160f;
    private const float BTN_GAP = 24f;
    private const float BTN_PAD = 60f;

    private Font _font;

    void Start()
    {
        // Auto-create EventSystem if missing — without it no button fires
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.LogWarning("[MainMenuController] EventSystem was missing — created one.");
        }
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        yield return new WaitForSeconds(0.05f);
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Canvas
        var cgo = new GameObject("MenuCanvas");
        var cvs = cgo.AddComponent<Canvas>();
        cvs.renderMode = RenderMode.ScreenSpaceOverlay;
        cvs.sortingOrder = 100;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        var cv = cvs.GetComponent<RectTransform>();

        // Full-screen background — raycast:false so it never blocks buttons
        MkPanel("BG", cv, C_BG,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, raycast: false);

        // Top bar
        var topBar = MkPanel("TopBar", cv, C_TOP,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, TOP_H), Vector2.zero);
        MkTMP(topBar, "SDS", 36, FontStyles.Bold, C_GOLD,
              HorizontalAlignmentOptions.Left, pad: 40f);
        var titleGo = MkPanel("SubTitle", Rt(topBar), new Color(0, 0, 0, 0),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Rt(titleGo).offsetMin = new Vector2(120, 0);
        MkTMP(titleGo, "JEWELLERY TRY-ON", 28, FontStyles.Normal,
              new Color(0.75f, 0.75f, 0.75f), HorizontalAlignmentOptions.Left);

        // Eyebrow label
        float totalBtnsH = 3 * BTN_H + 2 * BTN_GAP;
        var eyebrow = MkPanel("Eyebrow", cv, new Color(0, 0, 0, 0),
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0),
            new Vector2(0, 50), new Vector2(0, totalBtnsH * 0.5f + BTN_GAP + 50));
        MkTMP(eyebrow, "Try Jewellery Experience On", 30, FontStyles.Normal,
              C_SUB, HorizontalAlignmentOptions.Center);

        // Three buttons
        BuildMenuButton(cv, "360\u00b0 View", "Rotate & explore in 3D", "\u25ce", 0, totalBtnsH, () => LoadScene(scene360View));
        BuildMenuButton(cv, "Place On Room", "Visualise in your real space", "\u229f", 1, totalBtnsH, () => LoadScene(scenePlaceOnRoom));
        BuildMenuButton(cv, "Try-On", "Face & hand AR jewellery", "\u25c9", 2, totalBtnsH, () => LoadScene(sceneTryOn));

        // Bottom brand bar
        var bottom = MkPanel("BottomBar", cv, C_TOP,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 70), Vector2.zero);
        MkTMP(bottom, "SDS JEWELLERY \u00a9 2025", 20, FontStyles.Normal,
              new Color(0.35f, 0.35f, 0.40f), HorizontalAlignmentOptions.Center);
    }

    // ════════════════════════════════════════════════════════════════
    //  BUILD MENU BUTTON
    //  THE KEY FIX IS HERE:
    //    root btn  → raycast: true   (receives the click)
    //    all kids  → raycast: false  (invisible to pointer events)
    // ════════════════════════════════════════════════════════════════
    void BuildMenuButton(RectTransform cv, string label, string sub,
                         string icon, int index, float totalH,
                         UnityEngine.Events.UnityAction onClick)
    {
        float yOff = (totalH * 0.5f) - index * (BTN_H + BTN_GAP) - BTN_H * 0.5f;

        // ROOT — only panel that receives pointer events
        var btn = MkPanel("Btn_" + label, cv, C_BTN_BG,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, BTN_H), new Vector2(0, yOff),
            raycast: true);                          // ← TRUE: clickable
        Rt(btn).offsetMin = new Vector2(BTN_PAD, 0);
        Rt(btn).offsetMax = new Vector2(-BTN_PAD, 0);
        Rt(btn).anchoredPosition = new Vector2(0, yOff);
        Rt(btn).sizeDelta = new Vector2(0, BTN_H);

        // CHILDREN — all raycast:false so they never block the Button above
        MkPanel("Accent", Rt(btn), C_GOLD,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(6, 0), Vector2.zero, raycast: false);

        var ic = MkPanel("IconBg", Rt(btn), C_ICON_BG,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(90, 90), new Vector2(60, 0), raycast: false);
        MkTMP(ic, icon, 36, FontStyles.Bold, C_GOLD, HorizontalAlignmentOptions.Center);

        var lbGo = MkPanel("Label", Rt(btn), new Color(0, 0, 0, 0),
            new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero, raycast: false);
        Rt(lbGo).offsetMin = new Vector2(170, 0);
        Rt(lbGo).offsetMax = new Vector2(-20, 0);
        MkTMP(lbGo, label, 38, FontStyles.Bold,
              new Color(0.92f, 0.88f, 0.80f), HorizontalAlignmentOptions.Left);

        var sbGo = MkPanel("Sub", Rt(btn), new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero, raycast: false);
        Rt(sbGo).offsetMin = new Vector2(170, 0);
        Rt(sbGo).offsetMax = new Vector2(-20, 0);
        MkTMP(sbGo, sub, 24, FontStyles.Normal, C_SUB, HorizontalAlignmentOptions.Left);

        var ch = MkPanel("Arrow", Rt(btn), new Color(0, 0, 0, 0),
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(60, 0), Vector2.zero, raycast: false);
        MkTMP(ch, ">", 40, FontStyles.Bold, C_GOLD, HorizontalAlignmentOptions.Center);

        MkPanel("Divider", Rt(btn), C_BTN_LINE,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(-20, 1), Vector2.zero, raycast: false);

        // Button component — added LAST, wired to root Image
        var b = btn.AddComponent<Button>();
        b.targetGraphic = btn.GetComponent<Image>();
        var cols = b.colors;
        cols.normalColor = Color.white;
        cols.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
        cols.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        b.colors = cols;
        b.onClick.AddListener(onClick);
    }

    // ════════════════════════════════════════════════════════════════
    void LoadScene(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("[MainMenuController] Scene name empty — check Inspector.");
            return;
        }
        Debug.Log($"[MainMenuController] Loading: {name}");
        SceneManager.LoadScene(name);
    }

    RectTransform Rt(GameObject go) => go.GetComponent<RectTransform>();

    // raycast defaults to FALSE — only root interactive panels pass true
    GameObject MkPanel(string name, RectTransform parent, Color color,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
        Vector2 size, Vector2 pos, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;   // ← THE FIX
        return go;
    }

    void MkTMP(GameObject parent, string text, int size, FontStyles style,
               Color color, HorizontalAlignmentOptions align, float pad = 0f)
    {
        var go = new GameObject("T", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, 4f); rt.offsetMax = new Vector2(-8f, -4f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.horizontalAlignment = align;
        t.verticalAlignment = VerticalAlignmentOptions.Middle;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Truncate;
    }
}