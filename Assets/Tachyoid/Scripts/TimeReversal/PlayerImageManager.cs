using UnityEngine;
using System.Collections.Generic;
using OpenRelativity;
using OpenRelativity.Objects;
using Tachyoid.Objects;
using Tachyoid.TimeReversal.Generic;

namespace Tachyoid {
	public class PlayerImageManager : RelativisticBehavior, ITimeReversibleObject {

        public FTLPlayerController player;

		public GameObject playerDupePrefab;

        public ParticleSystem warpExitFlash;
        private AudioSource warpExitSound;
        public ParticleSystem warpEntranceFlash;
        private AudioSource warpEntranceSound;

        //public TimeReversalCtrl tRevCtrl { get; set; }

        public List<PlayerHistoryPoint> history;
        private int lastPositionIndex = 0;

		public float boostWorldTime { get; set; }
		
		private PlayerImage image;

		private class GraspableImageOriginalPair
		{
			public Graspable graspable { get; set; }
			public Graspable image { get; set; }
            public int index { get; set; }
		}

        private GraspableImageOriginalPair itemImage;

        //private Graspable itemHeld;

        private bool didWarp;
        private bool exitingWarp;
        private bool needsPickupFrame;
        private float exitWarpTime;
        public Vector3 playerPosOrig { get; set; }
        //private float origDistFromPlayer;

        private ParadoxSphere myParadoxSphere;

        public float previewClipDistance = 3.0f;
        private bool _isPreview;
        public bool isPreview {
            get
            {
                return _isPreview;
            }
            set
            {
                if (myParadoxSphere == null)
                {
                    myParadoxSphere = GetComponent<ParadoxSphere>();
                }
                myParadoxSphere.isBlockingTRev = !value;
                _isPreview = value;
            }
        }
		
		// Use this for initialization
		void Start () {
            itemImage = new GraspableImageOriginalPair();
            //itemHeld = null;
            //flashTime = state.WorldTimeBeforeReverse;
            exitWarpTime = state.TotalTimeWorld;
            if (warpExitFlash != null)
            {
                warpExitFlash.transform.parent = null;
                warpExitSound = warpExitFlash.GetComponent<AudioSource>();
            }
            if (warpEntranceFlash != null)
            {
                warpExitFlash.transform.parent = null;
                warpEntranceSound = warpEntranceFlash.GetComponent<AudioSource>();
            }
            didWarp = false;
            exitingWarp = false;
            needsPickupFrame = false;
            myParadoxSphere = GetComponent<ParadoxSphere>();
            myParadoxSphere.isBlockingTRev = false;
            isPreview = true;

            //origDistFromPlayer = playerPosOrig.magnitude;

            lastPositionIndex = 0;
		}

        public void SetParadoxSphereEnabled(bool enabled)
        {
            if (myParadoxSphere == null) myParadoxSphere = GetComponent<ParadoxSphere>();
            myParadoxSphere.isBlockingTRev = enabled;
        }

        void FixedUpdate()
        {
            Vector3 playerPos = state.playerTransform.position;

            if (needsPickupFrame)
            {
                float time = state.TotalTimeWorld;
                //didWarp = time >= flashTime;
                exitingWarp = time < exitWarpTime;
            }

            if (!isPreview)
            {
                CreateImage();
            }

            if (history != null && history.Count > 0 && !state.isMovementFrozen)
            {
                PlayerHistoryPoint lastPoint = history[history.Count - 1];
                float lastTime = lastPoint.WorldTime - GetTimeDelay(lastPoint.Position);
                if (image != null)
                {
                    lastTime += image.myRO.localTimeOffset;
                }
                
                if (lastTime + TachyoidConstants.WaitToDestroyHistory < state.TotalTimeWorld)
                {
                    if ((warpEntranceFlash != null)) Destroy(warpEntranceFlash.gameObject);
                    if ((warpExitFlash != null)) Destroy(warpExitFlash.gameObject);
                    if ((image != null) && (image.gameObject != null)) Destroy(image.gameObject);
                    if ((itemImage != null) && (itemImage.image != null)) Destroy(itemImage.image.gameObject);
                    Destroy(gameObject);
                }
            }
        }

