using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// NecklaceAttachARCore — v4  (Face-Relative + Physics Motion)
/// ─────────────────────────────────────────────────────────────
///
/// THE CORE PROBLEM WITH ALL PREVIOUS VERSIONS:
///   Using Vector3.down * neckDrop in WORLD space is wrong.
///   The ARCore face mesh is ~0.4–0.8m from the camera depending on how
///   close the person holds the phone. A fixed world-space drop of 0.06m
///   may land at the chin for one person and at the chest for another.
///
/// THE FIX — Face-relative positioning:
///   We use the face's OWN coordinate system (face.transform) to drop
///   "downward relative to the face". This means as the face scales
///   (person moves closer/further), the anchor moves proportionally.
///   We also measure the FACE HEIGHT (forehead to chin distance) and
///   express neckDrop as a FRACTION of face height. This way the anchor
///   is always the same percentage below the chin regardless of distance.
///
/// NECK VERTEX STRATEGY:
///   Vertex 152 = chin bottom tip (lowest face mesh point)
///   Vertex 10  = forehead top
///   We use (chin - forehead) distance to get face height, then place
///   the anchor at chin + faceHeight * neckDropFraction downward.
///
/// REALISM — Physics-based motion:
///   A spring + damper simulation makes the necklace lag behind head
///   movement naturally, then settle. This mimics real jewelry physics
///   without needing a physics engine on the mesh itself.
/// </summary>
[RequireComponent(typeof(ARFace))]
public class NecklaceAttachARCore : MonoBehaviour
{
    // ── Key vertices ──────────────────────────────────────────────────────
    // Chin bottom (lowest point of face mesh)
    private const int CHIN_VERT = 152;
    // Forehead top (highest point of face mesh)
    private const int FOREHEAD_VERT = 10;
    // Additional chin/jaw verts for averaging
    private static readonly int[] CHIN_CLUSTER = { 152, 175, 400, 148, 377, 32, 262 };

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Neck Position — Face-Relative")]
    [Tooltip("How far below the chin to drop the anchor, as a FRACTION of face height.\n" +
             "0.0 = at chin. 0.15 = 15% of face height below chin (collarbone).\n" +
             "This scales with how close the person is to the camera — much more reliable\n" +
             "than a fixed world-space value.\n" +
             "Recommended: 0.08 to 0.15 for a choker/collar necklace.")]
    [Range(0f, 0.5f)]
    public float neckDropFraction = 0.10f;

    [Tooltip("Push anchor forward (out from face) as fraction of face height.\n" +
             "Keeps necklace in front of the neck, not sunk into it.\n" +
             "Recommended: 0.05 to 0.10")]
    [Range(0f, 0.3f)]
    public float forwardFraction = 0.06f;

    [Tooltip("Extra world-space offset. Leave at zero unless needed.")]
    public Vector3 extraOffset = Vector3.zero;

    [Header("Position Smoothing")]
    [Tooltip("Spring stiffness — how quickly anchor follows the face.\n" +
             "Higher = snappier. Lower = more lag (but can feel floaty).\n" +
             "Recommended: 15-25")]
    [Range(1f, 40f)]
    public float posSmooth = 18f;

    [Header("Rotation Follow")]
    [Tooltip("How much the necklace rotates when head turns left/right.\n" +
             "0 = necklace stays perfectly world-upright (good for heavy necklaces).\n" +
             "0.3 = slight follow (most realistic for chokers).\n" +
             "1 = full follow (necklace rotates fully with head).")]
    [Range(0f, 1f)]
    public float yawFollow = 0.25f;

    [Tooltip("Pitch follow — how much necklace tilts when head tilts forward/back.\n" +
             "Keep low (0.1) for realism — necklace should mostly hang by gravity.")]
    [Range(0f, 1f)]
    public float pitchFollow = 0.08f;

    [Tooltip("Roll follow — necklace tilt when head tilts sideways.\n" +
             "Keep low for realism.")]
    [Range(0f, 1f)]
    public float rollFollow = 0.1f;

    [Range(1f, 30f)]
    public float rotSmooth = 10f;

    [Header("Physics Motion (Realism)")]
    [Tooltip("Simulates jewelry inertia — necklace lags behind fast movements.\n" +
             "Higher = more lag/swing. 0 = no physics motion.")]
    [Range(0f, 1f)]
    public float inertiaStrength = 0.35f;

    [Tooltip("How quickly the inertia effect decays back to resting position.\n" +
             "Higher = faster settle. Lower = longer swing.")]
    [Range(1f, 20f)]
    public float inertiaDamping = 6f;

    [Tooltip("Maximum distance the physics offset can push the necklace (meters).\n" +
             "Prevents extreme swing on fast movements.")]
    [Range(0f, 0.05f)]
    public float inertiaMaxDist = 0.015f;

    [Tooltip("Subtle constant sway — simulates breathing and micro-movements.\n" +
             "0 = no sway. 0.002 = very subtle (recommended).")]
    [Range(0f, 0.01f)]
    public float ambientSwayAmt = 0.001f;

    [Range(0.1f, 3f)]
    public float ambientSwaySpeed = 0.8f;

    // ── Private ───────────────────────────────────────────────────────────
    private ARFace _face;
    private Transform _neckAnchor;
    private bool _registered;

    // Position smoothing
    private Vector3 _smoothPos;
    private Vector3 _posVel = Vector3.zero;

    // Rotation smoothing
    private Quaternion _smoothRot = Quaternion.identity;

    // Physics inertia
    private Vector3 _inertiaOffset = Vector3.zero;
    private Vector3 _inertiaVelocity = Vector3.zero;
    private Vector3 _prevRawPos = Vector3.zero;
    private bool _firstFrame = true;

