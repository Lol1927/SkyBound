using UnityEngine;

public class AileronTilt : MonoBehaviour
{
    public float raiseSign = 1f;
    public float maxAngle = 13f;

    Quaternion rest;
    float angle;

    void Awake()
    {
        rest = transform.localRotation;
    }

    void Update()
    {
        float input = 0f;
        if (!WorldManager.FlightPaused)
        {
            PlaneController controller = GetComponentInParent<PlaneController>();
            bool locked = controller != null && controller.InputLocked;
            if (!locked)
            {
                if (Input.GetKey(KeyCode.A))
                    input -= 1f;
                if (Input.GetKey(KeyCode.D))
                    input += 1f;
            }
        }

        float target = input * raiseSign * maxAngle;
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        angle = Mathf.Lerp(angle, target, blend);
        transform.localRotation = rest * Quaternion.Euler(angle, 0f, 0f);
    }
}
