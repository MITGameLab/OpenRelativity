using System.Collections.Generic;
using OpenRelativity;
using OpenRelativity.Objects;
using Tachyoid.TimeReversal.Generic;
using UnityEngine;

namespace Tachyoid.Objects
{
    public class RelativisticObjectTimeReverser : RelativisticBehavior, ITimeReversibleObject
    {
        private Vector3 oldPeculiarVel;
        private Vector3 oldAviw;
        private List<ROHistoryCollision> collisions;
        private RelativisticObject myRO;
        private bool wasMovementFrozen;
        private double oldWorldTime;
        private ROHistoryCollision stateBeforeFrozen;
        //private Vector3 playerPosOrig;
        //private float lastTime;
        //private float origTime;

        // Use this for initialization
        void Awake()
        {
            myRO = GetComponent<RelativisticObject>();
            oldPeculiarVel = myRO.peculiarVelocity;
            oldAviw = myRO.aviw;
            collisions = new List<ROHistoryCollision>();
            wasMovementFrozen = false;

            stateBeforeFrozen = new ROHistoryCollision();
        }

        public void AddHistoryPoint(double? worldTime = null)
        {
            if (worldTime == null) worldTime = state.TotalTimeWorld + myRO.localTimeOffset;
            collisions.Add(new ROHistoryCollision()
            {
                OldPeculiarVel = oldPeculiarVel,
                NewPeculiarVel = myRO.peculiarVelocity,
                OldAviw = oldAviw,
                NewAviw = myRO.aviw,
                Piw = transform.position,
                Rotation = transform.rotation,
                WorldTime = worldTime.Value
            });
        }

        // Update is called once per frame
        void Update()
        {
            if (!state.isMovementFrozen)
            {
                wasMovementFrozen = false;

                RelativisticObject myRO = GetComponent<RelativisticObject>();
                double worldTime = state.TotalTimeWorld +  myRO.localTimeOffset;
                //Detect any changes in velocity:
                if (!(myRO.peculiarVelocity.Equals(oldPeculiarVel)) || !(myRO.aviw.Equals(oldAviw)))
                {
                    AddHistoryPoint(worldTime);

                    oldPeculiarVel = myRO.peculiarVelocity;
                    oldAviw = myRO.aviw;
                }

                //Clear out history past recording limit
                while (collisions.Count > 0 && (collisions[0].WorldTime + TachyoidConstants.WaitToDestroyHistory) < worldTime)
                {
                    collisions.RemoveAt(0);
                }
            }
        }

        public void UpdateTimeTravelPrediction()
        {
            RelativisticObject myRO = GetComponent<RelativisticObject>();
            if (!wasMovementFrozen)
            {
                stateBeforeFrozen.NewPeculiarVel = myRO.peculiarVelocity;
                stateBeforeFrozen.OldPeculiarVel = myRO.peculiarVelocity;
                stateBeforeFrozen.NewAviw = myRO.aviw;
                stateBeforeFrozen.OldAviw = myRO.aviw;
                stateBeforeFrozen.Piw = myRO.transform.position;
                stateBeforeFrozen.Rotation = myRO.transform.rotation;
                stateBeforeFrozen.WorldTime = state.TotalTimeWorld;

                wasMovementFrozen = true;
            }
            
            float deltaTime = (float)(state.TotalTimeWorld - oldWorldTime);
            if (collisions.Count == 0)
            {
                //If there was no collision, we can just reverse the change in position due to the velocity and change in rotation due to angular velocity
                transform.position += deltaTime * myRO.viw;
                float aviwMag = myRO.aviw.magnitude;
                transform.rotation = Quaternion.AngleAxis(deltaTime * aviwMag * Mathf.Rad2Deg, myRO.aviw / aviwMag) * transform.rotation;
            }
            else
            {
                //If there was a collision, we can find the last one before time travel exit, and calculate forward the difference in time
                int i = collisions.Count - 1;
                while (i >= 0 && collisions[i].WorldTime > state.TotalTimeWorld)
                {
                    if (i > 0)
                    {
                        collisions.RemoveAt(i);
                    }
                    i--;
                }
                ROHistoryCollision coll;
                if (i < 0)
                {
                    //We only have the collision immediately after time travel exit.
                    //We find how long ago it happened and calculate back the difference:
                    coll = collisions[0];
                    myRO.viw = coll.OldPeculiarVel;
                    myRO.aviw = coll.OldAviw;
                    collisions.RemoveAt(0);
                }
                else
                {
                    //We have the collision immediately before or at time travel exit.
                    //We find how long ago it happened and calculate forward the difference:
                    coll = collisions[i];
                    myRO.viw = coll.NewPeculiarVel;
                    myRO.aviw = coll.NewAviw;
                }
                float nDeltaTime = (float)(state.TotalTimeWorld - coll.WorldTime);
                transform.position = coll.Piw + nDeltaTime * myRO.viw;
                float aviwMag = myRO.aviw.magnitude;
                transform.rotation = Quaternion.AngleAxis(nDeltaTime * aviwMag * Mathf.Rad2Deg, myRO.aviw / aviwMag) * coll.Rotation;
            }

            myRO.UpdateColliderPosition();

            oldWorldTime = state.TotalTimeWorld;
        }

        public void ReverseTime()
        {
            wasMovementFrozen = false;
        }

        public void UndoTimeTravelPrediction()
        {
            if (enabled)
            {
                RelativisticObject myRO = GetComponent<RelativisticObject>();
                myRO.viw = stateBeforeFrozen.OldPeculiarVel;
                myRO.aviw = stateBeforeFrozen.OldAviw;
                transform.rotation = stateBeforeFrozen.Rotation;

                // TODO: Include proper acceleration
                bool wasKinematic = myRO.isKinematic;
                myRO.isKinematic = true;
                myRO.piw = stateBeforeFrozen.Piw;
                myRO.peculiarVelocity = stateBeforeFrozen.OldPeculiarVel;
                myRO.isKinematic = wasKinematic;

                oldWorldTime = stateBeforeFrozen.WorldTime;
            }
        }

        public class ROHistoryCollision
        {
            public double WorldTime { get; set; }
            public Vector3 Piw { get; set; }
            //It's only necessary to store either the old or new viw, depending,
            // but storing both makes it easier to restore the state.
            public Vector3 OldPeculiarVel { get; set; }
            public Vector3 NewPeculiarVel { get; set; }
            //Similarly for angular velocity:
            public Vector3 OldAviw { get; set; }
            public Vector3 NewAviw { get; set; }
            public Quaternion Rotation { get; set; }
        }

        public void ResetHistory()
        {
            collisions = new List<ROHistoryCollision>();
        }
    }
}