        private void CreateImage() {

            //When they are recorded, the player "lives" in optical space.
            PlayerHistoryPoint nextPoint, prevPoint, currentPoint;
            float currentTime, roTimeOffset;
            if (image == null)
            {
                roTimeOffset = 0.0f;
            }
            else
            {
                roTimeOffset = -image.myRO.localTimeOffset;
            }
            currentPoint = history[lastPositionIndex];
            currentTime = currentPoint.WorldTime + GetTimeDelay(currentPoint.Position) + roTimeOffset;
            while ((lastPositionIndex < (history.Count - 1)) && (currentTime < state.TotalTimeWorld))
            {
                lastPositionIndex++;
                currentPoint = history[lastPositionIndex];
                currentTime = currentPoint.WorldTime + GetTimeDelay(currentPoint.Position) + roTimeOffset;
            }
            currentTime = state.TotalTimeWorld;
            if (lastPositionIndex == history.Count - 1)
            {
                if (!didWarp)
                {
                    if (warpExitFlash != null)
                    {
                        warpExitFlash.transform.parent.parent = null;
                        RelativisticObject warpRO = warpExitFlash.GetComponent<RelativisticObject>();
                        RelativisticObject sourceRO = (image == null) ? GetComponent<RelativisticObject>() : image.myRO;
                        warpRO.piw = sourceRO.piw;
                        warpRO.viw = sourceRO.viw;
                        warpRO.nonGravAccel = sourceRO.nonGravAccel;
                        warpExitFlash.time = 0.0f;
                        warpExitFlash.Play();
                    }
                    if (warpExitSound != null)
                    {
                        warpExitSound.Play();
                    }
                }
                didWarp = true;
                if (image != null) image.SetImageEnabled(false);
                if (itemImage.image != null)
                {
                    //itemImage.image.enabled = false;
                    RelativisticObject itemRO = itemImage.image.GetComponent<RelativisticObject>();
                    if (itemRO != null) itemRO.SetDeathTime();
                }
            }
            else if (lastPositionIndex > 0)
            {
                //When they are recorded, the player "lives" in optical space.
                nextPoint = currentPoint;
                prevPoint = history[lastPositionIndex - 1];
                float prevTime = prevPoint.WorldTime + GetTimeDelay(prevPoint.Position) + roTimeOffset;
                float nextTime = nextPoint.WorldTime + GetTimeDelay(nextPoint.Position) + roTimeOffset;
                float frameFrac = (currentTime - prevTime) / (nextTime - prevTime);
                currentPoint = new PlayerHistoryPoint()
                {
                    Action = frameFrac < 0.5 ? prevPoint.Action : nextPoint.Action,
                    ItemHeld = prevPoint.ItemHeld,
                    Position = Vector3.Lerp(prevPoint.Position, nextPoint.Position, frameFrac),
                    Viw = Vector3.Lerp(prevPoint.Viw, nextPoint.Viw, frameFrac),
                    Rotation = Quaternion.Slerp(prevPoint.Rotation, nextPoint.Rotation, frameFrac),
                    WorldTime = currentTime
                };

                if (image == null)
                {
                    image = Instantiate(playerDupePrefab).GetComponent<PlayerImage>();
                }
                else
                {
                    image.SetImageEnabled(true);
                }
                image.SetAnimation(currentPoint.Action);
                image.myRO.piw = currentPoint.Position;
                image.myRO.viw = currentPoint.Viw;
                image.transform.rotation = currentPoint.Rotation;
                image.transform.forward = Vector3.ProjectOnPlane(image.transform.forward, Vector3.up).normalized;

                itemImage.graspable = currentPoint.ItemHeld;
                if (itemImage.graspable != null)
                {
                    bool wasNull = itemImage.image == null;
                    if (wasNull)
                    {
                        Collider graspableCollider = itemImage.graspable.GetComponent<Collider>();
                        bool wasPresentAndEnabled = false;
                        if (graspableCollider != null)
                        {
                            wasPresentAndEnabled = graspableCollider.enabled;
                            graspableCollider.enabled = false;
                        }
                        itemImage.image = itemImage.graspable.GetImage(currentPoint.ItemImageIndex);
                        itemImage.index = currentPoint.ItemImageIndex;
                        itemImage.image.ChangeHolder(image.head, null, image.myRO);
                        itemImage.image.MoveDirectlyToRestingPoint();
                        if (wasPresentAndEnabled)
                        {
                            graspableCollider.enabled = true;
                            itemImage.image.GetComponent<Collider>().enabled = true;
                        }
                        image.itemImageHeld = itemImage.image;
                    }
                    else
                    {
                        itemImage.image.MoveDirectlyToRestingPoint();
                    }

                    RelativisticObject itemImageRO = itemImage.image.GetComponent<RelativisticObject>();
                    if (itemImageRO != null)
                    {
                        //itemImageRO.enabled = false;
                        //itemImageRO.viw = currentPoint.Viw;
                        //itemImageRO.ForceShader(itemImageRO.viw, itemImageRO.transform.position, Vector3.zero);
                        //itemImageRO.ForceShader(currentPoint.velocity3, currentPoint.position, Vector3.zero);
                        RelativisticObjectTimeReverser itemImageROTRev = itemImage.image.GetComponent<RelativisticObjectTimeReverser>();
                        if (itemImageROTRev != null)
                        {
                            itemImageROTRev.enabled = false;
                        }
                    }
                }
                else if (itemImage.image != null)
                {
                    itemImage.image.GetComponent<RelativisticObject>().SetDeathTime();
                    itemImage.image = null;
                    image.itemImageHeld = null;
                }
            }

            if (image != null && !isPreview)
            {
                if (exitingWarp && !state.isMovementFrozen)
                {
                    exitingWarp = false;
                    if (warpEntranceFlash != null)
                    {
                        warpEntranceFlash.transform.parent.parent = null;
                        RelativisticObject warpRO = warpEntranceFlash.GetComponent<RelativisticObject>();
                        warpRO.piw = image.myRO.piw;
                        warpRO.viw = image.myRO.viw;
                        warpRO.nonGravAccel = image.myRO.nonGravAccel;
                        warpEntranceFlash.time = 0.0f;
                        warpEntranceFlash.Play();
                    }
                    if (warpEntranceSound != null)
                    {
                        warpEntranceSound.Play();
                    }
                }
            }
        }

