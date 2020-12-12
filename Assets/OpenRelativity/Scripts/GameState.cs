using System;
using UnityEngine;

using OpenRelativity.ConformalMaps;

namespace OpenRelativity
{
    public class GameState : MonoBehaviour
    {
        #region Static Variables
        // We want a "System" (in Entity-Component-Systems) to be unique.
        private static GameState _instance;
        public static GameState Instance { get { return _instance; } }
        #endregion

        #region Member Variables

        public ConformalMap conformalMap;

        //grab the player's transform so that we can use it
        public Transform playerTransform;
        //player Velocity as a scalar magnitude
        public float playerVelocity { get; set; }
        public bool IsPlayerFalling { get; set; }
        //speed of light
        private float c = 200;
        //Speed of light that is affected by the Unity editor
        public float totalC = 200;
        //max speed the player can achieve (starting value accessible from Unity Editor)
        public float maxPlayerSpeed;
        // Reduced Planck constant divided by gravitational constant
        // (WARNING: Effects implemented based on this have not been peer reviewed,
        // but that doesn't mean they wouldn't be "cool" in a video game, at least.)
        public float hbar = 1e-12f;
        public float gConst = 1;
        public float hbarOverG
        {
            // Physically would be ~7.038e-45f m^5/s^3, in our universe
            get
            {
                return hbar / gConst;
            }
        }
        public float planckLength
        {
            get
            {
                return Mathf.Sqrt((hbar * gConst) / Mathf.Pow(SpeedOfLight, 3));
            }
        }
        public float planckTime
        {
            get
            {
                return Mathf.Sqrt(hbar * gConst / Mathf.Pow(SpeedOfLight, 5));
            }
        }
        public float planckMass
        {
            get
            {
                return Mathf.Sqrt(hbar * gConst / SpeedOfLight);
            }
        }
        public float planckAccel
        {
            get
            {
                return SpeedOfLight / planckTime;
            }
        }
        
        public float fluxPerAccel = 0;

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
        public Vector3 PlayerComovingVelocityVector { get; set; }
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
            get { return c; }
            set { c = value; SpeedOfLightSqrd = value * value; }
        }
        public float SpeedOfLightSqrd { get; private set; }

        public bool keyHit { get; set; }
        public float MaxSpeed { get; set; }

        public bool HasWorldGravity { get; set; }

        public bool isPlayerComoving = true;

        #endregion

        #region consts
        public const int splitDistance = 21000;
        #endregion

        public bool IsInitDone
        {
            get
            {
                return SqrtOneMinusVSquaredCWDividedByCSquared != 0;
            }
        }

        public virtual void Awake()
        {
            // Ensure a singleton
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                _instance = this;
            }

            // This is the "flag" that lets us know initialization is not complete.
            SqrtOneMinusVSquaredCWDividedByCSquared = 0;

            //Initialize the player's speed to zero
            playerVelocity = 0;
            
            //Set our constants
            MaxSpeed = maxPlayerSpeed;

            c = totalC;
            SpeedOfLightSqrd = c * c;
            //And ensure that the game starts
            isMovementFrozen = false;
            menuKeyDown = false;
            shaderKeyDown = false;
            keyHit = false;

            playerRotation = Vector3.zero;
            deltaRotation = Vector3.zero;

            PlayerLorentzMatrix = Matrix4x4.identity;
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
                GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>().velocity = Vector3.zero;
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
                Vector4 vpc = -PlayerVelocityVector / c;
                PlayerLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(vpc);

                //update our acceleration (which relates rapidities rather than velocities)
                //playerAccelerationVector = (playerVelocityVector.Gamma() * playerVelocityVector - oldPlayerVelocityVector.Gamma() * oldPlayerVelocityVector) / Time.deltaTime;
                //and then update the old velocity for the calculation of the acceleration on the next frame
                //oldPlayerVelocityVector = playerVelocityVector;


