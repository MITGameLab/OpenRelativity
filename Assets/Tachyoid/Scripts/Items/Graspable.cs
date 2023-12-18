using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using OpenRelativity;
using OpenRelativity.Objects;
using Tachyoid.TimeReversal.Generic;

namespace Tachyoid.Objects {
	public class Graspable : MonoBehaviour, ITimeReversibleObject
    {
        private Vector3 localAnchorPoint = new Vector3(0.0f, -10.0f, 20.0f);
        //private float anchorSpringiness = 16.0f;
        //private float anchorRotSpeed = 360.0f * 2.0f;
        private GameState state;
        public bool isHeld { get; set; }
        private Transform holder;
        private RelativisticObject holderRO;
        //private Collider holderCollider;

        //private bool didReverseTime;
        public Vector3 playerPosOrig { get; set; }
        private bool isOriginal = true;
        //private double warpTime;

        private bool wasMovementFrozen;
        private HistoryPoint stateBeforeFrozen;

        private List<Graspable> images;
        public int CurrentImageIndex { get { return images.Count; } }
        private Dictionary<int, float> imageIndexDeathTimeDict;

        public Graspable GetImage(int index)
        {
            Graspable image = images[index];
            image.enabled = true;
            image.isOriginal = false;
            Rigidbody imageRB = image.GetComponent<Rigidbody>();
            if (imageRB != null) imageRB.isKinematic = false;
            Collider imageCollider = image.GetComponent<Collider>();
            if (imageCollider != null) imageCollider.enabled = true;
            Renderer imageRenderer = image.GetComponent<Renderer>();
            if (imageRenderer != null) imageRenderer.enabled = true;
            RelativisticObject imageRO = image.GetComponent<RelativisticObject>();
            if (imageRO != null)
            {
                if (!imageIndexDeathTimeDict.ContainsKey(index)) imageIndexDeathTimeDict.Add(index, imageRO.DeathTime);
                imageRO.ResetDeathTime();
                imageRO.enabled = true;
            }
            RelativisticObjectTimeReverser imageROTRev = image.GetComponent<RelativisticObjectTimeReverser>();
            if (imageROTRev != null) imageROTRev.enabled = false;
            image.gameObject.SetActive(true);
            return image;
        }

        public float GetImageDeathTime(int index)
        {
            return imageIndexDeathTimeDict[index];
        }

        private class HistoryPoint
        {
            public bool IsHeld { get; set; }
            public double WorldTime { get; set; }
            public Vector3 Piw { get; set; }
            public Quaternion Rotation { get; set; }
            public Vector3 OldViw { get; set; }
            public Vector3 OldAviw { get; set; }
            public Vector3 NewViw { get; set; }
            public Vector3 NewAviw { get; set; }
            public bool GraspableCollision { get; set; }
            public int ImageIndex { get; set; }
        }

        void Awake()
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            state = playerGO.GetComponent<GameState>();
            //warpTime = state.TotalTimeWorld;
            images = new List<Graspable>();
            imageIndexDeathTimeDict = new Dictionary<int, float>();

            isHeld = false;
            //didReverseTime = false;
            wasMovementFrozen = false;

            stateBeforeFrozen = new HistoryPoint();
        }

        // Use this for initialization
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            //Only let this flag stay for one frame.
            //didReverseTime = false;
            RelativisticObject myRO = GetComponent<RelativisticObject>();
            if (!isOriginal)
            {
                if (state.TotalTimeWorld > myRO.DeathTime + TachyoidConstants.WaitToDestroyHistory)
                {
                    Destroy(this.gameObject);
                }
                else if (state.TotalTimeWorld > myRO.DeathTime)
                {
                    GetComponent<Renderer>().enabled = false;
                }
                else if (!state.isMovementFrozen)
                {
                    GetComponent<Renderer>().enabled = true;
                    myRO.enabled = true;
                    Rigidbody myRB = GetComponent<Rigidbody>();
                    if (myRB != null) myRB.isKinematic = false;
                    GetComponent<RelativisticObjectTimeReverser>().UpdateTimeTravelPrediction();
                }
            }

            if (!state.isMovementFrozen)
            {
                wasMovementFrozen = false;
            }

