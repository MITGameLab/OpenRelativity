using UnityEngine;

using System.Collections;
using System.Collections.Generic;

using OpenRelativity;
using Tachyoid.TimeReversal.Generic;

namespace Tachyoid {
    public class TachyoidGameState : GameState
    {
        public Vector3 PlayerSupplementalGravity = new Vector3(0, -5, 0);
        public Vector3 PlayerGravity {
            get
            {
                return Physics.gravity + PlayerSupplementalGravity;
            }
        }

        #region Time Travel Properties
        public Vector3 PlayerPositionBeforeReverse { get; set; }
        public Vector3 PlayerVelocityVectorBeforeReverse { get; set; }
        public Vector3 PlayerAccelerationBeforeReverse { get; set; }
        public float WorldTimeBeforeReverse { get; private set; }
        public bool ReversingTime { get; private set; }
        public float WorldTimePrediction {
            set
            {
                if (isMovementFrozen)
                {
                    TotalTimeWorld = value;
                }
            }
        }
        private bool justReversedTime;
        #endregion

        public override void ChangeState()
        {
            if (isMovementFrozen)
            {
                isMovementFrozen = false;

                if (!justReversedTime)
                {
                    TotalTimeWorld = WorldTimeBeforeReverse;
                    UndoTimeReversalPrediction();
                }
            }
            else
            {
                //When we pause, set our velocity to zero, show the cursor and unlock it.
                GameObject.FindGameObjectWithTag(Tags.playerRigidbody).GetComponent<Rigidbody>().velocity = Vector3.zero;
                isMovementFrozen = true;

                justReversedTime = false;
                WorldTimeBeforeReverse = TotalTimeWorld;
            }

        }

        public override void LateUpdate()
        {
            if (ReversingTime) {
                return;
            }

            base.LateUpdate();

            if (isMovementFrozen)
            {
                //If we're paused, we're potentially predicting time travel:

                UpdateOtherTimeReversibleObjects();
            } else {
                //If not frozen, keep these two values the same:
                WorldTimeBeforeReverse = TotalTimeWorld;
            }
        }

        #region Time Travel Methods

        public void ReverseTime(Vector3 playerPosChange)
        {
            PlayerVelocityVector = Vector3.zero;
            PlayerAccelerationVector = Vector3.zero;

            PlayerPositionBeforeReverse = playerTransform.position;
            playerTransform.position += playerPosChange;
            
            DeltaTimeWorld = TotalTimeWorld - WorldTimeBeforeReverse;

            List<string> tRevTags = new List<string>();
            tRevTags.Add("TRev");
            tRevTags.Add("TRev;Pickup");
            tRevTags.Add("TRev;ImageManager");
            tRevTags.Add("TRev;Button");
            /*string to_search_tag="TRev";
			for (int i = 0; i < UnityEditorInternal.InternalEditorUtility.tags.Length; i++) {
				if (UnityEditorInternal.InternalEditorUtility.tags[i].Contains(to_search_tag)) {
					tRevTags.Add(UnityEditorInternal.InternalEditorUtility.tags[i]);
				}
			}*/
            for (int i = 0; i < tRevTags.Count; i++)
            {
                GameObject[] toReverse = GameObject.FindGameObjectsWithTag(tRevTags[i]);
                for (int j = 0; j < toReverse.Length; j++)
                {
                    ITimeReversibleObject[] tRevs = toReverse[j].GetComponents<ITimeReversibleObject>();
                    for (int k = 0; k < tRevs.Length; k++)
                    {
                        tRevs[k].ReverseTime();
                    }
                }
            }

            ReversingTime = true;
            isMovementFrozen = true;
            WaitForPhysics();
            justReversedTime = true;
            isMovementFrozen = false;
            ReversingTime = false;
        }

        public void UpdateOtherTimeReversibleObjects()
        {
            List<string> tRevTags = new List<string>();
            tRevTags.Add("TRev");
            tRevTags.Add("TRev;Pickup");
            tRevTags.Add("TRev;ImageManager");
            for (int i = 0; i < tRevTags.Count; i++)
            {
                List<GameObject> toReverse = new List<GameObject>();
                toReverse.AddRange(GameObject.FindGameObjectsWithTag(tRevTags[i]));
                for (int j = 0; j < toReverse.Count; j++)
                {
                    ITimeReversibleObject[] tRevs = toReverse[j].GetComponents<ITimeReversibleObject>();
                    for (int k = 0; k < tRevs.Length; k++)
                    {
                        tRevs[k].UpdateTimeTravelPrediction();
                    }
                }
            }
        }

        public void UndoTimeReversalPrediction()
        {
            List<string> tRevTags = new List<string>();
            tRevTags.Add("TRev");
            tRevTags.Add("TRev;Pickup");
            tRevTags.Add("TRev;ImageManager");
            for (int i = 0; i < tRevTags.Count; i++)
            {
                List<GameObject> toReverse = new List<GameObject>();
                toReverse.AddRange(GameObject.FindGameObjectsWithTag(tRevTags[i]));
                for (int j = 0; j < toReverse.Count; j++)
                {
                    ITimeReversibleObject[] tRevs = toReverse[j].GetComponents<ITimeReversibleObject>();
                    for (int k = 0; k < tRevs.Length; k++)
                    {
                        tRevs[k].UndoTimeTravelPrediction();
                    }
                }
            }
        }

        private IEnumerator WaitForPhysics()
        {
            yield return new WaitForFixedUpdate();
        }

        #endregion
    }
}