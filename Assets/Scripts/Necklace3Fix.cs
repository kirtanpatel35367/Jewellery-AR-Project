using UnityEngine;

public class Necklace3Fix : MonoBehaviour
{
    [Header("Necklace_3 placement")]
    public Vector3 finalLocalPosition = new Vector3(0f, 0.12f, 0.04f); // more UP + closer
    public Vector3 finalLocalRotation = Vector3.zero;
    public float finalUniformScale = 1f;

    [Header("Re-apply for few frames (prevents manager override)")]
    public float applyForSeconds = 1.0f;

    float tEnd;

    void OnEnable()
    {
        tEnd = Time.time + applyForSeconds;
        Apply();
    }

    void LateUpdate()
    {
        if (Time.time <= tEnd) Apply();
    }

    void Apply()
    {
        transform.localPosition = finalLocalPosition;
        transform.localEulerAngles = finalLocalRotation;
        transform.localScale = Vector3.one * Mathf.Max(0.0001f, finalUniformScale);
    }
}