using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityStandardAssets.CrossPlatformInput
{
    public class AxisTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        // designed to work in a pair with another axis touch button
        // (typically with one having -1 and one having 1 axisValues)
        public string axisName = "Horizontal"; // The name of the axis
        public float axisValue = 1; // The axis that the value has
        public float responseSpeed = 3; // The speed at which the axis touch button responds
        public float returnToCentreSpeed = 3; // The speed at which the button will return to its centre

        AxisTouchButton m_PairedWith; // Which button this one is paired with
        CrossPlatformInputManager.VirtualAxis m_Axis; // A reference to the virtual axis as it is in the cross platform input
        bool m_OwnsAxis;

        void OnEnable()
        {
            m_OwnsAxis = false;
            if (!CrossPlatformInputManager.AxisExists(axisName))
            {
                // if the axis doesn't exist create a new one in cross platform input
                m_Axis = new CrossPlatformInputManager.VirtualAxis(axisName);
                CrossPlatformInputManager.RegisterVirtualAxis(m_Axis);
                m_OwnsAxis = true;
            }
            else
            {
                m_Axis = CrossPlatformInputManager.VirtualAxisReference(axisName);
            }
            FindPairedButton();
        }

        void FindPairedButton()
        {
            m_PairedWith = null;
            // find the other button which this button should be paired
            // (it should have the same axisName)
            var otherAxisButtons = FindObjectsByType<AxisTouchButton>(FindObjectsSortMode.None);

            if (otherAxisButtons != null)
            {
                for (int i = 0; i < otherAxisButtons.Length; i++)
                {
                    if (otherAxisButtons[i] != this && otherAxisButtons[i].isActiveAndEnabled &&
                        otherAxisButtons[i].axisName == axisName &&
                        ReferenceEquals(otherAxisButtons[i].m_Axis, m_Axis))
                    {
                        m_PairedWith = otherAxisButtons[i];
                    }
                }
            }
        }

        void OnDisable()
        {
            if (m_OwnsAxis && m_Axis != null &&
                ReferenceEquals(CrossPlatformInputManager.VirtualAxisReference(m_Axis.name), m_Axis))
            {
                // Keep the shared axis registered while another button is still using it.
                FindPairedButton();
                if (m_PairedWith != null)
                {
                    m_PairedWith.m_OwnsAxis = true;
                }
                else
                {
                    m_Axis.Remove();
                }
            }
            m_OwnsAxis = false;
            m_Axis = null;
            m_PairedWith = null;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (m_Axis == null) return;
            if (m_PairedWith == null)
            {
                FindPairedButton();
            }
            // update the axis and record that the button has been pressed this frame
            m_Axis.Update(Mathf.MoveTowards(m_Axis.GetValue, axisValue, responseSpeed * Time.deltaTime));
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (m_Axis == null) return;
            m_Axis.Update(Mathf.MoveTowards(m_Axis.GetValue, 0, responseSpeed * Time.deltaTime));
        }
    }
}
