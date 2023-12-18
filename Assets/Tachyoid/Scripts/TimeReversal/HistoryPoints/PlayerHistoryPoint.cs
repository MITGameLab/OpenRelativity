using UnityEngine;
using System.Collections;
using Tachyoid.TimeReversal.HistoryPoints;

namespace Tachyoid {
	public class PlayerHistoryPoint : HistoryPoint {
		public Vector3 Position {get; set;}
		public Quaternion Rotation { get; set; }
		public Vector3 Viw {get; set;}
		public Action Action { get; set; }
		public Objects.Graspable ItemHeld { get; set; }
        public int ItemImageIndex { get; set; }
	}

	public enum Action {
		none, isRunning, isJumping, exitedBoost, didBoost
	}
}