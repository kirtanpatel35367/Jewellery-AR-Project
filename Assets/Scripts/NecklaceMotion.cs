using UnityEngine;

public class NecklaceMotion : MonoBehaviour
{
    public float swayAmount = 2f;
    public float swaySpeed = 3f;

    Quaternion startRot;

    void Start()
    {
        startRot = transform.localRotation;
    }

    void Update()
    {
        float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
        transform.localRotation = startRot * Quaternion.Euler(0, 0, sway);
    }
}