		public void ReverseTime() {
            lastPositionIndex = 0;
            //if (image != null && image.myRO != null)
            //{
            //    image.myRO.ResetLocalTime();
            //}
            PickupOnReverse();
        }

        private void PickupOnReverse()
        {
            PlayerHistoryPoint lastPoint = history[history.Count - 1];
            float apparentTime = state.TotalTimeWorld - GetTimeDelay(lastPoint.Position);
            if (image != null)
            {
                apparentTime += image.myRO.localTimeOffset;
            }
            if (apparentTime <= lastPoint.WorldTime)
            {
                didWarp = false;
            }
            if (apparentTime <= history[0].WorldTime)
            {
                exitingWarp = true;
            }
        }

        public void UpdateTimeTravelPrediction()
        {
            if (!isPreview)
            {
                SetParadoxSphereEnabled(true);
            }
            //PickupOnReverse(state.TotalTimeWorld);
        }

        public void UndoTimeTravelPrediction()
        {
            SetParadoxSphereEnabled(false);
        }

        void OnDestroy()
        {
            if ((warpEntranceFlash != null)) Destroy(warpEntranceFlash.gameObject);
            if ((warpExitFlash != null)) Destroy(warpExitFlash.gameObject);
            if (itemImage.image != null)
            {
                Vector3 releaseViw = Vector3.zero;
                RelativisticObject imageRO = image.GetComponent<RelativisticObject>();
                if (imageRO != null)
                {
                    releaseViw = imageRO.viw;
                }
                itemImage.image.ChangeHolder(null, null, imageRO, Vector3.zero);
                RelativisticObject itemRO = itemImage.image.GetComponent<RelativisticObject>();
                if (itemRO != null)
                {
                    itemRO.DeathTime = itemImage.graspable.GetImageDeathTime(itemImage.index);
                }
            }
            if (image != null)
            {
                Destroy(image.gameObject);
            }
        }

        //public Vector3 GetWorldPos(PlayerHistoryPoint playerHistoryPoint)
        //{
        //    return ((Vector4)(playerHistoryPoint.Position)).OpticalToWorld(
        //        playerHistoryPoint.Viw,
        //        state.playerTransform.position,
        //        state.PlayerVelocityVector,
        //        state.PlayerAccelerationVector,
        //        state.PlayerAngularVelocityVector,
        //        playerHistoryPoint.Pap);
        //}

        public float GetTimeDelay(Vector3 opticalPos)
        {
            Vector3 playerPos = state.playerTransform.position;
            return (opticalPos - playerPos).magnitude / SRelativityUtil.c;
        }
    }
}