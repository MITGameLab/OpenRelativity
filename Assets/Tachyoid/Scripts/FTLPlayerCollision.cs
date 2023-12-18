using UnityEngine;
using OpenRelativity;
using OpenRelativity.Objects;

namespace Tachyoid
{
    public class FTLPlayerCollision : MonoBehaviour
    {
        public FTLPlayerController playerCtrl;
        private TachyoidGameState state;

        void Start()
        {
            state = playerCtrl.GetComponent<TachyoidGameState>();
        }


        //This is a reference type to package collision points with collision normal vectors
        private class PointAndNorm
        {
            public Vector3 point;
            public Vector3 normal;
        }

        private PointAndNorm DecideContactPoint(Collision collision)
        {
            PointAndNorm contactPoint;
            if (collision.contacts.Length == 0)
            {
                return null;
            }
            else if (collision.contacts.Length == 1)
            {
                contactPoint = new PointAndNorm()
                {
                    point = collision.contacts[0].point,
                    normal = collision.contacts[0].normal
                };
            }
            else
            {
                contactPoint = new PointAndNorm();
                for (int i = 0; i < collision.contacts.Length; i++)
                {
                    contactPoint.point += collision.contacts[i].point;
                    contactPoint.normal += collision.contacts[i].normal;
                }
                contactPoint.point = 1.0f / collision.contacts.Length * contactPoint.point;
                contactPoint.normal.Normalize();
            }
            if ((contactPoint.point - playerCtrl.transform.position).sqrMagnitude == 0.0f)
            {
                contactPoint.point = 0.001f * collision.collider.transform.position;
            }
            return contactPoint;
        }

        void OnCollisionEnter(Collision collision)
        {
            OnCollision(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            OnCollision(collision);
        }

        private void OnCollision(Collision collision)
        {
            Rigidbody otherRB = collision.collider.GetComponent<Rigidbody>();
            RelativisticObject otherRO = collision.collider.GetComponent<RelativisticObject>();
            if (otherRO != null && otherRB != null && otherRB.isKinematic)
            {
                PointAndNorm contactPoint = DecideContactPoint(collision);
                Vector3 extents = GetComponent<Collider>().bounds.extents;
                float dist = 0.0f;
                if (Vector3.Dot(contactPoint.normal, Vector3.up) > 0.95)
                {
                    dist = extents.y - (contactPoint.point.y - playerCtrl.transform.position.y);
                    if (dist > 0.0f)
                    {
                        state.playerTransform.position -= dist * Vector3.down;
                    }

                    state.IsPlayerFalling = false;
                    Vector3 pVel = state.PlayerVelocityVector;
                    if (pVel.y > 0.0f)
                    {
                        state.PlayerVelocityVector = state.PlayerVelocityVector.AddVelocity(new Vector3(0.0f, -pVel.y, 0.0f));
                    }
                    bool foundCollider = false;
                    for (int i = 0; i < playerCtrl.collidersBelow.Count; i++)
                    {
                        if (playerCtrl.collidersBelow[i].Equals(collision.collider))
                        {
                            foundCollider = true;
                        }
                    }

                    if (!foundCollider)
                    {
                        playerCtrl.collidersBelow.Add(collision.collider);
                    }
                }
                else
                {
                    dist = (extents - (contactPoint.point - playerCtrl.transform.position)).magnitude;
                    Vector3 pVel = state.PlayerVelocityVector;
                    if (Vector3.Dot(pVel, contactPoint.normal) < 0.0f)
                    {
                        //Decompose velocity in parallel and perpendicular components:
                        Vector3 myParraVel = Vector3.Project(pVel, contactPoint.normal);
                        //Vector3 myPerpVel = Vector3.Cross(direction, Vector3.Cross(direction, pVel));
                        //Relativistically cancel the downward velocity:
                        state.PlayerVelocityVector = state.PlayerVelocityVector.AddVelocity(-myParraVel);
                    }
                    state.playerTransform.position += dist * contactPoint.normal;
                }
            }
        }

        void OnCollisionExit(Collision collision)
        {
            playerCtrl.collidersBelow.Remove(collision.collider);
            if (playerCtrl.collidersBelow.Count == 0)
            {
                state.IsPlayerFalling = true;
            }
        }
    }
}
