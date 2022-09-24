using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.SceneManagement;

using OpenRelativity.ConformalMaps;

namespace OpenRelativity
{
    [ExecuteInEditMode]
    public class GameState : MonoBehaviour
    {
        #region Static Variables
        // We want a "System" (in Entity-Component-Systems) to be unique.
        private static GameState _instance;
        public static GameState Instance { get { return _instance ? _instance : FindObjectOfType<GameState>(); } }
        #endregion

        #region Member Variables

        public ConformalMap conformalMap;

        //grab the player's transform so that we can use it
        public Transform playerTransform;
        //player Velocity as a scalar magnitude
        public float playerVelocity { get; set; }
        public bool IsPlayerFalling { get; set; }
        //max speed the player can achieve (starting value accessible from Unity Editor)
        public float maxPlayerSpeed;
        //speed of light
        private double c = 200;
        //Speed of light that is affected by the Unity editor
        public double totalC = 200;
        // Reduced Planck constant divided by gravitational constant
        // (WARNING: Effects implemented based on this have not been peer reviewed,
        // but that doesn't mean they wouldn't be "cool" in a video game, at least.)
        public double hbar = 1e-12f;
        public double gConst = 1;
        public double boltzmannConstant = 1;
        public double vacuumPermeability = 1;
        public double vacuumPermittivity
        {
            get
            {
                return 1 / (vacuumPermeability * SpeedOfLightSqrd);
            }
        }
        public double hbarOverG
        {
            // Physically would be ~7.038e-45f m^5/s^3, in our universe
            get
            {
                return hbar / gConst;
            }
        }
        public double planckLength
        {
            get
            {
                return Math.Sqrt(hbar * gConst / Math.Pow(SpeedOfLight, 3));
            }
        }
        public double planckArea
        {
            get
            {
                return hbar * gConst / Math.Pow(SpeedOfLight, 3);
            }
        }
        public double planckTime
        {
            get
            {
                return Math.Sqrt(hbar * gConst / Math.Pow(SpeedOfLight, 5));
            }
        }
        public double planckMass
        {
            get
            {
                return Math.Sqrt(hbar * SpeedOfLight / gConst);
            }
        }
        public double planckEnergy
        {
            get
            {
                return Math.Sqrt(hbar * Math.Pow(SpeedOfLight, 5) / gConst);
            }
        }
        public double planckPower
        {
            get
            {
                return Math.Pow(SpeedOfLight, 5) / gConst;
            }
        }
        public double planckTemperature
        {
            get
            {
                return Math.Sqrt(hbar * Math.Pow(SpeedOfLight, 5) / (gConst * boltzmannConstant * boltzmannConstant));
            }
        }
        public double planckCharge
        {
            get
            {
                //The energy required to accumulate one Planck charge on a sphere one Planck length in diameter will make the sphere one Planck mass heavier
                return Math.Sqrt(4 * Math.PI * vacuumPermittivity * hbar * SpeedOfLight);
            }
        }
        public double planckAccel
        {
            get
            {
                return Math.Sqrt(Math.Pow(SpeedOfLight, 7) / (hbar * gConst));
            }
        }
        public double planckMomentum
        {
            get
            {
                return Math.Sqrt(hbar * Math.Pow(SpeedOfLight, 3) / gConst);
            }
        }
        public double planckAngularMomentum
        {
            get
            {
                return hbar;
            }
        }

        // In Planck units
        public float gravityBackgroundPlanckTemperature = 2.53466e-31f;

        //Use this to determine the state of the color shader. If it's True, all you'll see is the lorenz transform.
        private bool shaderOff = false;

        //Did we hit the menu key?
        public bool menuKeyDown { get; set; }
        //Did we hit the shader key?
        public bool shaderKeyDown { get; set; }

        //This is the equivalent of the above value for an accelerated player frame
        //private float inverseAcceleratedGamma;

        //Player rotation and change in rotation since last frame
        public Vector3 playerRotation { get; set; }
        public Vector3 deltaRotation { get; set; }

        private Vector3 oldCameraForward { get; set; }
        public Vector3 cameraForward { get; set; }
        public float deltaCameraAngle { get; set; }

        #endregion

        #region Properties

        //If we've paused the game
        public bool isMovementFrozen { get; set; }

        public Matrix4x4 WorldRotation { get; private set; }
        public Vector3 PlayerVelocityVector { get; set; }
        public Vector3 PlayerAccelerationVector { get; set; }
        public Vector3 PlayerAngularVelocityVector { get { if (DeltaTimePlayer == 0) { return Vector3.zero; } else { return (deltaCameraAngle * Mathf.Deg2Rad / DeltaTimePlayer) * playerTransform.up; } } }
        public Matrix4x4 PlayerLorentzMatrix { get; private set; }