            if (isHeld)
            {
                MoveDirectlyToRestingPoint();
                //if (isOriginal || doResetPos || myRO == null || transform.parent == holder)
                //{
                //    MoveDirectlyToRestingPoint();
                //}
                //else
                //{
                //    Vector3 playerPos = state.playerTransform.position;
                //    Vector3 playerVel = state.PlayerVelocityVector;
                //    Vector3 disp;
                //    Vector3 viw = holder.GetComponent<RelativisticObject>().viw;
                //    Vector3 relVel = viw.AddVelocity(-playerVel);
                //    disp = holder.position + (holder.rotation * localAnchorPoint).ContractLengthBy(relVel) - transform.position;
                //    double magnitude = disp.magnitude;
                //    Vector3 partViw = anchorSpringiness * (holder.TransformPoint(localAnchorPoint) - transform.position).normalized;
                //    if (partViw.sqrMagnitude < 0.25f * anchorSpringiness * anchorSpringiness)
                //    {
                //        MoveDirectlyToRestingPoint();
                //    }
                //    else
                //    {
                //        viw = viw.AddVelocity(partViw);
                //        //The objects "bounce" erratically if the velocity constantly changes:
                //        if ((viw - myRO.viw).sqrMagnitude > 1.0f)
                //        {
                //            myRO.viw = viw;
                //        }
                //    }
                //    double angle = state.DeltaTimeWorld * anchorRotSpead;
                //    if (Vector3.Angle(transform.forward, holder.forward) <= angle)
                //    {
                //        transform.forward = holder.forward;
                //    }
                //    else
                //    {
                //        transform.Rotate(Quaternion.AngleAxis(angle, Vector3.Cross(transform.forward, holder.forward)).eulerAngles, Space.Self);
                //    }
                //    myRO.aviw = Vector3.zero;
                //}
            }
        }

        public void MoveDirectlyToRestingPoint()
        {
            RelativisticObject myRO = GetComponent<RelativisticObject>();
            //myRO.enabled = false;
            Rigidbody myRB = GetComponent<Rigidbody>();
            myRB.isKinematic = true;
            Vector3 relVel;
            Vector3 newViw;
            if (holderRO == null)
            {
                newViw = state.PlayerVelocityVector;
                relVel = Vector3.zero;
            }
            else
            {
                newViw = holderRO.viw;
                relVel = newViw.AddVelocity(-state.PlayerVelocityVector);
            }
            myRO.aviw = Vector3.zero;

            transform.rotation = holder.rotation;

            //transform.parent = null;
            bool wasKinematic = myRO.isKinematic;
            myRO.isKinematic = true;
            myRO.piw = holder.position + (holder.rotation * localAnchorPoint).ContractLengthBy(relVel);
            myRO.viw = newViw;
            myRO.isKinematic = wasKinematic;
            //transform.parent = holder;
        }

        public void ChangeHolder(Transform parent, Collider playerCollider, RelativisticObject parentRO, Vector3 releaseVelocity = default(Vector3))
        {
            RelativisticObject myRO = GetComponent<RelativisticObject>();
            //Rigidbody myRB = GetComponent<Rigidbody>();
            Collider myCollider = GetComponent<Collider>();
            RelativisticObjectTimeReverser myROTRev = GetComponent<RelativisticObjectTimeReverser>();
            //releaseVelocity = releaseVel;
            if (parent != null)
            {
                //If we're using time reversal, drop a history image in place first
                if (isOriginal && myROTRev != null)
                {
                    MakeTimeReversalDupe();
                }

                if (playerCollider != null && myCollider != null)
                {
                    //holderCollider = playerCollider;
                    Physics.IgnoreCollision(playerCollider, myCollider);
                }
                //else
                //{
                //    holderCollider = null;
                //}
                holder = parent;
                holderRO = parentRO;
                isHeld = true;
                //transform.parent = parent;
            }
            else
            {
                isHeld = false;
                //transform.parent = null;
                if (playerCollider != null && myCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, myCollider, false);
                }
                //holderCollider = null;
                if (myRO != null)
                {
                    //Vector3 oldPos = myRO.position3;
                    myRO.viw = releaseVelocity == default(Vector3) ? Vector3.zero : releaseVelocity;
                    myRO.aviw = Vector3.zero;
                    //myRO.position3 = oldPos;
                    //Debug.Log ("Release velocity: " + myRB4.velocity3 + ", GroundPos: " + myRB4.groundPosition);
                }
                if (myROTRev != null)
                {
                    myROTRev.enabled = true;
                }
            }
        }

        private void FixPenetration(Collision collision)
        {
            PointAndNorm contactPoint = null;
            RaycastHit hitInfo;
            Vector3 testNormal = Vector3.up;
            float penTest;
            float penDist = 100.0f;
            float startDist;
            Collider myCollider = GetComponent<Collider>();
            foreach (ContactPoint point in collision.contacts)
            {
                testNormal = point.normal;
                startDist = 10.0f * Vector3.Dot(transform.lossyScale, transform.lossyScale);
                Ray ray = new Ray(transform.position + startDist * testNormal, -testNormal);
                if (collision.collider.Raycast(ray, out hitInfo, startDist * 2.0f))
                {
                    penTest = hitInfo.distance - startDist;
                    ray = new Ray(transform.position - startDist * testNormal, testNormal);
                    if (myCollider.Raycast(ray, out hitInfo, startDist))
                    {
                        penTest = ((startDist - hitInfo.distance) - penTest);
                    }
                    else
                    {
                        penTest = 0.0f;
                    }
                }
                else
                {
                    penTest = 100.0f;
                }
                if (penTest < penDist)
                {
                    penDist = penTest;
                    contactPoint = new PointAndNorm()
                    {
                        point = point.point,
                        normal = point.normal
                    };
                }
            }
            //penDist -= 0.05f;
            if (contactPoint != null || penDist > 0.0f)
            {
                Vector3 disp;
                try
                {
                    disp = penDist * contactPoint.normal;
                }
                catch
                {
                    return;
                }
                transform.position += disp;
            }
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (isHeld && collision.gameObject != holder && collision.transform != holder.transform.parent)
            {
                FixPenetration(collision);
            }
            //if (collision.gameObject.tag == "Button")
            //{
            //    lastPos = transform.position;
            //    lastForward = transform.forward;
            //}
        }

        public void OnCollisionStay(Collision collision)
        {
            if (isHeld && collision.gameObject != holder && collision.transform != holder.transform.parent)
            {
                FixPenetration(collision);
            }
            //if (collision.gameObject.tag == "Button")
            //{
            //    //If position and rotation are extremely close since the last frame, put the graspable to sleep:
            //    if ((lastPos - transform.position).sqrMagnitude < 0.0001 && Vector3.Dot(lastForward, transform.forward) > 0.999f)
            //    {
            //        myRO.Sleep();
            //        myRB4TRev.AddHistoryPoint(null);
            //    }
            //    else
            //    {
            //        lastPos = transform.position;
            //        lastForward = transform.forward;
            //    }
            //}
        }

        private class PointAndNorm
        {
            public Vector3 point;
            public Vector3 normal;
        }

        public void ReverseTime()
        {
            wasMovementFrozen = false;
            if (!isOriginal)
            {
                GetComponent<RelativisticObjectTimeReverser>().enabled = false;
            }
        }

        public void UpdateTimeTravelPrediction()
        {
            if (!wasMovementFrozen)
            {
                RelativisticObject myRO = GetComponent<RelativisticObject>();
                stateBeforeFrozen.IsHeld = isHeld;
                stateBeforeFrozen.NewViw = myRO.viw;
                stateBeforeFrozen.OldViw = myRO.viw;
                stateBeforeFrozen.NewAviw = myRO.aviw;
                stateBeforeFrozen.OldAviw = myRO.aviw;
                stateBeforeFrozen.Piw = myRO.transform.position;
                stateBeforeFrozen.Rotation = transform.rotation;
                stateBeforeFrozen.WorldTime = state.TotalTimeWorld;

                wasMovementFrozen = true;
            }
        }

        public void UndoTimeTravelPrediction()
        {
            RelativisticObject myRO = GetComponent<RelativisticObject>();
            isHeld = stateBeforeFrozen.IsHeld;
            if (isHeld)
            {
                myRO.aviw = stateBeforeFrozen.OldAviw;
                transform.rotation = stateBeforeFrozen.Rotation;

                bool wasKinematic = myRO.isKinematic;
                myRO.isKinematic = true;
                myRO.piw = stateBeforeFrozen.Piw;
                myRO.viw = stateBeforeFrozen.OldViw;
                myRO.isKinematic = wasKinematic;
            }
        }

        public Graspable MakeTimeReversalDupe()
        {
            GameObject dupeGraspableGO = Instantiate(this.gameObject);
            Graspable dupeGraspable = dupeGraspableGO.GetComponent<Graspable>();
            dupeGraspable.isOriginal = false;
            dupeGraspable.isHeld = false;
            dupeGraspable.holder = null;
            //dupeGraspable.originalGraspable = this;
            dupeGraspable.gameObject.layer = LayerMask.NameToLayer("No Button Trigger");
            dupeGraspable.transform.position = this.transform.position;
            Collider dupeGraspableCollider = dupeGraspableGO.GetComponent<Collider>();
            if (dupeGraspableCollider != null)
            {
                dupeGraspableCollider.enabled = false;
            }
            Renderer dupeGraspableRenderer = dupeGraspableGO.GetComponent<Renderer>();
            if (dupeGraspableRenderer != null)
            {
                dupeGraspableRenderer.enabled = false;
            }
            RelativisticObject dupeGraspableRO = dupeGraspableGO.GetComponent<RelativisticObject>();
            if (dupeGraspableRO != null)
            {
                dupeGraspableRO.enabled = false;
                dupeGraspableRO.SetDeathTime();
            }
            RelativisticObjectTimeReverser dupeGraspableROTRev = dupeGraspableGO.GetComponent<RelativisticObjectTimeReverser>();
            if (dupeGraspableROTRev != null)
            {
                dupeGraspableROTRev.enabled = false;
                dupeGraspableROTRev.AddHistoryPoint();

                RelativisticObjectTimeReverser myROTRev = this.GetComponent<RelativisticObjectTimeReverser>();
                myROTRev.ResetHistory();
            }
            Rigidbody dupeGraspableRB = dupeGraspableGO.GetComponent<Rigidbody>();
            if (dupeGraspableRB != null)
            {
                dupeGraspableRB.isKinematic = true;
            }

            images.Add(dupeGraspable);

            dupeGraspable.isOriginal = false;
            dupeGraspable.isHeld = false;
            dupeGraspable.holder = null;
            //dupeGraspable.originalGraspable = this;
            dupeGraspable.gameObject.layer = LayerMask.NameToLayer("No Button Trigger");
            dupeGraspable.transform.position = this.transform.position;

            return dupeGraspable;
        }

        void OnEnable()
        {
            gameObject.SetActive(true);
        }

        private IEnumerator WaitForUpdate()
        {
            yield return null;
        }
    }
}