    // Ambient sway
    private float _swayPhase;

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _face = GetComponent<ARFace>();
        _neckAnchor = new GameObject("NecklaceAnchor").transform;
        _neckAnchor.SetParent(transform);
        _swayPhase = Random.Range(0f, Mathf.PI * 2f);
        Register();
    }

    void OnEnable()
    {
        if (!_registered) Register();
    }

    void Register()
    {
        JewelryManager mgr = FindObjectOfType<JewelryManager>();
        if (mgr != null)
        {
            mgr.RegisterNecklaceAnchor(_neckAnchor);
            _registered = true;
            Debug.Log("[NecklaceAttachARCore v4] Anchor registered.");
        }
        else
        {
            Debug.LogError("[NecklaceAttachARCore] JewelryManager not found!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void LateUpdate()  // LateUpdate so we run after ARFace updates vertices
    {
        if (_face == null || _neckAnchor == null) return;
        if (_face.trackingState != TrackingState.Tracking) return;
        if (_face.vertices == null || _face.vertices.Length < 200) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ── 1. Compute face metrics ───────────────────────────────────────
        Vector3 chinWorld = FaceVert(CHIN_VERT);
        Vector3 foreheadWorld = FaceVert(FOREHEAD_VERT);
        float faceHeight = Vector3.Distance(chinWorld, foreheadWorld);

        // Clamp faceHeight to sensible range (avoids divide-by-zero or crazy values)
        faceHeight = Mathf.Clamp(faceHeight, 0.05f, 0.40f);

        // Average chin cluster for a stable base point
        Vector3 chinBase = Vector3.zero;
        int count = 0;
        foreach (int idx in CHIN_CLUSTER)
        {
            if (idx < _face.vertices.Length)
            { chinBase += FaceVert(idx); count++; }
        }
        if (count > 0) chinBase /= count;
        else chinBase = chinWorld;

        // ── 2. Compute raw target position ───────────────────────────────
        // Drop along FACE's local down (not world down) — this is the key fix.
        // face.transform.up = face's local up axis (top of head direction).
        // So face's down = -face.transform.up.
        // This scales with face distance automatically.
        Vector3 faceDown = -_face.transform.up;
        Vector3 faceForward = -_face.transform.forward; // ARCore face forward points INTO screen

        Vector3 rawTarget = chinBase
            + faceDown * (faceHeight * neckDropFraction)
            + faceForward * (faceHeight * forwardFraction)
            + extraOffset;

        // ── 3. Smooth position ────────────────────────────────────────────
        float smoothTime = 1f / Mathf.Max(posSmooth, 0.01f);
        if (_firstFrame)
        {
            _smoothPos = rawTarget;
            _prevRawPos = rawTarget;
            _firstFrame = false;
        }
        else
        {
            _smoothPos = Vector3.SmoothDamp(_smoothPos, rawTarget, ref _posVel, smoothTime);
        }

        // ── 4. Physics inertia offset ─────────────────────────────────────
        // Measure how fast the face is moving in world space
        Vector3 faceDelta = rawTarget - _prevRawPos;
        _prevRawPos = rawTarget;

        // Inertia: necklace resists movement — offset opposite to motion
        if (inertiaStrength > 0f)
        {
            Vector3 targetInertia = -faceDelta * (inertiaStrength * 80f);
            _inertiaOffset = Vector3.SmoothDamp(
                _inertiaOffset,
                targetInertia,
                ref _inertiaVelocity,
                1f / Mathf.Max(inertiaDamping, 0.1f));

            // Clamp magnitude so it doesn't go crazy
            if (_inertiaOffset.magnitude > inertiaMaxDist)
                _inertiaOffset = _inertiaOffset.normalized * inertiaMaxDist;
        }
        else
        {
            _inertiaOffset = Vector3.zero;
        }

        // ── 5. Ambient sway ───────────────────────────────────────────────
        Vector3 swayOffset = Vector3.zero;
        if (ambientSwayAmt > 0f)
        {
            _swayPhase += dt * ambientSwaySpeed;
            float swayX = Mathf.Sin(_swayPhase) * ambientSwayAmt;
            float swayY = Mathf.Sin(_swayPhase * 0.7f) * ambientSwayAmt * 0.5f;
            swayOffset = new Vector3(swayX, swayY, 0f);
        }

        // ── 6. Final anchor position ──────────────────────────────────────
        _neckAnchor.position = _smoothPos + _inertiaOffset + swayOffset;

        // ── 7. Smooth rotation ────────────────────────────────────────────
        _smoothRot = Quaternion.Slerp(_smoothRot, TargetRot(), dt * rotSmooth);
        _neckAnchor.rotation = _smoothRot;
    }

    // ─────────────────────────────────────────────────────────────────────
    Vector3 FaceVert(int idx)
    {
        idx = Mathf.Clamp(idx, 0, _face.vertices.Length - 1);
        return _face.transform.TransformPoint(_face.vertices[idx]);
    }

    Quaternion TargetRot()
    {
        Vector3 e = _face.transform.eulerAngles;
        return Quaternion.Euler(
            Mathf.LerpAngle(0f, e.x, pitchFollow),
            Mathf.LerpAngle(0f, e.y, yawFollow),
            Mathf.LerpAngle(0f, e.z, rollFollow));
    }

    // ─────────────────────────────────────────────────────────────────────
    public void OnNecklaceSpawned(GameObject instance)
    {
        if (instance == null) return;
        var fit = instance.GetComponent<NecklaceAutoFit>();
        if (fit != null) fit.Apply();
        else Debug.LogWarning($"[NecklaceAttachARCore] '{instance.name}' has no NecklaceAutoFit.");
    }

    void OnDestroy()
    {
        if (_neckAnchor != null) Destroy(_neckAnchor.gameObject);
    }
}