using System;
using UnityEngine;

[DefaultExecutionOrder(-20)]
[RequireComponent(typeof(PlaneController))]
public class WorldBoundary : MonoBehaviour
{
    [SerializeField] float radius = 700f;
    [SerializeField] float minAltitude = 10f;
    [SerializeField] float maxAltitude = 320f;
    [SerializeField] float steerStrength = 90f;

    const float HorizontalBlendDistance = 80f;
    const float AltitudeBlendDistance = 30f;
    const float OceanResetHeight = 2f;

    public float Radius => radius;
    public float MinAltitude => minAltitude;
    public float MaxAltitude => maxAltitude;
    public bool IsOutOfBounds { get; private set; }
    public event Action<bool> OutOfBoundsChanged;

    PlaneController controller;

    void Awake()
    {
        controller = GetComponent<PlaneController>();
        WorldGenerator.HideDebugLines();
    }

    void OnDrawGizmos()
    {
        if (WorldGenerator.DrawingInGameView())
            return;

        Gizmos.color = new Color(0.45f, 0.85f, 1f, 0.4f);
        const int segments = 64;
        Vector3 previous = new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    void Update()
    {
        if (WorldManager.FlightPaused)
            return;

        if (LivesManager.Instance != null && LivesManager.Instance.IsDowned)
            return;

        if (transform.position.y < OceanResetHeight)
        {
            bool vulnerable = LivesManager.Instance == null || LivesManager.Instance.CanCrash;
            if (vulnerable)
            {
                if (LivesManager.Instance != null)
                    LivesManager.Instance.Crash();
                else if (controller != null)
                    controller.ResetPlane();
                SetOutOfBounds(false);
                return;
            }
        }

        Vector3 position = transform.position;
        Vector3 flatPosition = new Vector3(position.x, 0f, position.z);
        float distance = flatPosition.magnitude;
        SetOutOfBounds(distance > radius || position.y < minAltitude || position.y > maxAltitude);

        float yaw = HorizontalSteer(flatPosition, distance);
        float pitch = AltitudeSteer(position.y);
        if (Mathf.Abs(yaw) > 0.0001f || Mathf.Abs(pitch) > 0.0001f)
            controller.AddSteer(pitch, yaw);
    }

    float HorizontalSteer(Vector3 flatPosition, float distance)
    {
        float excess = distance - radius;
        if (excess <= 0f || flatPosition.sqrMagnitude < 0.0001f)
            return 0f;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            return 0f;

        float signedAngle = Vector3.SignedAngle(forward, -flatPosition.normalized, Vector3.up);
        float weight = Mathf.SmoothStep(0f, 1f, excess / HorizontalBlendDistance);
        float maxStep = steerStrength * weight * Time.deltaTime;
        return Mathf.Clamp(signedAngle, -maxStep, maxStep);
    }

    float AltitudeSteer(float height)
    {
        if (height < minAltitude + AltitudeBlendDistance)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(minAltitude + AltitudeBlendDistance, minAltitude, height));
            return -steerStrength * t * Time.deltaTime;
        }

        if (height > maxAltitude - AltitudeBlendDistance)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(maxAltitude - AltitudeBlendDistance, maxAltitude, height));
            return steerStrength * t * Time.deltaTime;
        }

        return 0f;
    }

    void SetOutOfBounds(bool value)
    {
        if (IsOutOfBounds == value)
            return;

        IsOutOfBounds = value;
        OutOfBoundsChanged?.Invoke(value);
    }
}
