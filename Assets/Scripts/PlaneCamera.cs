using UnityEngine;

public class PlaneCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 4f, -12f);
    [SerializeField] float positionSmoothTime = 0.18f;
    [SerializeField] float rotationSharpness = 8f;
    [SerializeField] float lookAhead = 8f;

    Vector3 positionVelocity;
    float shakeUntil;
    float shakeMag;

    public bool SuspendFollow { get; set; }

    public void Shake(float duration, float magnitude)
    {
        shakeUntil = Time.unscaledTime + Mathf.Max(0.05f, duration);
        shakeMag = magnitude;
    }

    void Start()
    {
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (!SuspendFollow && target != null)
        {
            Quaternion followRotation = UnbankedRotation(target);
            Vector3 desiredPosition = target.position + followRotation * offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);

            Vector3 lookPoint = target.position + followRotation * Vector3.forward * lookAhead;
            Vector3 toLookPoint = lookPoint - transform.position;
            if (toLookPoint.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(toLookPoint, Vector3.up);
                float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
            }
        }

        if (Time.unscaledTime < shakeUntil && shakeMag > 0f)
        {
            transform.position += UnityEngine.Random.insideUnitSphere * shakeMag;
            float remaining = shakeUntil - Time.unscaledTime;
            if (remaining < 0.12f)
                shakeMag = Mathf.Lerp(0f, shakeMag, remaining / 0.12f);
        }
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        Quaternion followRotation = UnbankedRotation(target);
        transform.position = target.position + followRotation * offset;
        Vector3 lookPoint = target.position + followRotation * Vector3.forward * lookAhead;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        positionVelocity = Vector3.zero;
    }

    static Quaternion UnbankedRotation(Transform followTarget)
    {
        Vector3 forward = followTarget.forward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (up.sqrMagnitude < 0.0001f)
            up = Vector3.up;

        return Quaternion.LookRotation(forward, up);
    }
}