        public float PlayerVelocity { get { return playerVelocity; } }
        public float SqrtOneMinusVSquaredCWDividedByCSquared { get; private set; }
        //public float InverseAcceleratedGamma { get { return inverseAcceleratedGamma; } }
        public float DeltaTimeWorld { get; protected set; }
        public float FixedDeltaTimeWorld {
            get {
                return Time.fixedDeltaTime / SqrtOneMinusVSquaredCWDividedByCSquared;
            }
        }
        //public float FixedDeltaTimeWorld { get { return Time.fixedDeltaTime / inverseAcceleratedGamma; } }
        public float DeltaTimePlayer { get; private set; }
        public float FixedDeltaTimePlayer { get { return Time.fixedDeltaTime; } }
        public float TotalTimePlayer { get; set; }
        public float TotalTimeWorld;
        public float SpeedOfLight {
            get { return (float)c; }
            set { c = value; SpeedOfLightSqrd = value * value; }
        }
        public float SpeedOfLightSqrd { get; private set; }

        public bool keyHit { get; set; }
        public float MaxSpeed { get; set; }

        public bool HasWorldGravity { get; set; }

        // If using comoveViaAcceleration in the player controller, turn off isPlayerComoving here in GameState.
        public bool isPlayerComoving = true;

        private bool _isInitDone = false;
        public bool isInitDone
        {
            get
            {
                return _isInitDone;
            }
        }

        #endregion

        #region consts
        public const int splitDistance = 21000;
        #endregion

        public void OnEnable()
        {
            // Ensure a singleton
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            else
            {
                _instance = this;
            }

            if (!conformalMap)
            {
                conformalMap = gameObject.AddComponent<Minkowski>();
            }

            SqrtOneMinusVSquaredCWDividedByCSquared = 1;

            //Initialize the player's speed to zero
            playerVelocity = 0;
            
            //Set our constants
            MaxSpeed = maxPlayerSpeed;

            c = totalC;
            SpeedOfLightSqrd = (float)(c * c);
            //And ensure that the game starts
            isMovementFrozen = false;
            menuKeyDown = false;
            shaderKeyDown = false;
            keyHit = false;

            playerRotation = Vector3.zero;
            deltaRotation = Vector3.zero;

            PlayerAccelerationVector = conformalMap.GetRindlerAcceleration(playerTransform.position);
            PlayerLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(Vector3.zero);

            if (shaderOff)
            {
                Shader.SetGlobalFloat("_colorShift", 0);
                //shaderParams.colorShift = 0;
            }
            else
            {
                Shader.SetGlobalFloat("_colorShift", 1);
                //shaderParams.colorShift = 1;
            }

            //Send velocities and acceleration to shader
            Shader.SetGlobalVector("_playerOffset", new Vector4(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z, 0));
            Shader.SetGlobalVector("_vpc", Vector3.zero);
            Shader.SetGlobalVector("_pap", PlayerAccelerationVector);
            Shader.SetGlobalVector("_avp", PlayerAngularVelocityVector);
            Shader.SetGlobalMatrix("_vpcLorentzMatrix", PlayerLorentzMatrix);
            Shader.SetGlobalMatrix("_invVpcLorentzMatrix", PlayerLorentzMatrix.inverse);

            // See https://docs.unity3d.com/Manual/ProgressiveLightmapper-CustomFallOff.html
            Lightmapping.RequestLightsDelegate testDel = (Light[] requests, Unity.Collections.NativeArray<LightDataGI> lightsOutput) =>
            {
                DirectionalLight dLight = new DirectionalLight();
                PointLight point = new PointLight();
                SpotLight spot = new SpotLight();
                RectangleLight rect = new RectangleLight();
                DiscLight disc = new DiscLight();
                Cookie cookie = new Cookie();
                LightDataGI ld = new LightDataGI();

                for (int i = 0; i < requests.Length; i++)
                {
                    Light l = requests[i];
                    switch (l.type)
                    {
                        case UnityEngine.LightType.Directional: LightmapperUtils.Extract(l, ref dLight); LightmapperUtils.Extract(l, out cookie); ld.Init(ref dLight, ref cookie); break;
                        case UnityEngine.LightType.Point: LightmapperUtils.Extract(l, ref point); LightmapperUtils.Extract(l, out cookie); ld.Init(ref point, ref cookie); break;
                        case UnityEngine.LightType.Spot: LightmapperUtils.Extract(l, ref spot); LightmapperUtils.Extract(l, out cookie); ld.Init(ref spot, ref cookie); break;
                        case UnityEngine.LightType.Area: LightmapperUtils.Extract(l, ref rect); LightmapperUtils.Extract(l, out cookie); ld.Init(ref rect, ref cookie); break;
                        case UnityEngine.LightType.Disc: LightmapperUtils.Extract(l, ref disc); LightmapperUtils.Extract(l, out cookie); ld.Init(ref disc, ref cookie); break;
                        default: ld.InitNoBake(l.GetInstanceID()); break;
                    }
                    ld.cookieID = l.cookie?.GetInstanceID() ?? 0;
                    ld.falloff = FalloffType.InverseSquared;
                    lightsOutput[i] = ld;
                }
            };
            Lightmapping.SetDelegate(testDel);
        }
        void OnDisable()
        {
            Lightmapping.ResetDelegate();
        }

