using UnityEngine;

public class PlaneController : MonoBehaviour
{
    [SerializeField] float speed = 30f;
    [SerializeField] float boostSpeed = 70f;
    [SerializeField] float turnSpeed = 80f;
    [SerializeField] float pitchSpeed = 60f;
    [SerializeField] float maxBankAngle = 35f;

    const float BoostSmoothTime = 0.4f;
    const float BankSharpness = 8f;

    static readonly Collider[] OverlapHits = new Collider[8];

    Vector3 spawnPosition;
    Quaternion spawnRotation;
    Quaternion flightRotation;
    float currentSpeed;
    float speedVelocity;
    float bankAngle;
    float externalPitch;
    float externalYaw;

    public bool InputLocked { get; set; }
    public Vector3 SpawnPosition => spawnPosition;

    void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        flightRotation = spawnRotation;
        currentSpeed = speed;
        EnsureTriggerSensor();
    }

    void EnsureTriggerSensor()
    {
        if (transform.Find("CoPilotSensor") != null)
            return;

        var sensorObject = new GameObject("CoPilotSensor");
        sensorObject.transform.SetParent(transform, false);
        Vector3 scale = transform.localScale;
        sensorObject.transform.localScale = new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);

        var sphere = sensorObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 2.5f;

        var body = sensorObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
    }

    void Update()
    {
        if (WorldManager.FlightPaused || InputLocked)
        {
            externalPitch = 0f;
            externalYaw = 0f;
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (LivesManager.Instance != null)
                LivesManager.Instance.Recover();
            else
                ResetPlane();
            return;
        }

        float yawInput = 0f;
        if (Input.GetKey(KeyCode.A)) yawInput -= 1f;
        if (Input.GetKey(KeyCode.D)) yawInput += 1f;

        float pitchInput = 0f;
        if (Input.GetKey(KeyCode.W)) pitchInput -= 1f;
        if (Input.GetKey(KeyCode.S)) pitchInput += 1f;

        float dt = Time.deltaTime;
        flightRotation *= Quaternion.AngleAxis(pitchInput * pitchSpeed * dt + externalPitch, Vector3.right);
        flightRotation *= Quaternion.AngleAxis(yawInput * turnSpeed * dt + externalYaw, Vector3.up);
        externalPitch = 0f;
        externalYaw = 0f;

        float targetBank = -yawInput * maxBankAngle;
        bankAngle = Mathf.Lerp(bankAngle, targetBank, 1f - Mathf.Exp(-BankSharpness * dt));
        transform.rotation = flightRotation * Quaternion.AngleAxis(bankAngle, Vector3.forward);

        float targetSpeed = Input.GetKey(KeyCode.LeftShift) ? boostSpeed : speed;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, BoostSmoothTime);
        Vector3 next = transform.position + transform.forward * currentSpeed * dt;
        bool vulnerable = LivesManager.Instance == null || LivesManager.Instance.CanCrash;
        if (vulnerable && HitsSolid(transform.position, next))
        {
            if (LivesManager.Instance != null)
                LivesManager.Instance.Crash();
            else
                ResetPlane();
            return;
        }

        transform.position = next;
    }

    bool HitsSolid(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance > 0.001f && Physics.SphereCast(from, 1.25f, delta / distance, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                return true;
        }

        int count = Physics.OverlapSphereNonAlloc(to, 1.1f, OverlapHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = OverlapHits[i];
            if (collider != null && !collider.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    public void AddSteer(float pitchDegrees, float yawDegrees)
    {
        externalPitch += pitchDegrees;
        externalYaw += yawDegrees;
    }

    public void Place(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation;
        ResetPlane();
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        flightRotation = rotation;
        bankAngle = 0f;
        currentSpeed = speed;
        speedVelocity = 0f;
        externalPitch = 0f;
        externalYaw = 0f;
    }

    public void ResetPlane()
    {
        Teleport(spawnPosition, spawnRotation);
    }

    public void SetRenderersVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }
    }

    public void SetSolidColliders(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && !collider.isTrigger)
                collider.enabled = enabled;
        }
    }
}
