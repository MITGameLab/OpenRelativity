using UnityEngine;
using OpenRelativity;
using Tachyoid.TimeReversal.Generic;

namespace Tachyoid {
	public class ParadoxSphere : RelativisticBehavior, ITimeReversibleObject {

		//private PlayerImageManager imageManager;
        private FTLPlayerController _playerCtrl;
		private FTLPlayerController playerCtrl
        {
            get
            {
                if (_playerCtrl == null) _playerCtrl = state.GetComponent<FTLPlayerController>();
                return _playerCtrl;
            }
        }
		public Renderer exteriorRenderer;
		public Renderer interiorRenderer;
        //private RelativisticObject myRO;
        //private Collider myCollider;
        public TachyoidGameState tState
        {
            get
            {
                return (TachyoidGameState)state;
            }
        }

        public float originalRadiusTime { get; set; }

        public bool useGravity;

        private Collider _myCollider;
        public Collider myCollider {
            get {
                if (_myCollider == null) _myCollider = exteriorRenderer.GetComponent<Collider>();
                return _myCollider;
            }
        }

        private bool _isBlockingTRev;
        public bool isBlockingTRev {
            get
            {
                return _isBlockingTRev;
            }
            set
            {
                if (value)
                {
                    exteriorRenderer.enabled = true;
                    interiorRenderer.enabled = true;
                    myCollider.enabled = true;
                    if (!_isBlockingTRev)
                    {
                        this.enabled = true;
                    }
                }
                else
                {
                    exteriorRenderer.enabled = false;
                    interiorRenderer.enabled = false;
                    myCollider.enabled = false;
                    if (_isBlockingTRev)
                    {
                        this.enabled = false;
                    }
                }
                _isBlockingTRev = value;
            }
        }

        private Vector3 origPos;

		// Use this for initialization
		void Start () {
            //imageManager = GetComponent<PlayerImageManager>();
            origPos = transform.position;
			exteriorRenderer.enabled = false;
			interiorRenderer.enabled = false;
            myCollider.enabled = false;
			MeshFilter interiorFilter = interiorRenderer.gameObject.GetComponent<MeshFilter>();
			//Reverse triangles
			int[] triangles = interiorFilter.mesh.triangles;
			for (int i = 0; i < triangles.Length; i += 3) {
				int t = triangles[i];
				triangles[i] = triangles[i+2];
				triangles[i+2] = t;
			}
			interiorFilter.mesh.triangles = triangles;
            //Reverse normals
            for (int i = 0; i < interiorFilter.mesh.normals.Length; i++)
            {
                interiorFilter.mesh.normals[i] = -interiorFilter.mesh.normals[i];
            }
            //myRO = GetComponent<RelativisticObject>();
            //myCollider = GetComponent<Collider>();
            isBlockingTRev = false;
            this.enabled = false;
		}

        private double DeltaR
        {
            get
            {
                // Predicted distance to warp point, minus current distance to warp point (giving a time difference when divided by the speed of light).
                return ((tState.PlayerPositionBeforeReverse - origPos).magnitude - (state.playerTransform.position - origPos).magnitude);
            }
        }

        private double Radius
        {
            get
            {
                return (originalRadiusTime - state.TotalTimeWorld) * state.SpeedOfLight + DeltaR;
            }
        }

        private Vector3 DeltaX
        {
            get
            {
                // Change in position of ParadoxSphere, due to world frame gravitational field
                double a = tState.PlayerGravity.magnitude;
                Vector3 aUnit = tState.PlayerGravity / (float)a;
                double beta = (a * (Radius / SRelativityUtil.cSqrd));
                return aUnit * (float)((SRelativityUtil.cSqrd / a) * (Mathf.Sqrt((float)(1 + beta * beta)) - 1));
            }
        }

        public float GetRadius(double totalTimeWorld, Vector3 origPlayerPos, Vector3 estPlayerPos)
        {
            float toClamp = (float)((originalRadiusTime - totalTimeWorld) * state.SpeedOfLight + ((origPlayerPos - transform.position).magnitude - (estPlayerPos - transform.position).magnitude));
            if (toClamp < 0.0f)
            {
                toClamp = 0.0f;
            }
            return toClamp;
        }

        // Update is called once per frame
        void Update () {
            _myCollider = exteriorRenderer.GetComponent<Collider>();
            if (!state.isMovementFrozen)
            {
                isBlockingTRev = false;
            }
		}

        public void UpdateTimeTravelPrediction()
        {
            if (isBlockingTRev)
            {
                //The sphere has a set original radius based on how far the original time travel event was.
                // We are currently frozen, and we add the new time travel estimate to the base radius:

                //This version is a correct and technically functional time travel preview, but it's confusing to the player stuck in the sphere:
                float radius = (float)(Radius);

                //float radius = (float)((originalRadiusTime + state.WorldTimeBeforeReverse - state.TotalTimeWorld) * state.SpeedOfLight);

                if (radius > 0.0f)
                {
                    transform.position = origPos + DeltaX;
                    transform.localScale = 2.0f * radius * new Vector3(1.0f, 1.0f, 1.0f);
                    exteriorRenderer.enabled = true;
                    interiorRenderer.enabled = true;
                }
                else
                {
                    transform.position = origPos;
                    transform.localScale = Vector3.zero;
                    exteriorRenderer.enabled = false;
                    interiorRenderer.enabled = false;
                }
            }
        }

        public void ReverseTime()
        {
            //No action necessary
        }

        public void UndoTimeTravelPrediction()
        {
            UpdateTimeTravelPrediction();
        }
	}
}