        //Call this function to pause and unpause the game
        public virtual void ChangeState()
        {
            if (isMovementFrozen)
            {
                //When we unpause, lock the cursor and hide it so that it doesn't get in the way
                isMovementFrozen = false;
                //Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                //When we pause, set our velocity to zero, show the cursor and unlock it.
                GameObject.FindGameObjectWithTag(Tags.playerRigidbody).GetComponent<Rigidbody>().velocity = Vector3.zero;
                isMovementFrozen = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

        }

        //We set this in late update because of timing issues with collisions
        public virtual void LateUpdate()
        {
            //Set the pause code in here so that our other objects can access it.
            if (Input.GetAxis("Menu Key") > 0 && !menuKeyDown)
            {
                menuKeyDown = true;
                ChangeState();
            }
            //set up our buttonUp function
            else if (!(Input.GetAxis("Menu Key") > 0))
            {
                menuKeyDown = false;
            }
            //Set our button code for the shader on/off button
            if (Input.GetAxis("Shader") > 0 && !shaderKeyDown)
            {
                if (shaderOff)
                    shaderOff = false;
                else
                    shaderOff = true;

                shaderKeyDown = true;
            }
            //set up our buttonUp function
            else if (!(Input.GetAxis("Shader") > 0))
            {
                shaderKeyDown = false;
            }

            //If we're not paused, update everything
            if (!isMovementFrozen)
            {
                //Put our player position into the shader so that it can read it.
                Shader.SetGlobalVector("_playerOffset", new Vector4(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z, 0));

                //if we reached max speed, forward or backwards, keep at max speed

                if (PlayerVelocityVector.magnitude >= maxPlayerSpeed - .01f)
                {
                    PlayerVelocityVector = PlayerVelocityVector.normalized * (maxPlayerSpeed - .01f);
                }

                //update our player velocity
                playerVelocity = PlayerVelocityVector.magnitude;
                Vector4 vpc = -PlayerVelocityVector / (float)c;
                PlayerLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(vpc);

                //update our acceleration (which relates rapidities rather than velocities)
                //playerAccelerationVector = (playerVelocityVector.Gamma() * playerVelocityVector - oldPlayerVelocityVector.Gamma() * oldPlayerVelocityVector) / Time.deltaTime;
                //and then update the old velocity for the calculation of the acceleration on the next frame
                //oldPlayerVelocityVector = playerVelocityVector;


                //During colorshift on/off, during the last level we don't want to have the funky
                //colors changing so they can apperciate the other effects
                if (shaderOff)
                {
                    Shader.SetGlobalFloat("_colorShift", 0);
                    //shaderParams.colorShift = 0;
                }
                else
                {
                    Shader.SetGlobalFloat("_colorShift", 1);
                    //shaderParams.colorShift = 1;
                }

                //Send velocities and acceleration to shader
                Shader.SetGlobalVector("_vpc", vpc);
                Shader.SetGlobalVector("_pap", PlayerAccelerationVector);
                Shader.SetGlobalVector("_avp", PlayerAngularVelocityVector);
                Shader.SetGlobalMatrix("_vpcLorentzMatrix", PlayerLorentzMatrix);
                Shader.SetGlobalMatrix("_invVpcLorentzMatrix", PlayerLorentzMatrix.inverse);

                /******************************
                * PART TWO OF ALGORITHM
                * THE NEXT 4 LINES OF CODE FIND
                * THE TIME PASSED IN WORLD FRAME
                * ****************************/
                //find this constant
                SqrtOneMinusVSquaredCWDividedByCSquared = Mathf.Sqrt(1 - (playerVelocity * playerVelocity) / SpeedOfLightSqrd);

                //Set by Unity, time since last update
                DeltaTimePlayer = Time.deltaTime;
                //Get the total time passed of the player and world for display purposes
                TotalTimePlayer += DeltaTimePlayer;
                //Get the delta time passed for the world, changed by relativistic effects
                DeltaTimeWorld = DeltaTimePlayer / SqrtOneMinusVSquaredCWDividedByCSquared;
                //NOTE: Dan says, there should also be a correction for acceleration in the 00 component of the metric tensor.
                // This correction is dependent on object position and needs to factored by the RelativisticObject itself.
                // (Pedagogical explanation at http://aether.lbl.gov/www/classes/p139/homework/eight.pdf.
                // See "The Metric for a Uniformly Accelerating System.")
                TotalTimeWorld += DeltaTimeWorld;

                /*****************************
                 * PART 3 OF ALGORITHM
                 * FIND THE ROTATION MATRIX
                 * AND CHANGE THE PLAYERS VELOCITY
                 * BY THIS ROTATION MATRIX
                 * ***************************/


                //Find the turn angle
                //Steering constant angular velocity in the player frame
                //Rotate around the y-axis

                playerTransform.rotation = Quaternion.AngleAxis(playerRotation.y, Vector3.up) * Quaternion.AngleAxis(playerRotation.x, Vector3.right);
                // World rotation is opposite of player world rotation
                WorldRotation = CreateFromQuaternion(Quaternion.Inverse(playerTransform.rotation));

                //Add up our rotation so that we know where the character (NOT CAMERA) should be facing 
                playerRotation += deltaRotation;

                cameraForward = playerTransform.forward;
                deltaCameraAngle = Vector3.SignedAngle(oldCameraForward, cameraForward, playerTransform.up);
                if (deltaCameraAngle == 180)
                {
                    deltaCameraAngle = 0;
                }
                oldCameraForward = cameraForward;
            }

            _isInitDone = true;
        }

        void FixedUpdate()
        {
            Rigidbody playerRB = GameObject.FindGameObjectWithTag(Tags.playerRigidbody).GetComponent<Rigidbody>();

            if (!isMovementFrozen && (SpeedOfLight > 0))
            {
                if (isPlayerComoving)
                {
                    // Assume local player coordinates are comoving
                    Comovement cm = conformalMap.ComoveOptical(FixedDeltaTimePlayer, playerTransform.position, Quaternion.identity);
                    float test = cm.piw.sqrMagnitude;
                    if (!float.IsNaN(test) && !float.IsInfinity(test))
                    {
                        playerTransform.rotation = cm.riw * playerTransform.rotation;
                        playerTransform.position = cm.piw;
                    }
                }

                Vector3 pVel = -PlayerVelocityVector;
                playerRB.velocity = pVel / SqrtOneMinusVSquaredCWDividedByCSquared;
                pVel = playerRB.velocity;
                if (!IsPlayerFalling && (-pVel .y <= Physics.bounceThreshold)) {
                    Vector3 pVelPerp = new Vector3(pVel.x, 0, pVel.z);
                    playerRB.velocity = pVel.AddVelocity(new Vector3(0, -pVel.y * pVelPerp.Gamma(), 0));
                }
            } else
            {
                playerRB.velocity = Vector3.zero;
            }
        }
        #region Matrix/Quat math
        //They are functions that XNA had but Unity doesn't, so I had to make them myself

        //This function takes in a quaternion and creates a rotation matrix from it
        public Matrix4x4 CreateFromQuaternion(Quaternion q)
        {
            float w = q.w;
            float x = q.x;
            float y = q.y;
            float z = q.z;

            float wSqrd = w * w;
            float xSqrd = x * x;
            float ySqrd = y * y;
            float zSqrd = z * z;

            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetColumn(0, new Vector4(wSqrd + xSqrd - ySqrd - zSqrd, 2 * x * y + 2 * w * z, 2 * x * z - 2 * w * y, 0));
            matrix.SetColumn(1, new Vector4(2 * x * y - 2 * w * z, wSqrd - xSqrd + ySqrd - zSqrd, 2 * y * z - 2 * w * x, 0));
            matrix.SetColumn(2, new Vector4(2 * x * z + 2 * w * y, 2 * y * z + 2 * w * x, wSqrd - xSqrd - ySqrd + zSqrd, 0));
            matrix.SetColumn(3, new Vector4(0, 0, 0, 1));

            return matrix;
        }
        #endregion
    }
}