using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CarStuckRecovery : MonoBehaviour
{
    [Header("Availability")]
    [SerializeField] private bool alwaysAvailable;
    [SerializeField, Min(0f)] private float stationaryDelay = 5f;
    [SerializeField, Min(0f)] private float stationarySpeedThreshold = 0.75f;
    [SerializeField, Min(0f)] private float stationaryDriftRadius = 1.25f;

    [Header("Destination Search")]
    [SerializeField, Min(1f)] private float minimumSearchDistance = 7f;
    [SerializeField, Min(1f)] private float maximumSearchDistance = 18f;
    [SerializeField, Range(4, 24)] private int searchDirections = 12;
    [SerializeField, Range(0f, 1f)] private float minimumGroundUpDot = 0.92f;
    [SerializeField, Min(1f)] private float raycastHeight = 24f;
    [SerializeField, Min(1f)] private float raycastDistance = 80f;
    [SerializeField, Min(0f)] private float destinationClearance = 0.75f;

    [Header("Recovery Motion")]
    [SerializeField, Min(0.1f)] private float liftHeight = 5f;
    [SerializeField, Min(0.1f)] private float relocationDuration = 1.8f;

    [Header("UI")]
    [SerializeField] private Text availabilityPrompt;

    private readonly RaycastHit[] m_RaycastHits = new RaycastHit[32];
    private readonly Collider[] m_OverlapHits = new Collider[32];

    private Rigidbody m_Body;
    private CarUserControl m_UserControl;
    private CarRoofRecovery m_RoofRecovery;
    private InfiniteBackgroundTerrain m_Terrain;

    private float m_StationaryTime;
    private Vector3 m_StationaryAnchor;
    private Vector3 m_InitialPosition;
    private float m_BodyBottomOffset;
    private float m_ClearanceRadius;
    private bool m_HasDriven;

    private bool m_IsRecovering;
    private bool m_OriginalKinematic;
    private bool m_UserControlWasEnabled;
    private bool m_RoofRecoveryWasEnabled;

    private RecoveryPhase m_Phase;
    private float m_PhaseTime;
    private Vector3 m_StartPosition;
    private Vector3 m_LiftedStartPosition;
    private Vector3 m_LiftedTargetPosition;
    private Vector3 m_FinalPosition;
    private Quaternion m_StartRotation;
    private Quaternion m_FinalRotation;

    public bool CanRecover => !m_IsRecovering && (alwaysAvailable || (m_HasDriven && m_StationaryTime >= stationaryDelay));
    public bool IsRecovering => m_IsRecovering;
    public float StationaryTime => m_StationaryTime;
    public bool HasDriven => m_HasDriven;

    private float LiftDuration => Mathf.Max(0.025f, relocationDuration * 0.25f);
    private float TravelDuration => Mathf.Max(0.05f, relocationDuration * 0.5f);
    private float LowerDuration => Mathf.Max(0.025f, relocationDuration * 0.25f);

    private enum RecoveryPhase
    {
        Lift,
        Travel,
        Lower
    }

    private void Awake()
    {
        m_Body = GetComponent<Rigidbody>();
        m_UserControl = GetComponent<CarUserControl>();
        m_RoofRecovery = GetComponent<CarRoofRecovery>();
        m_Terrain = FindFirstObjectByType<InfiniteBackgroundTerrain>();
        m_StationaryAnchor = m_Body.position;
        m_InitialPosition = m_Body.position;
        CacheVehicleDimensions();
        UpdateAvailabilityPrompt();
    }

    private void Update()
    {
        UpdateAvailabilityPrompt();

        if (m_IsRecovering || !CanRecover)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            TryBeginRecovery();
        }
    }

    private void FixedUpdate()
    {
        if (m_IsRecovering)
        {
            AdvanceRecoveryMotion();
            return;
        }

        UpdateDrivenState();
        UpdateStationaryTimer();
    }

    private void OnDisable()
    {
        SetAvailabilityPromptVisible(false);
    }

    private void UpdateDrivenState()
    {
        if (m_HasDriven)
            return;

        Vector3 planarVelocity = m_Body.linearVelocity;
        planarVelocity.y = 0f;
        float drivingSpeed = Mathf.Max(2f, stationarySpeedThreshold * 2f);
        if (planarVelocity.sqrMagnitude >= drivingSpeed * drivingSpeed)
        {
            m_HasDriven = true;
            ResetStationaryTimer();
            return;
        }

        Vector3 planarDistance = m_Body.position - m_InitialPosition;
        planarDistance.y = 0f;
        if (planarDistance.sqrMagnitude >= 4f)
        {
            m_HasDriven = true;
            ResetStationaryTimer();
        }
    }

    private void UpdateStationaryTimer()
    {
        float thresholdSquared = stationarySpeedThreshold * stationarySpeedThreshold;
        if (m_Body.linearVelocity.sqrMagnitude > thresholdSquared)
        {
            ResetStationaryTimer();
            return;
        }

        Vector3 planarDelta = m_Body.position - m_StationaryAnchor;
        planarDelta.y = 0f;
        if (planarDelta.sqrMagnitude > stationaryDriftRadius * stationaryDriftRadius)
        {
            ResetStationaryTimer();
            return;
        }

        m_StationaryTime += Time.fixedDeltaTime;
    }

    private void ResetStationaryTimer()
    {
        m_StationaryTime = 0f;
        m_StationaryAnchor = m_Body.position;
    }

    private void CacheVehicleDimensions()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: false);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            m_BodyBottomOffset = 0.8f;
            m_ClearanceRadius = 2f;
            return;
        }

        m_BodyBottomOffset = Mathf.Clamp(m_Body.position.y - bounds.min.y + destinationClearance, 0.6f, 2.5f);
        m_ClearanceRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.5f;
    }

    private bool TryBeginRecovery()
    {
        if (m_Terrain == null)
        {
            m_Terrain = FindFirstObjectByType<InfiniteBackgroundTerrain>();
            if (m_Terrain == null)
                return false;
        }

        if (!TryFindRecoveryTarget(out Vector3 groundPoint))
            return false;

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.01f)
        {
            flatForward = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        }
        if (flatForward.sqrMagnitude < 0.01f)
        {
            flatForward = Vector3.forward;
        }
        flatForward.Normalize();

        m_StartPosition = m_Body.position;
        m_StartRotation = m_Body.rotation;
        m_FinalRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        m_FinalPosition = groundPoint + Vector3.up * m_BodyBottomOffset;
        m_LiftedStartPosition = m_StartPosition + Vector3.up * liftHeight;
        m_LiftedTargetPosition = m_FinalPosition + Vector3.up * liftHeight;

        m_UserControlWasEnabled = m_UserControl != null && m_UserControl.enabled;
        m_RoofRecoveryWasEnabled = m_RoofRecovery != null && m_RoofRecovery.enabled;
        if (m_UserControl != null)
            m_UserControl.enabled = false;
        if (m_RoofRecovery != null)
            m_RoofRecovery.enabled = false;

        m_OriginalKinematic = m_Body.isKinematic;
        m_Body.linearVelocity = Vector3.zero;
        m_Body.angularVelocity = Vector3.zero;
        m_Body.isKinematic = true;

        m_IsRecovering = true;
        m_Phase = RecoveryPhase.Lift;
        m_PhaseTime = 0f;
        UpdateAvailabilityPrompt();
        return true;
    }

    private bool TryFindRecoveryTarget(out Vector3 groundPoint)
    {
        groundPoint = default;
        Transform terrainRoot = m_Terrain.transform;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        const int ringCount = 3;

        for (int ring = 0; ring < ringCount; ring++)
        {
            float distance = Mathf.Lerp(minimumSearchDistance, maximumSearchDistance, ring / (float)(ringCount - 1));

            for (int directionIndex = 0; directionIndex < searchDirections; directionIndex++)
            {
                float angle = directionIndex * Mathf.PI * 2f / searchDirections;
                Vector3 direction = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                Vector3 sample = m_Body.position + direction * distance;
                Vector3 rayOrigin = sample + Vector3.up * raycastHeight;

                int hitCount = Physics.RaycastNonAlloc(
                    rayOrigin,
                    Vector3.down,
                    m_RaycastHits,
                    raycastDistance,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = m_RaycastHits[hitIndex];
                    if (hit.collider == null || !hit.collider.transform.IsChildOf(terrainRoot))
                        continue;

                    float upDot = Vector3.Dot(hit.normal, Vector3.up);
                    if (upDot < minimumGroundUpDot)
                        continue;

                    if (!IsDestinationClear(hit.point, terrainRoot))
                        continue;

                    float score = upDot * 100f - distance * 0.5f;
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    groundPoint = hit.point;
                    found = true;
                }
            }
        }

        return found;
    }

    private bool IsDestinationClear(Vector3 groundPoint, Transform terrainRoot)
    {
        Vector3 center = groundPoint + Vector3.up * Mathf.Max(m_BodyBottomOffset, 1f);
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            m_ClearanceRadius,
            m_OverlapHits,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = m_OverlapHits[i];
            if (collider == null)
                continue;

            Transform hitTransform = collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(terrainRoot))
                continue;

            return false;
        }

        return true;
    }

    private void AdvanceRecoveryMotion()
    {
        m_PhaseTime += Time.fixedDeltaTime;

        switch (m_Phase)
        {
            case RecoveryPhase.Lift:
            {
                float t = Smooth01(m_PhaseTime / LiftDuration);
                m_Body.MovePosition(Vector3.LerpUnclamped(m_StartPosition, m_LiftedStartPosition, t));
                m_Body.MoveRotation(Quaternion.Slerp(m_StartRotation, m_FinalRotation, t));

                if (m_PhaseTime >= LiftDuration)
                    BeginPhase(RecoveryPhase.Travel);
                break;
            }

            case RecoveryPhase.Travel:
            {
                float t = Smooth01(m_PhaseTime / TravelDuration);
                Vector3 position = Vector3.LerpUnclamped(m_LiftedStartPosition, m_LiftedTargetPosition, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.75f;
                m_Body.MovePosition(position);
                m_Body.MoveRotation(m_FinalRotation);

                if (m_PhaseTime >= TravelDuration)
                    BeginPhase(RecoveryPhase.Lower);
                break;
            }

            case RecoveryPhase.Lower:
            {
                float t = Smooth01(m_PhaseTime / LowerDuration);
                m_Body.MovePosition(Vector3.LerpUnclamped(m_LiftedTargetPosition, m_FinalPosition, t));
                m_Body.MoveRotation(m_FinalRotation);

                if (m_PhaseTime >= LowerDuration)
                    CompleteRecovery();
                break;
            }
        }
    }

    private void BeginPhase(RecoveryPhase phase)
    {
        m_Phase = phase;
        m_PhaseTime = 0f;
    }

    private void CompleteRecovery()
    {
        m_Body.position = m_FinalPosition;
        m_Body.rotation = m_FinalRotation;
        m_Body.linearVelocity = Vector3.zero;
        m_Body.angularVelocity = Vector3.zero;
        m_Body.isKinematic = m_OriginalKinematic;
        m_Body.WakeUp();

        if (m_UserControl != null)
            m_UserControl.enabled = m_UserControlWasEnabled;
        if (m_RoofRecovery != null)
            m_RoofRecovery.enabled = m_RoofRecoveryWasEnabled;

        m_IsRecovering = false;
        ResetStationaryTimer();
        UpdateAvailabilityPrompt();
    }

    private void UpdateAvailabilityPrompt()
    {
        SetAvailabilityPromptVisible(CanRecover);
    }

    private void SetAvailabilityPromptVisible(bool visible)
    {
        if (availabilityPrompt == null)
            return;

        if (availabilityPrompt.text != "R to move your car")
            availabilityPrompt.text = "R to move your car";

        if (availabilityPrompt.gameObject.activeSelf != visible)
            availabilityPrompt.gameObject.SetActive(visible);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
