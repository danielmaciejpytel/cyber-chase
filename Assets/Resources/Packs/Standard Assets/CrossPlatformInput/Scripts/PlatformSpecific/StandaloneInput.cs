using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UnityStandardAssets.CrossPlatformInput.PlatformSpecific
{
    /// <summary>
    /// Hardware input bridge for the legacy Standard Assets CrossPlatformInput API.
    ///
    /// The public API is intentionally kept intact because the car/camera code still
    /// consumes CrossPlatformInputManager. Hardware reads, however, come exclusively
    /// from Unity's Input System package so the project no longer requires the legacy
    /// Input Manager backend.
    /// </summary>
    public class StandaloneInput : VirtualInput
    {
        private const float KeyboardGravity = 3f;
        private const float KeyboardSensitivity = 3f;
        private const float JoystickDeadZone = 0.19f;
        private const float MouseSensitivity = 0.1f;

        private float m_Horizontal;
        private float m_Vertical;
        private int m_LastSmoothedFrame = -1;

        public override float GetAxis(string name, bool raw)
        {
            switch (name)
            {
                case "Horizontal":
                    return ReadMovementAxis(horizontal: true, raw);
                case "Vertical":
                    return ReadMovementAxis(horizontal: false, raw);
                case "Jump":
                    return GetButton("Jump") ? 1f : 0f;
                case "Mouse X":
                    return ReadMouseDelta().x * MouseSensitivity;
                case "Mouse Y":
                    return ReadMouseDelta().y * MouseSensitivity;
                case "Mouse ScrollWheel":
                    return Mouse.current != null ? Mouse.current.scroll.ReadValue().y * MouseSensitivity : 0f;
                default:
                    return 0f;
            }
        }

        public override bool GetButton(string name)
        {
            return ReadButton(name, ButtonState.Held);
        }

        public override bool GetButtonDown(string name)
        {
            return ReadButton(name, ButtonState.PressedThisFrame);
        }

        public override bool GetButtonUp(string name)
        {
            return ReadButton(name, ButtonState.ReleasedThisFrame);
        }

        public override void SetButtonDown(string name)
        {
            throw UnsupportedStandaloneMutation();
        }

        public override void SetButtonUp(string name)
        {
            throw UnsupportedStandaloneMutation();
        }

        public override void SetAxisPositive(string name)
        {
            throw UnsupportedStandaloneMutation();
        }

        public override void SetAxisNegative(string name)
        {
            throw UnsupportedStandaloneMutation();
        }

        public override void SetAxisZero(string name)
        {
            throw UnsupportedStandaloneMutation();
        }

        public override void SetAxis(string name, float value)
        {
            throw UnsupportedStandaloneMutation();
        }

        public override Vector3 MousePosition()
        {
            if (Mouse.current == null)
                return Vector3.zero;

            Vector2 position = Mouse.current.position.ReadValue();
            return new Vector3(position.x, position.y, 0f);
        }

        private float ReadMovementAxis(bool horizontal, bool raw)
        {
            float keyboardRaw = ReadKeyboardAxis(horizontal);
            float controller = ReadControllerAxis(horizontal);

            if (raw)
                return LargestMagnitude(keyboardRaw, controller);

            UpdateSmoothedKeyboardAxes();
            float keyboard = horizontal ? m_Horizontal : m_Vertical;
            return LargestMagnitude(keyboard, controller);
        }

        private void UpdateSmoothedKeyboardAxes()
        {
            if (m_LastSmoothedFrame == Time.frameCount)
                return;

            m_LastSmoothedFrame = Time.frameCount;
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
                deltaTime = Time.fixedUnscaledDeltaTime;

            m_Horizontal = SmoothKeyboardAxis(m_Horizontal, ReadKeyboardAxis(horizontal: true), deltaTime);
            m_Vertical = SmoothKeyboardAxis(m_Vertical, ReadKeyboardAxis(horizontal: false), deltaTime);
        }

        private static float SmoothKeyboardAxis(float current, float target, float deltaTime)
        {
            // InputManager defaults for Horizontal/Vertical were gravity=3,
            // sensitivity=3 and snap=true. Preserve that response curve.
            if (target != 0f)
            {
                if (current != 0f && Mathf.Sign(current) != Mathf.Sign(target))
                    current = 0f;

                return Mathf.MoveTowards(current, target, KeyboardSensitivity * deltaTime);
            }

            return Mathf.MoveTowards(current, 0f, KeyboardGravity * deltaTime);
        }

        private static float ReadKeyboardAxis(bool horizontal)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return 0f;

            bool negative = horizontal
                ? keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed
                : keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
            bool positive = horizontal
                ? keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed
                : keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;

            if (negative == positive)
                return 0f;
            return positive ? 1f : -1f;
        }

        private static float ReadControllerAxis(bool horizontal)
        {
            float value = 0f;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float gamepadValue = horizontal
                    ? gamepad.leftStick.x.ReadUnprocessedValue()
                    : gamepad.leftStick.y.ReadUnprocessedValue();
                value = ApplyLegacyDeadZone(gamepadValue);
            }

            Joystick joystick = Joystick.current;
            if (joystick != null)
            {
                float joystickValue = horizontal
                    ? joystick.stick.x.ReadUnprocessedValue()
                    : joystick.stick.y.ReadUnprocessedValue();
                joystickValue = ApplyLegacyDeadZone(joystickValue);
                value = LargestMagnitude(value, joystickValue);
            }

            return value;
        }

        private static float ApplyLegacyDeadZone(float value)
        {
            return Mathf.Abs(value) < JoystickDeadZone ? 0f : Mathf.Clamp(value, -1f, 1f);
        }

        private static Vector2 ReadMouseDelta()
        {
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }

        private static float LargestMagnitude(float first, float second)
        {
            return Mathf.Abs(second) > Mathf.Abs(first) ? second : first;
        }

        private static bool ReadButton(string name, ButtonState state)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            switch (name)
            {
                case "Jump":
                    // Legacy InputManager: Space or joystick button 3 (Y / North).
                    return Any(state, keyboard?.spaceKey, gamepad?.buttonNorth);
                case "Submit":
                    return Any(state, keyboard?.enterKey, keyboard?.spaceKey, gamepad?.buttonSouth);
                case "Cancel":
                    return Any(state, keyboard?.escapeKey, gamepad?.buttonEast);
                case "Fire1":
                    return Any(state, keyboard?.leftCtrlKey, mouse?.leftButton, gamepad?.buttonSouth);
                case "Fire2":
                    return Any(state, keyboard?.leftAltKey, mouse?.rightButton, gamepad?.buttonEast);
                case "Fire3":
                    return Any(state, keyboard?.leftShiftKey, mouse?.middleButton, gamepad?.buttonWest);
                default:
                    return false;
            }
        }

        private static bool Any(ButtonState state, params ButtonControl[] controls)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                ButtonControl control = controls[i];
                if (control == null)
                    continue;

                switch (state)
                {
                    case ButtonState.Held:
                        if (control.isPressed) return true;
                        break;
                    case ButtonState.PressedThisFrame:
                        if (control.wasPressedThisFrame) return true;
                        break;
                    case ButtonState.ReleasedThisFrame:
                        if (control.wasReleasedThisFrame) return true;
                        break;
                }
            }

            return false;
        }

        private static Exception UnsupportedStandaloneMutation()
        {
            return new InvalidOperationException(
                "Hardware input cannot be mutated through StandaloneInput. Use the touch/virtual input backend for synthetic input.");
        }

        private enum ButtonState
        {
            Held,
            PressedThisFrame,
            ReleasedThisFrame
        }
    }
}
