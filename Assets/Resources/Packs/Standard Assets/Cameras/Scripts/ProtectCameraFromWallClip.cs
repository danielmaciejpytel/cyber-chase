using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityStandardAssets.Cameras
{
    public class ProtectCameraFromWallClip : MonoBehaviour
    {
        public float clipMoveTime = 0.05f;              // time taken to move when avoiding cliping (low value = fast, which it should be)
        public float returnTime = 0.4f;                 // time taken to move back towards desired position, when not clipping (typically should be a higher value than clipMoveTime)
        public float sphereCastRadius = 0.1f;           // the radius of the sphere used to test for object between camera and target
        public bool visualiseInEditor;                  // toggle for visualising the algorithm through lines for the raycast in the editor
        public float closestDistance = 0.5f;            // the closest distance the camera can be from the target
        public bool protecting { get; private set; }    // used for determining if there is an object between the target and the camera
        public string dontClipTag = "Player";           // don't clip against objects with this tag (useful for not clipping against the targeted object)

        private Transform m_Cam;                  // the transform of the camera
        private Transform m_Pivot;                // the point at which the camera pivots around
        private float m_OriginalDist;             // the original distance to the camera before any modification are made
        private float m_MoveVelocity;             // the velocity at which the camera moved
        private float m_CurrentDist;              // the current distance from the camera to the target
        private Ray m_Ray = new Ray();                        // the ray used in the lateupdate for casting between the camera and the target
        private Collider[] m_OverlapResults = new Collider[16];
        private RaycastHit[] m_Hits = new RaycastHit[16]; // the hits between the camera and the target
        private RayHitComparer m_RayHitComparer;  // variable to compare raycast hit distances


        private void Start()
        {
            // find the camera in the object hierarchy
            m_Cam = GetComponentInChildren<Camera>().transform;
            m_Pivot = m_Cam.parent;
            m_OriginalDist = m_Cam.localPosition.magnitude;
            m_CurrentDist = m_OriginalDist;

            // create a new RayHitComparer
            m_RayHitComparer = new RayHitComparer();
        }


        private void LateUpdate()
        {
            // initially set the target distance
            float targetDist = m_OriginalDist;

            m_Ray.origin = m_Pivot.position + m_Pivot.forward*sphereCastRadius;
            m_Ray.direction = -m_Pivot.forward;

            // initial check to see if start of spherecast intersects anything
            int overlapCount = GetOverlapCount();

            bool initialIntersect = false;
            bool hitSomething = false;

            // loop through all the collisions to check if something we care about
            for (int i = 0; i < overlapCount; i++)
            {
                var collider = m_OverlapResults[i];
                var attachedRigidbody = collider.attachedRigidbody;
                if ((!collider.isTrigger) &&
                    !(attachedRigidbody != null && attachedRigidbody.CompareTag(dontClipTag)))
                {
                    initialIntersect = true;
                    break;
                }
            }
            Array.Clear(m_OverlapResults, 0, overlapCount);

            // if there is a collision
            if (initialIntersect)
            {
                m_Ray.origin += m_Pivot.forward*sphereCastRadius;
            }
            int hitCount = GetHitCount(initialIntersect);

            // sort the collisions by distance
            Array.Sort<RaycastHit>(m_Hits, 0, hitCount, m_RayHitComparer);

            // set the variable used for storing the closest to be as far as possible
            float nearest = Mathf.Infinity;

            // loop through all the collisions
            for (int i = 0; i < hitCount; i++)
            {
                // only deal with the collision if it was closer than the previous one, not a trigger, and not attached to a rigidbody tagged with the dontClipTag
                if (m_Hits[i].distance < nearest && (!m_Hits[i].collider.isTrigger) &&
                    !(m_Hits[i].collider.attachedRigidbody != null &&
                      m_Hits[i].collider.attachedRigidbody.CompareTag(dontClipTag)))
                {
                    // change the nearest collision to latest
                    nearest = m_Hits[i].distance;
                    targetDist = -m_Pivot.InverseTransformPoint(m_Hits[i].point).z;
                    hitSomething = true;
                }
            }

            // visualise the cam clip effect in the editor
            if (hitSomething)
            {
                Debug.DrawRay(m_Ray.origin, -m_Pivot.forward*(targetDist + sphereCastRadius), Color.red);
            }

            // hit something so move the camera to a better position
            protecting = hitSomething;
            m_CurrentDist = Mathf.SmoothDamp(m_CurrentDist, targetDist, ref m_MoveVelocity,
                                           m_CurrentDist > targetDist ? clipMoveTime : returnTime);
            m_CurrentDist = Mathf.Clamp(m_CurrentDist, closestDistance, m_OriginalDist);
            m_Cam.localPosition = -Vector3.forward*m_CurrentDist;
        }


        private int GetOverlapCount()
        {
            int count = Physics.OverlapSphereNonAlloc(m_Ray.origin, sphereCastRadius, m_OverlapResults);
            if (count == m_OverlapResults.Length)
            {
                // A full buffer may omit a relevant collider. Use the complete query for this frame
                // and keep extra capacity so the same density does not allocate again next frame.
                var overlaps = Physics.OverlapSphere(m_Ray.origin, sphereCastRadius);
                m_OverlapResults = new Collider[Mathf.Max(m_OverlapResults.Length*2, overlaps.Length + 1)];
                Array.Copy(overlaps, m_OverlapResults, overlaps.Length);
                count = overlaps.Length;
            }
            return count;
        }


        private int GetHitCount(bool initialIntersect)
        {
            int count = initialIntersect
                ? Physics.RaycastNonAlloc(m_Ray, m_Hits, m_OriginalDist - sphereCastRadius)
                : Physics.SphereCastNonAlloc(m_Ray, sphereCastRadius, m_Hits, m_OriginalDist + sphereCastRadius);
            if (count == m_Hits.Length)
            {
                // NonAlloc queries do not guarantee the nearest hits when their buffer is full.
                var hits = initialIntersect
                    ? Physics.RaycastAll(m_Ray, m_OriginalDist - sphereCastRadius)
                    : Physics.SphereCastAll(m_Ray, sphereCastRadius, m_OriginalDist + sphereCastRadius);
                m_Hits = new RaycastHit[Mathf.Max(m_Hits.Length*2, hits.Length + 1)];
                Array.Copy(hits, m_Hits, hits.Length);
                count = hits.Length;
            }
            return count;
        }


        // comparer for check distances in ray cast hits
        public class RayHitComparer : IComparer, IComparer<RaycastHit>
        {
            public int Compare(object x, object y)
            {
                return Compare((RaycastHit) x, (RaycastHit) y);
            }


            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}
