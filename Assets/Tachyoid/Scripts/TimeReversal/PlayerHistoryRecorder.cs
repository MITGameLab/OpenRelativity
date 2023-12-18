using UnityEngine;
using System.Collections.Generic;
using OpenRelativity.Objects;
using Tachyoid.TimeReversal.Generic;

namespace Tachyoid {
	public class PlayerHistoryRecorder : MonoBehaviour, ITimeReversibleObject {

		public float periodSeconds = 0.0167f;
		public GameObject dupeManagerPrefab;

        public Transform playerHead;
		//public TimeReversalCtrl tRevCtrl { get; set; }
		public FTLPlayerController playerCtrl { get; set; }

        private TachyoidGameState state;
		
		private float updateTimer;
		
		public List<PlayerHistoryPoint> history;

        PlayerImageManager previewPastManager { get; set; }

        private bool willDestroyPreview;
        

        // Use this for initialization
        void Start () {
			updateTimer = 0.0f;
			history = new List<PlayerHistoryPoint>();
			//tRevCtrl = player.GetComponent<TimeReversalCtrl>();
			playerCtrl = GameObject.FindGameObjectWithTag("Player").GetComponent<FTLPlayerController>();
            previewPastManager = null;
            state = (TachyoidGameState)TachyoidGameState.Instance;

            willDestroyPreview = true;
		}
		
		// Update is called once per frame
		void Update () {
            if (state.ReversingTime)
            {
                willDestroyPreview = false;
            }
            else if (!state.isMovementFrozen) {

                updateTimer += Time.deltaTime;
                if (updateTimer >= periodSeconds)
                {
                    AddHistoryPoint();
                    updateTimer -= periodSeconds * ((int)(updateTimer / periodSeconds));
                    double time = state.TotalTimeWorld - TachyoidConstants.WaitToDestroyHistory;
                    while (history.Count > 0 && history[0].WorldTime < time)
                    {
                        history.RemoveAt(0);
                    }
                }

                if (willDestroyPreview)
                {
                    if (previewPastManager != null) Destroy(previewPastManager.gameObject);
                    previewPastManager = null;
                }

                willDestroyPreview = true;
            }
        }
		
		private void AddHistoryPoint() {
            Vector3 playerPos = state.playerTransform.position;
            history.Add(new PlayerHistoryPoint()
            {
                WorldTime = state.TotalTimeWorld,
                Position = playerPos,
                Rotation = playerHead.rotation,
                Viw = state.PlayerVelocityVector,
                Action = playerCtrl.playerAction,
                ItemHeld = playerCtrl.itemInHand,
                ItemImageIndex = playerCtrl.itemInHand == null ? -1 : playerCtrl.itemInHand.CurrentImageIndex
            });
        }

        public void ReverseTime()
        {
            if (previewPastManager == null)
            {
                InitTimeReversalPreview();
            }
            else
            {
                previewPastManager.GetComponent<ParadoxSphere>().originalRadiusTime = (float)state.WorldTimeBeforeReverse;
            }

            previewPastManager.isPreview = false;
            history = new List<PlayerHistoryPoint>();
            previewPastManager = null;
            willDestroyPreview = true;
        }

        public void InitTimeReversalPreview() {
            history.Sort((x, y) => x.WorldTime.CompareTo(y.WorldTime));
            GameObject pastManagerGO = Instantiate(dupeManagerPrefab);
            pastManagerGO.transform.localScale = transform.lossyScale;
			RelativisticObject pastManagerRO = pastManagerGO.GetComponent<RelativisticObject>();
			if (pastManagerRO != null) {
                bool wasKinematic = pastManagerRO.isKinematic;
                pastManagerRO.isKinematic = true;
                pastManagerRO.piw = state.playerTransform.position;
                pastManagerRO.viw = state.PlayerVelocityVector;
                //pastManagerRO.nonGravAccel = state.PlayerAccelerationVector;
                pastManagerRO.isKinematic = wasKinematic;

                pastManagerRO.viw = Vector3.zero;
            }
			ParadoxSphere pastManagerPS = pastManagerGO.GetComponent<ParadoxSphere>();
			if (pastManagerPS != null) {
                pastManagerPS.originalRadiusTime = (float)state.WorldTimeBeforeReverse;
                pastManagerPS.isBlockingTRev = false;
            }
			previewPastManager = pastManagerGO.GetComponent<PlayerImageManager>();
            previewPastManager.isPreview = true;
            previewPastManager.player = playerCtrl;
			previewPastManager.history = history;
			//previewPastManager.playerRB4 = playerRB4;
			//previewPastManager.tRevCtrl = ;
			previewPastManager.boostWorldTime = state.TotalTimeWorld;
            previewPastManager.playerPosOrig = state.PlayerPositionBeforeReverse;
            previewPastManager.transform.position = state.PlayerPositionBeforeReverse;
            //previewPastManager.SetParadoxSphereEnabled(false);
        }

        public void UpdateTimeTravelPrediction()
        {
            if (previewPastManager == null)
            {
                InitTimeReversalPreview();
            }
            else
            {
                previewPastManager.GetComponent<ParadoxSphere>().originalRadiusTime = (float)state.WorldTimeBeforeReverse;
            }
        }

        public void UndoTimeTravelPrediction()
        {
            UpdateTimeTravelPrediction();
        }
    }
}