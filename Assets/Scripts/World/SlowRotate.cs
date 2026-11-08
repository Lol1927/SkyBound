using UnityEngine;

public class SlowRotate : MonoBehaviour
{
    public float degreesPerSecond = 22f;

    void Update()
    {
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
