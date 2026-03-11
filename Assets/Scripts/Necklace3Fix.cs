using UnityEngine;

public class Necklace3Fix : MonoBehaviour
{
    [Header("Final Placement (relative to NecklaceAnchor)")]
    public Vector3 finalLocalPosition = Vector3.zero;

    [Header("Final Rotation")]
    public Vector3 finalLocalRotationEuler = Vector3.zero;

    [Header("Final Size")]
    public float finalUniformScale = 1f;

    [Header("Stability")]
    [Tooltip("If true, keeps applying every frame so nothing overrides it.")]
    public bool applyContinuously = true;

    [Tooltip("If applyContinuously is false, apply only for a short time after spawn.")]
    public float applyForSeconds = 1f;

    float timer;

    void OnEnable()
    {
        timer = 0f;
        ApplyNow();
    }

    void LateUpdate()
    {
        if (applyContinuously)
        {
            ApplyNow();
            return;
        }

        timer += Time.deltaTime;
        if (timer <= applyForSeconds)
            ApplyNow();
    }

    public void ApplyNow()
    {
        transform.localPosition = finalLocalPosition;
        transform.localRotation = Quaternion.Euler(finalLocalRotationEuler);
        transform.localScale = Vector3.one * Mathf.Max(0.0001f, finalUniformScale);
    }
}