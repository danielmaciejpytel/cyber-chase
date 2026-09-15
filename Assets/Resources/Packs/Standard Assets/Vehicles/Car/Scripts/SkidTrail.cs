using System;
using System.Collections;
using UnityEngine;

namespace UnityStandardAssets.Vehicles.Car
{
    public class SkidTrail : MonoBehaviour
    {
        [SerializeField] private float m_PersistTime;


        private IEnumerator Start()
        {
            while (true)
            {
                yield return null;

                var parent = transform.parent;
                if (parent == null || parent.parent == null)
                {
                    Destroy(gameObject, m_PersistTime);
                    yield break;
                }
            }
        }
    }
}
