using UnityEngine;

public class SlowRotate : MonoBehaviour
{
    public float speed = 25f;

    void Update()
    {
        transform.Rotate(0f, speed * Time.deltaTime, 0f);
    }
}