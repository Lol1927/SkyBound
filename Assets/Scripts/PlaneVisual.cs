using UnityEngine;

public class PropellerSpin : MonoBehaviour
{
    public float cruiseDegreesPerSecond = 520f;
    public float boostDegreesPerSecond = 1560f;

    float spinRate;

    void Update()
    {
        float target = cruiseDegreesPerSecond;
        if (!WorldManager.FlightPaused)
        {
            PlaneController controller = GetComponentInParent<PlaneController>();
            bool locked = controller != null && controller.InputLocked;
            if (!locked && Input.GetKey(KeyCode.LeftShift))
                target = boostDegreesPerSecond;
        }

        float blend = 1f - Mathf.Exp(-3.2f * Time.deltaTime);
        spinRate = Mathf.Lerp(spinRate, target, blend);
        transform.Rotate(Vector3.forward, spinRate * Time.deltaTime, Space.Self);
    }
}
