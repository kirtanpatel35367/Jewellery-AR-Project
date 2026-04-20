using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// MainMenuController — Modern UI (Approved Mockup Version)
///
/// Design Features:
///   • Deep navy background (#080b10) with ambient floating particles
///   • Gold-pulsing brand logo tile (SDS) in top bar
///   • Hero section with eyebrow label + two-line styled title
///   • Per-button accent colors: Blue (360) / Green (Room) / Purple (TryOn)
///   • Geometric icon per button drawn from Unity UI panels
///   • Shimmer sweep animation cycling on each button
///   • Button press: scale punch + accent color flash
///   • Staggered slide-up entrance for all three buttons
///   • Gold separator bar under hero title
///   • Footer brand bar with top border line
///
/// BUG FIX RETAINED (from original v2):
///   Only root button panels have raycastTarget = true.
///   All decorative children have raycastTarget = false.
///   EventSystem auto-created if missing.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names (must match Build Settings exactly)")]
    public string scene360View     = "Jewelry3DScene";
    public string scenePlaceOnRoom = "JewelryARScene";
    public string sceneTryOn       = "FaceTryOn";

    // ── Colour Palette ────────────────────────────────────────────────
    private static readonly Color C_BG            = new Color(0.031f, 0.043f, 0.063f, 1f);
    private static readonly Color C_TOPBAR        = new Color(1f, 1f, 1f, 0.025f);
    private static readonly Color C_TOPBAR_BORDER = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color C_GOLD          = new Color(0.831f, 0.659f, 0.125f, 1f);
    private static readonly Color C_GOLD_BRIGHT   = new Color(0.941f, 0.753f, 0.188f, 1f);
    private static readonly Color C_GOLD_DIM      = new Color(0.831f, 0.659f, 0.125f, 0.55f);
    private static readonly Color C_WHITE_DIM     = new Color(1f, 1f, 1f, 0.38f);
    private static readonly Color C_BTN_CARD      = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color C_FOOTER_TEXT   = new Color(1f, 1f, 1f, 0.20f);
    private static readonly Color C_EYEBROW       = new Color(0.831f, 0.659f, 0.125f, 0.55f);

    // Per-button accent colours
    private static readonly Color C_BLUE_ACCENT   = new Color(0.157f, 0.337f, 0.831f, 1f);
    private static readonly Color C_BLUE_ICON     = new Color(0.157f, 0.337f, 0.831f, 0.18f);
    private static readonly Color C_GREEN_ACCENT  = new Color(0.094f, 0.627f, 0.353f, 1f);
    private static readonly Color C_GREEN_ICON    = new Color(0.094f, 0.627f, 0.353f, 0.18f);
    private static readonly Color C_PURPLE_ACCENT = new Color(0.565f, 0.188f, 0.816f, 1f);
    private static readonly Color C_PURPLE_ICON   = new Color(0.565f, 0.188f, 0.816f, 0.18f);

    // Icon tint colours (bright variants for visibility)
    private static readonly Color C_ICON_BLUE   = new Color(0.353f, 0.533f, 1.00f, 0.9f);
    private static readonly Color C_ICON_GREEN  = new Color(0.240f, 0.840f, 0.553f, 0.9f);
    private static readonly Color C_ICON_PURPLE = new Color(0.753f, 0.440f, 1.000f, 0.9f);
    // Inner mask colour for ring icons (matches background)
    private static readonly Color C_ICON_MASK   = new Color(0.094f, 0.094f, 0.125f, 1f);

    // ── Layout (reference 1080 × 1920) ───────────────────────────────
    private const float REF_W       = 1080f;
    private const float TOP_H       = 160f;
    private const float FOOTER_H    = 100f;
    private const float BTN_H       = 210f;
    private const float BTN_GAP     = 22f;
    private const float BTN_PAD_X   = 40f;
    private const float HERO_H      = 300f;
    private const float ACCENT_W    = 10f;
    private const float ICON_SIZE   = 110f;
    private const float ICON_AREA_W = 160f;
    private const int   PARTICLE_N  = 20;

    // ════════════════════════════════════════════════════════════════
    void Start()
    {
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

        // ── Canvas ────────────────────────────────────────────────────
        var cgo = new GameObject("MenuCanvas");
        var cvs = cgo.AddComponent<Canvas>();
        cvs.renderMode = RenderMode.ScreenSpaceOverlay;
        cvs.sortingOrder = 100;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(REF_W, 1920f);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        var cv = cvs.GetComponent<RectTransform>();

        // Background
        MkPanel("BG", cv, C_BG,
            Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);

        // Layers (bottom → top)
        BuildParticles(cv);
        BuildTopBar(cv);
        BuildHero(cv);

        float totalBtnsH = 3f * BTN_H + 2f * BTN_GAP;
        float centerY    = (FOOTER_H - TOP_H) * 0.5f;

        BuildMenuButton(cv, "360° View",     "Rotate & explore in 3D",
            C_BLUE_ACCENT,   C_BLUE_ICON,   DrawIcon360,
            0, totalBtnsH, centerY, () => LoadScene(scene360View));

        BuildMenuButton(cv, "Place On Room", "Visualise in your real space",
            C_GREEN_ACCENT,  C_GREEN_ICON,  DrawIconAR,
            1, totalBtnsH, centerY, () => LoadScene(scenePlaceOnRoom));

        BuildMenuButton(cv, "Try On",        "Face & hand AR jewellery",
            C_PURPLE_ACCENT, C_PURPLE_ICON, DrawIconTryOn,
            2, totalBtnsH, centerY, () => LoadScene(sceneTryOn));

        BuildFooter(cv);
        Debug.Log("[MainMenuController] Modern UI built OK.");
    }

    // ════════════════════════════════════════════════════════════════
    //  AMBIENT PARTICLES
    // ════════════════════════════════════════════════════════════════
    void BuildParticles(RectTransform cv)
    {
        Color[] cols = {
            new Color(0.831f, 0.659f, 0.125f, 0.55f),
            new Color(0.376f, 0.188f, 0.784f, 0.45f),
            new Color(0.094f, 0.627f, 0.353f, 0.40f),
            new Color(0.157f, 0.337f, 0.831f, 0.45f),
        };

        for (int i = 0; i < PARTICLE_N; i++)
        {
            float startX = UnityEngine.Random.Range(0f, REF_W);
            float startY = UnityEngine.Random.Range(0f, 900f);
            Color col    = cols[i % cols.Length];
            float delay  = UnityEngine.Random.Range(0f, 6f);
            float dur    = UnityEngine.Random.Range(4f, 8f);
            float rise   = UnityEngine.Random.Range(280f, 580f);

            var p = MkPanel("P" + i, cv, new Color(col.r, col.g, col.b, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(6f, 6f),
                new Vector2(startX - REF_W * 0.5f, startY - 960f), false);
            p.transform.SetAsFirstSibling();
            StartCoroutine(ParticleLoop(p, col, delay, dur, rise));
        }
    }

    IEnumerator ParticleLoop(GameObject p, Color baseCol, float delay, float dur, float rise)
    {
        yield return new WaitForSeconds(delay);
        var rt  = p.GetComponent<RectTransform>();
        var img = p.GetComponent<Image>();
        if (rt == null || img == null) yield break;
        Vector2 origin = rt.anchoredPosition;

        while (p != null)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (p == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float a = t < 0.2f ? t / 0.2f : (t > 0.8f ? (1f - t) / 0.2f : 1f);
                img.color = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * a * 0.6f);
                rt.anchoredPosition = new Vector2(origin.x, origin.y + rise * t);
                yield return null;
            }
            if (p == null) yield break;
            rt.anchoredPosition = origin;
            img.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  TOP BAR
    // ════════════════════════════════════════════════════════════════
    void BuildTopBar(RectTransform cv)
    {
        var bar = MkPanel("TopBar", cv, C_TOPBAR,
            new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1),
            new Vector2(0, TOP_H), Vector2.zero, false);

        // Bottom border
        MkPanel("TBLine", Rt(bar), C_TOPBAR_BORDER,
            new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0),
            new Vector2(0, 1.5f), Vector2.zero, false);

        // Gold logo tile
        var logo = MkPanel("Logo", Rt(bar), C_GOLD,
            new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(0,0.5f),
            new Vector2(88f,88f), new Vector2(52f, 0f), false);
        MkTMP(logo, "S", 46, FontStyles.Bold,
              new Color(0.05f,0.05f,0.05f,1f), HorizontalAlignmentOptions.Center);

        // Pulse ring
        var ring = MkPanel("Ring", Rt(bar), new Color(C_GOLD.r,C_GOLD.g,C_GOLD.b,0f),
            new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(0,0.5f),
            new Vector2(100f,100f), new Vector2(52f, 0f), false);
        StartCoroutine(PulseRing(ring.GetComponent<Image>()));

        // Brand name (perfectly locked)
        var n1 = new GameObject("BName", typeof(RectTransform));
        n1.transform.SetParent(bar.transform, false);
        Rt(n1).anchorMin = new Vector2(0,0.5f); Rt(n1).anchorMax = new Vector2(0,0.5f);
        Rt(n1).pivot = new Vector2(0,0.5f);
        Rt(n1).anchoredPosition = new Vector2(165f, 22f);
        Rt(n1).sizeDelta = new Vector2(600f, 50f);
        MkTMP(n1, "SDS", 40, FontStyles.Bold, C_GOLD_BRIGHT, HorizontalAlignmentOptions.Left);

        // Brand subtitle (perfectly flush)
        var n2 = new GameObject("BSub", typeof(RectTransform));
        n2.transform.SetParent(bar.transform, false);
        Rt(n2).anchorMin = new Vector2(0,0.5f); Rt(n2).anchorMax = new Vector2(0,0.5f);
        Rt(n2).pivot = new Vector2(0,0.5f);
        Rt(n2).anchoredPosition = new Vector2(165f, -18f);
        Rt(n2).sizeDelta = new Vector2(600f, 40f);
        MkTMP(n2, "JEWELLERY TRY-ON", 22, FontStyles.Normal,
              new Color(1f,1f,1f,0.35f), HorizontalAlignmentOptions.Left);

        // Hamburger (3 lines)
        var hb = MkPanel("HB", Rt(bar), Color.clear,
            new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(1,0.5f),
            new Vector2(70f,70f), new Vector2(-30f, 0f), false);
        foreach (float off in new float[]{ 18f, 0f, -18f })
            MkPanel("L", Rt(hb), new Color(1f,1f,1f,0.4f),
                new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                new Vector2(54f,5f), new Vector2(0,off), false);
    }

    IEnumerator PulseRing(Image img)
    {
        while (img != null)
        {
            float dur = 2.8f, e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float t = e / dur;
                float a = t < 0.3f ? (t/0.3f)*0.22f : ((1f-t)/0.7f)*0.22f;
                if (img == null) yield break;
                img.color = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, a);
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  HERO SECTION
    // ════════════════════════════════════════════════════════════════
    void BuildHero(RectTransform cv)
    {
        var hero = MkPanel("Hero", cv, Color.clear,
            new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1),
            new Vector2(0, HERO_H), new Vector2(0, -TOP_H), false);

        // Eyebrow
        var eb = new GameObject("Eyebrow", typeof(RectTransform));
        eb.transform.SetParent(hero.transform, false);
        Rt(eb).anchorMin = new Vector2(0,0.72f); Rt(eb).anchorMax = new Vector2(1,0.90f);
        Rt(eb).offsetMin = new Vector2(54f,0); Rt(eb).offsetMax = new Vector2(-40f,0);
        MkTMP(eb, "EXPERIENCE COLLECTION", 22, FontStyles.Bold, C_EYEBROW, HorizontalAlignmentOptions.Left);

        // Title line 1
        var t1 = new GameObject("T1", typeof(RectTransform));
        t1.transform.SetParent(hero.transform, false);
        Rt(t1).anchorMin = new Vector2(0,0.44f); Rt(t1).anchorMax = new Vector2(1,0.72f);
        Rt(t1).offsetMin = new Vector2(50f,0); Rt(t1).offsetMax = new Vector2(-40f,0);
        MkTMP(t1, "Try Jewellery", 64, FontStyles.Bold, Color.white, HorizontalAlignmentOptions.Left);

        // Title line 2 (gold accent)
        var t2 = new GameObject("T2", typeof(RectTransform));
        t2.transform.SetParent(hero.transform, false);
        Rt(t2).anchorMin = new Vector2(0,0.16f); Rt(t2).anchorMax = new Vector2(1,0.46f);
        Rt(t2).offsetMin = new Vector2(50f,0); Rt(t2).offsetMax = new Vector2(-40f,0);
        MkTMP(t2, "On Your Terms", 64, FontStyles.Bold, C_GOLD_BRIGHT, HorizontalAlignmentOptions.Left);

        // Gold separator bar
        MkPanel("GoldBar", Rt(hero), C_GOLD,
            new Vector2(0,0), new Vector2(0,0), new Vector2(0,0),
            new Vector2(80f,7f), new Vector2(50f, 20f), false);

        StartCoroutine(FadeIn(hero, 0.3f, 0.55f));
    }

    // ════════════════════════════════════════════════════════════════
    //  MENU BUTTON
    //  Root panel: raycast = TRUE   (receives pointer events)
    //  All children: raycast = FALSE (decorative, never block clicks)
    // ════════════════════════════════════════════════════════════════
    void BuildMenuButton(RectTransform cv,
        string label, string sub,
        Color accent, Color iconBg,
        System.Action<RectTransform> drawIcon,
        int index, float totalH, float centerY,
        UnityEngine.Events.UnityAction onClick)
    {
        float yOff = totalH * 0.5f - index * (BTN_H + BTN_GAP) - BTN_H * 0.5f + centerY;

        // ROOT — only element that is clickable
        var btn = MkPanel("Btn_" + index, cv, C_BTN_CARD,
            new Vector2(0,0.5f), new Vector2(1,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0, BTN_H), new Vector2(0, yOff), true);   // ← TRUE
        Rt(btn).offsetMin = new Vector2(BTN_PAD_X, 0);
        Rt(btn).offsetMax = new Vector2(-BTN_PAD_X, 0);
        Rt(btn).anchoredPosition = new Vector2(0, yOff);
        Rt(btn).sizeDelta = new Vector2(0, BTN_H);

        // Left accent bar
        MkPanel("Accent", Rt(btn), accent,
            new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f),
            new Vector2(ACCENT_W, 0), Vector2.zero, false);

        // Icon background + icon
        var iconBgGo = MkPanel("IconBg", Rt(btn), iconBg,
            new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(0,0.5f),
            new Vector2(ICON_SIZE, ICON_SIZE), new Vector2(ICON_AREA_W*0.5f, 0f), false);
        drawIcon(Rt(iconBgGo));

        // Main label with solid X positioning so it never overrides
        var lb = new GameObject("Lb", typeof(RectTransform));
        lb.transform.SetParent(btn.transform, false);
        Rt(lb).anchorMin = new Vector2(0,0.5f); Rt(lb).anchorMax = new Vector2(0,0.5f);
        Rt(lb).pivot = new Vector2(0,0.5f);
        Rt(lb).anchoredPosition = new Vector2(195f, 30f);
        Rt(lb).sizeDelta = new Vector2(700f, 60f);
        MkTMP(lb, label, 42, FontStyles.Bold,
              new Color(0.929f,0.878f,0.784f,1f), HorizontalAlignmentOptions.Left);

        // Sub-label exactly flush
        var sb = new GameObject("Sb", typeof(RectTransform));
        sb.transform.SetParent(btn.transform, false);
        Rt(sb).anchorMin = new Vector2(0,0.5f); Rt(sb).anchorMax = new Vector2(0,0.5f);
        Rt(sb).pivot = new Vector2(0,0.5f);
        Rt(sb).anchoredPosition = new Vector2(195f, -25f);
        Rt(sb).sizeDelta = new Vector2(700f, 50f);
        MkTMP(sb, sub, 28, FontStyles.Normal, C_WHITE_DIM, HorizontalAlignmentOptions.Left);

        // Chevron arrow
        var ar = new GameObject("Ar", typeof(RectTransform));
        ar.transform.SetParent(btn.transform, false);
        Rt(ar).anchorMin = new Vector2(1,0); Rt(ar).anchorMax = new Vector2(1,1);
        Rt(ar).pivot = new Vector2(1,0.5f);
        Rt(ar).sizeDelta = new Vector2(80f, 0);
        Rt(ar).anchoredPosition = Vector2.zero;
        MkTMP(ar, "›", 52, FontStyles.Normal, C_GOLD_DIM, HorizontalAlignmentOptions.Center);

        // Bottom divider
        MkPanel("Div", Rt(btn), new Color(0.25f,0.22f,0.12f,0.6f),
            new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0),
            new Vector2(-40f,1.5f), Vector2.zero, false);

        // Shimmer overlay (cycling alpha pulse)
        var shim = MkPanel("Shim", Rt(btn), new Color(1f,1f,1f,0f),
            Vector2.zero, Vector2.one, new Vector2(0.5f,0.5f),
            Vector2.zero, Vector2.zero, false);
        StartCoroutine(ShimmerLoop(shim.GetComponent<Image>(), 0.6f + index * 0.45f));

        // Button component — wired to root Image
        var b = btn.AddComponent<Button>();
        b.targetGraphic = btn.GetComponent<Image>();
        var cols = b.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.08f,1.08f,1.08f,1f);
        cols.pressedColor     = new Color(0.72f,0.72f,0.72f,1f);
        cols.fadeDuration     = 0.08f;
        b.colors = cols;
        b.onClick.AddListener(() =>
        {
            StartCoroutine(PressFlash(btn, btn.GetComponent<Image>(), accent));
            onClick.Invoke();
        });

        // Staggered slide-up entrance
        StartCoroutine(SlideUpEntrance(btn.transform, index * 0.13f));
    }

    // ════════════════════════════════════════════════════════════════
    //  ICONS  (pure Unity UI panels — no sprites needed)
    // ════════════════════════════════════════════════════════════════

    // Globe / 360 icon: ring + cross lines
    void DrawIcon360(RectTransform p)
    {
        MkIconRing(p, C_ICON_BLUE, 82f, 7f);
        MkPanel("EQ", p, C_ICON_BLUE, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(82f,6f), Vector2.zero, false);
        MkPanel("VL", p, C_ICON_BLUE, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(6f,82f), Vector2.zero, false);
    }

    // AR camera / room icon: frame + center dot
    void DrawIconAR(RectTransform p)
    {
        float s = 68f;
        MkPanel("T",  p, C_ICON_GREEN, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(s,6f), new Vector2(0, s*0.5f),  false);
        MkPanel("B",  p, C_ICON_GREEN, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(s,6f), new Vector2(0,-s*0.5f),  false);
        MkPanel("L",  p, C_ICON_GREEN, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(6f,s), new Vector2(-s*0.5f,0),  false);
        MkPanel("R",  p, C_ICON_GREEN, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(6f,s), new Vector2( s*0.5f,0),  false);
        MkPanel("C",  p, C_ICON_GREEN, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(22f,22f), Vector2.zero,         false);
    }

    // Location pin icon: circle head + tail bar
    void DrawIconTryOn(RectTransform p)
    {
        MkPanel("Head", p, C_ICON_PURPLE, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(52f,52f), new Vector2(0,16f),  false);
        MkPanel("Hole", p, C_ICON_MASK, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(20f,20f), new Vector2(0,16f),  false);
        MkPanel("Tail", p, C_ICON_PURPLE, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(16f,36f), new Vector2(0,-16f), false);
    }

    // Thin ring: outer filled square minus inner mask square
    void MkIconRing(RectTransform p, Color col, float size, float thickness)
    {
        MkPanel("Outer", p, col, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f), new Vector2(size,size), Vector2.zero, false);
        MkPanel("Inner", p, C_ICON_MASK, new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0.5f,0.5f),
            new Vector2(size - thickness*2f, size - thickness*2f), Vector2.zero, false);
    }

    // ════════════════════════════════════════════════════════════════
    //  FOOTER BAR
    // ════════════════════════════════════════════════════════════════
    void BuildFooter(RectTransform cv)
    {
        var f = MkPanel("Footer", cv, C_TOPBAR,
            new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0),
            new Vector2(0, FOOTER_H), Vector2.zero, false);
        MkPanel("FLine", Rt(f), C_TOPBAR_BORDER,
            new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1),
            new Vector2(0, 1.5f), Vector2.zero, false);
        var ft = new GameObject("FTxt", typeof(RectTransform));
        ft.transform.SetParent(f.transform, false);
        Rt(ft).anchorMin = Vector2.zero; Rt(ft).anchorMax = Vector2.one;
        MkTMP(ft, "SDS JEWELLERY © 2025", 22, FontStyles.Normal,
              C_FOOTER_TEXT, HorizontalAlignmentOptions.Center);
    }

    // ════════════════════════════════════════════════════════════════
    //  ANIMATIONS
    // ════════════════════════════════════════════════════════════════

    IEnumerator SlideUpEntrance(Transform t, float delay)
    {
        var cg = t.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        var rt = t.GetComponent<RectTransform>();
        Vector2 orig = rt.anchoredPosition;
        rt.anchoredPosition = new Vector2(orig.x, orig.y - 60f);
        yield return new WaitForSeconds(0.25f + delay);
        float dur = 0.28f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, e / dur);
            cg.alpha = p;
            rt.anchoredPosition = new Vector2(orig.x, Mathf.Lerp(orig.y - 60f, orig.y, p));
            yield return null;
        }
        cg.alpha = 1f;
        rt.anchoredPosition = orig;
        Destroy(cg);
    }

    IEnumerator FadeIn(GameObject go, float delay, float dur)
    {
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        yield return new WaitForSeconds(delay);
        float e = 0f;
        while (e < dur) { e += Time.deltaTime; cg.alpha = Mathf.SmoothStep(0,1,e/dur); yield return null; }
        cg.alpha = 1f;
        Destroy(cg);
    }

    IEnumerator PressFlash(GameObject btn, Image img, Color accent)
    {
        Color orig = img.color;
        float dur = 0.10f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = e / dur;
            img.color = Color.Lerp(orig, new Color(accent.r,accent.g,accent.b,0.22f), p);
            btn.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one*0.95f, p);
            yield return null;
        }
        e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = e / dur;
            img.color = Color.Lerp(new Color(accent.r,accent.g,accent.b,0.22f), orig, p);
            btn.transform.localScale = Vector3.Lerp(Vector3.one*0.95f, Vector3.one, p);
            yield return null;
        }
        img.color = orig;
        btn.transform.localScale = Vector3.one;
    }

    IEnumerator ShimmerLoop(Image img, float startDelay)
    {
        yield return new WaitForSeconds(startDelay);
        while (img != null)
        {
            float dur = 0.55f, hold = 3.5f, e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float t = e / dur;
                float a = t < 0.5f ? (t/0.5f)*0.06f : ((1f-t)/0.5f)*0.06f;
                if (img == null) yield break;
                img.color = new Color(1f,1f,1f,a);
                yield return null;
            }
            if (img != null) img.color = new Color(1f,1f,1f,0f);
            yield return new WaitForSeconds(hold);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SCENE LOADING
    // ════════════════════════════════════════════════════════════════
    void LoadScene(string name)
    {
        if (string.IsNullOrEmpty(name))
        { Debug.LogError("[MainMenuController] Scene name empty — check Inspector."); return; }
        Debug.Log($"[MainMenuController] Loading: {name}");
        SceneManager.LoadScene(name);
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════
    RectTransform Rt(GameObject go) => go.GetComponent<RectTransform>();

    GameObject MkPanel(string name, RectTransform parent, Color color,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
        Vector2 size, Vector2 pos, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return go;
    }

    void MkTMP(GameObject parent, string text, int size, FontStyles style,
               Color color, HorizontalAlignmentOptions align, float pad = 0f)
    {
        var go = new GameObject("T", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, 0f); rt.offsetMax = new Vector2(0f, 0f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = color;
        t.horizontalAlignment = align;
        t.verticalAlignment = VerticalAlignmentOptions.Middle;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
    }
}