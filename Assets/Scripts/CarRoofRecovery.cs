using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CarRoofRecovery : MonoBehaviour
{
    [SerializeField, Range(-1f, 1f)] private float activationUpDot = -0.15f;
    [SerializeField, Range(-1f, 1f)] private float releaseUpDot = 0.75f;
    [SerializeField, Min(0f)] private float rollAcceleration = 4f;
    [SerializeField, Min(0.1f)] private float maxRollAngularSpeed = 1.8f;

    private Rigidbody m_Body;
    private bool m_IsRecovering;

    private void Awake()
    {
        m_Body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.up);

        if (!m_IsRecovering)
        {
            if (upDot > activationUpDot)
                return;

            m_IsRecovering = true;
        }
        else if (upDot >= releaseUpDot)
        {
            m_IsRecovering = false;
            return;
        }

        float rollInput = ReadKeyboardRollInput();
        if (Mathf.Approximately(rollInput, 0f))
            return;

        Vector3 rollAxis = transform.forward.normalized;
        float currentRollSpeed = Vector3.Dot(m_Body.angularVelocity, rollAxis);

        if (Mathf.Sign(currentRollSpeed) == Mathf.Sign(rollInput) &&
            Mathf.Abs(currentRollSpeed) >= maxRollAngularSpeed)
        {
            return;
        }

        m_Body.WakeUp();
        m_Body.AddTorque(rollAxis * (rollInput * rollAcceleration), ForceMode.Acceleration);
    }

    private static float ReadKeyboardRollInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return 0f;

        bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;

        if (left == right)
            return 0f;

        return right ? 1f : -1f;
    }
}
