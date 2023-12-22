using UnityEngine;

namespace Tachyoid.TimeReversal.Generic {
	public abstract class TimeReversalCtrl : MonoBehaviour {
        public abstract void ReverseTime();
        public abstract double GetUnreversedTime();
        public abstract double GetTime();
        public abstract Vector3 GetUnreversedCameraGroundPosition();
        public abstract Vector3 GetUnreversedCameraPosition3();
        public abstract Vector3 GetCameraPosition3();
        public abstract Vector3 GetCameraGroundPosition();
        public abstract Vector3 GetVelocity();
        public abstract Vector3 GetUnreversedAcceleration();
        public abstract Vector3 GetPredictedPosition3Difference();
        public abstract float GetPredictedTimeDifference();
        public abstract bool GetIsAging();
        public abstract bool GetDidReverseTime();
    }
}
