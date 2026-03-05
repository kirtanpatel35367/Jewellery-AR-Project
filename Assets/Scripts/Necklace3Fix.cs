using UnityEngine;

public class NecklaceStableFix : MonoBehaviour
{
    [Header("Position")]
    public Vector3 finalLocalPosition = new Vector3(0f, 0f, 0f);

    [Header("Rotation")]
    public Vector3 finalLocalRotation = Vector3.zero;

    [Header("Scale")]
    public float finalUniformScale = 1.15f;

    [Header("Stability")]
    public bool applyContinuously = true;   // always override (recommended)
    public float applyForSeconds = 1.0f;    // fallback if above false

    float tEnd;

    void OnEnable()
    {
        tEnd = Time.time + applyForSeconds;
        Apply();
    }

    void LateUpdate()
    {
        if (applyContinuously || Time.time <= tEnd)
        {
            Apply();
        }
    }

    void Apply()
    {
        transform.localPosition = finalLocalPosition;
        transform.localEulerAngles = finalLocalRotation;
        transform.localScale = Vector3.one * Mathf.Max(0.0001f, finalUniformScale);
    }
}