                //During colorshift on/off, during the last level we don't want to have the funky
                //colors changing so they can apperciate the other effects
                if (shaderOff)
                {
                    Shader.SetGlobalFloat("_colorShift", 0.0f);
                    //shaderParams.colorShift = 0.0f;
                }
                else
                {
                    Shader.SetGlobalFloat("_colorShift", 1);
                    //shaderParams.colorShift = 1.0f;
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
                //inverseAcceleratedGamma = SRelativityUtil.InverseAcceleratedGamma(playerAccelerationVector, playerVelocityVector, deltaTimePlayer);

                //Set by Unity, time since last update
                DeltaTimePlayer = Time.deltaTime;
                //Get the total time passed of the player and world for display purposes
                TotalTimePlayer += DeltaTimePlayer;
                //if (!float.IsNaN(inverseAcceleratedGamma))
                if (!float.IsNaN(SqrtOneMinusVSquaredCWDividedByCSquared))
                {
                    //Get the delta time passed for the world, changed by relativistic effects
                    DeltaTimeWorld = DeltaTimePlayer / SqrtOneMinusVSquaredCWDividedByCSquared;
                    //NOTE: Dan says, there should also be a correction for acceleration in the 00 component of the metric tensor.
                    // This correction is dependent on object position and needs to factored by the RelativisticObject itself.
                    // (Pedagogical explanation at http://aether.lbl.gov/www/classes/p139/homework/eight.pdf.
                    // See "The Metric for a Uniformly Accelerating System.")
                    TotalTimeWorld += DeltaTimeWorld;
                }

                //Set our rigidbody's velocity
                if (!float.IsNaN(DeltaTimePlayer) && !float.IsNaN(SqrtOneMinusVSquaredCWDividedByCSquared))
                {
                    
                }
                //But if either of those two constants is null due to a zero error, that means our velocity is zero anyways.
                else
                {
                    GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>().velocity = Vector3.zero;
                }
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
                if (deltaCameraAngle == 180.0f)
                {
                    deltaCameraAngle = 0;
                }
                oldCameraForward = cameraForward;
            }
        }

        void FixedUpdate()
        {
            Rigidbody playerRB = GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>();

            if (!isMovementFrozen &&
                !float.IsNaN(DeltaTimePlayer) &&
                SqrtOneMinusVSquaredCWDividedByCSquared > 0 &&
                !float.IsNaN(SqrtOneMinusVSquaredCWDividedByCSquared) && SpeedOfLight > 0)
            {
                if (conformalMap != null && isPlayerComoving)
                {
                    // Assume local player coordinates are comoving
                    Vector4 piw4 = conformalMap.ComoveOptical(FixedDeltaTimePlayer, playerTransform.position);
                    Vector3 pDiff = (Vector3)piw4 - playerTransform.position;
                    PlayerComovingVelocityVector = pDiff / FixedDeltaTimePlayer;
                    playerTransform.position = piw4;
                    PlayerVelocityVector = PlayerVelocityVector.AddVelocity(conformalMap.GetRindlerAcceleration(playerTransform.position) * FixedDeltaTimePlayer);
                }

                Vector3 velocity = -PlayerVelocityVector;
                playerRB.velocity = velocity / SqrtOneMinusVSquaredCWDividedByCSquared;
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

            Matrix4x4 matrix;
            matrix.m00 = wSqrd + xSqrd - ySqrd - zSqrd;
            matrix.m01 = 2 * x * y - 2 * w * z;
            matrix.m02 = 2 * x * z + 2 * w * y;
            matrix.m03 = 0;
            matrix.m10 = 2 * x * y + 2 * w * z;
            matrix.m11 = wSqrd - xSqrd + ySqrd - zSqrd;
            matrix.m12 = 2 * y * z + 2 * w * x;
            matrix.m13 = 0;
            matrix.m20 = 2 * x * z - 2 * w * y;
            matrix.m21 = 2 * y * z - 2 * w * x;
            matrix.m22 = wSqrd - xSqrd - ySqrd + zSqrd;
            matrix.m23 = 0;
            matrix.m30 = 0;
            matrix.m31 = 0;
            matrix.m32 = 0;
            matrix.m33 = 1;
            return matrix;
        }
        #endregion
    }
}