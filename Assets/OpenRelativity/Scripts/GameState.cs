using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.Content;
//using Microsoft.Xna.Framework.GamerServices;
//using Microsoft.Xna.Framework.Graphics;
//using Microsoft.Xna.Framework.Input;
//using Microsoft.Xna.Framework.Net;
//using Microsoft.Xna.Framework.Storage;

namespace OpenRelativity
{
    public class GameState : MonoBehaviour
    {
        #region Member Variables

        private System.IO.TextWriter stateStream;

        //Player orientation
        private Quaternion orientation = Quaternion.identity;
        //world rotation so that we can transform between the two
        private Matrix4x4 worldRotation;
        //Player's velocity in vector format
        private Vector3 playerVelocityVector;
        //Player's acceleration in vector format
        private Vector3 playerAccelerationVector;
        //Lorentz transformation matrix between world and player rest frame
        private Matrix4x4 playerLorentzMatrix;

        //grab the player's transform so that we can use it
        public Transform playerTransform;
        //If we've paused the game
        private bool movementFrozen = false;
        //player Velocity as a scalar magnitude
        public double playerVelocity { get; set; }
        //time passed since last frame in the world frame
        private double deltaTimeWorld;
        //time passed since last frame in the player frame
        private double deltaTimePlayer;
        //total time passed in the player frame
        private double totalTimePlayer;
        //total time passed in the world frame
        private double totalTimeWorld;
        //speed of light
        private double c = 200;
        //Speed of light that is affected by the Unity editor
        public double totalC = 200;
        //max speed the player can achieve (starting value accessible from Unity Editor)
        public double maxPlayerSpeed;
        //max speed, for game use, not accessible from Unity Editor
        private double maxSpeed;
        //speed of light squared, kept for easy access in many calculations
        private double cSqrd;

        //Use this to determine the state of the color shader. If it's True, all you'll see is the lorenz transform.
        private bool shaderOff = false;

        //Did we hit the menu key?
        public bool menuKeyDown { get; set; }
        //Did we hit the shader key?
        public bool shaderKeyDown { get; set; }


        //This is a value that gets used in many calculations, so we calculate it each frame
        private double sqrtOneMinusVSquaredCWDividedByCSquared;
        //This is the equivalent of the above value for an accelerated player frame
        //private double inverseAcceleratedGamma;

        //Player rotation and change in rotation since last frame
        public Vector3 playerRotation { get; set; }
        public Vector3 deltaRotation { get; set; }
        public double pctOfSpdUsing { get; set; } // Percent of velocity you are using

        private Vector3 oldCameraForward { get; set; }
        public Vector3 cameraForward { get; set; }
        public float deltaCameraAngle { get; set; }



        #endregion

        #region Properties

        public bool MovementFrozen { get { return movementFrozen; } set { movementFrozen = value; } }

        public Matrix4x4 WorldRotation { get { return worldRotation; } }
        public Quaternion Orientation { get { return orientation; } }
        public Vector3 PlayerVelocityVector { get { return playerVelocityVector; } set { playerVelocityVector = value; } }
        public Vector3 PlayerAccelerationVector { get { return playerAccelerationVector; } set { playerAccelerationVector = value; } }
        public Vector3 PlayerAngularVelocityVector { get { if (deltaTimePlayer == 0) { return Vector3.zero; } else { return (float)(deltaCameraAngle * Mathf.Deg2Rad / deltaTimePlayer) * playerTransform.up; } } }
        public Matrix4x4 PlayerLorentzMatrix { get { return playerLorentzMatrix; } }

        public double PctOfSpdUsing { get { return pctOfSpdUsing; } set { pctOfSpdUsing = value; } }
        public double PlayerVelocity { get { return playerVelocity; } }
        public double SqrtOneMinusVSquaredCWDividedByCSquared { get { return sqrtOneMinusVSquaredCWDividedByCSquared; } }
        //public double InverseAcceleratedGamma { get { return inverseAcceleratedGamma; } }
        public double DeltaTimeWorld { get { return deltaTimeWorld; } }
        public double FixedDeltaTimeWorld { get { return Time.fixedDeltaTime / sqrtOneMinusVSquaredCWDividedByCSquared; } }
        //public double FixedDeltaTimeWorld { get { return Time.fixedDeltaTime / inverseAcceleratedGamma; } }
        public double DeltaTimePlayer { get { return deltaTimePlayer; } }
        public double FixedDeltaTimePlayer { get { return Time.fixedDeltaTime; } }
        public double TotalTimePlayer { get { return totalTimePlayer; } }
        public double TotalTimeWorld { get { return totalTimeWorld; } }
        public double SpeedOfLight { get { return c; } set { c = value; cSqrd = value * value; } }
        public double SpeedOfLightSqrd { get { return cSqrd; } }

        public bool keyHit { get; set; }
        public double MaxSpeed { get { return maxSpeed; } set { maxSpeed = value; } }

        public ConformalMaps.ConformalMap conformalMap;
        public bool isMinkowski { get; set; }
        public bool HasWorldGravity { get; set; }

        #endregion

        #region consts
        private const float ORB_SPEED_INC = 0.05f;
        private const float ORB_DECEL_RATE = 0.6f;
        private const float ORB_SPEED_DUR = 2f;
        private const float MAX_SPEED = 20.00f;
        public const float NORM_PERCENT_SPEED = .99f;
        public const int splitDistance = 21000;
        #endregion


        public void Awake()
        {
            //If the conformalMap parameter is null, default to Minkowski
            if (conformalMap == null)
            {
                conformalMap = gameObject.AddComponent<ConformalMaps.Minkowski>();
                isMinkowski = true;
            }
            else
            {
                isMinkowski = false;
            }

            //Initialize the player's speed to zero
            playerVelocityVector = Vector3.zero;
            playerVelocity = 0;
            //If the player starts out resting on a surface, then their initial acceleration is due to gravity.
            // If the player is in free fall (i.e. "not affected by gravity" in the sense of Einstein equivalance principle,)
            // then their acceleration is zero.
            //oldPlayerVelocityVector = Vector3.zero;
            playerAccelerationVector = Vector3.zero;
            
            //Set our constants
            MaxSpeed = maxPlayerSpeed;
            pctOfSpdUsing = NORM_PERCENT_SPEED;

            c = totalC;
            cSqrd = c * c;
            //And ensure that the game starts/
            movementFrozen = false;
            menuKeyDown = false;
            shaderKeyDown = false;
            keyHit = false;

            playerRotation = Vector3.zero;
            deltaRotation = Vector3.zero;

            playerLorentzMatrix = Matrix4x4.identity;
        }
        public void reset()
        {
            //Reset everything not level-based
            playerRotation = Vector3.zero;
            pctOfSpdUsing = 0;
        }
        //Call this function to pause and unpause the game
        public void ChangeState()
        {
            if (movementFrozen)
            {
                //When we unpause, lock the cursor and hide it so that it doesn't get in the way
                movementFrozen = false;
                //Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                //When we pause, set our velocity to zero, show the cursor and unlock it.
                GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>().velocity = Vector3.zero;
                movementFrozen = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

        }

        //We set this in late update because of timing issues with collisions
        public void LateUpdate()
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
            if (!movementFrozen)
            {
                //Put our player position into the shader so that it can read it.
                Shader.SetGlobalVector("_playerOffset", new Vector4(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z, 0));

                //if we reached max speed, forward or backwards, keep at max speed

                if (playerVelocityVector.magnitude >= (float)MaxSpeed - .01f)
                {
                    playerVelocityVector = playerVelocityVector.normalized * ((float)MaxSpeed - .01f);
                }

                //update our player velocity
                playerVelocity = playerVelocityVector.magnitude;

                //update our acceleration (which relates rapidities rather than velocities)
                //playerAccelerationVector = (playerVelocityVector.Gamma() * playerVelocityVector - oldPlayerVelocityVector.Gamma() * oldPlayerVelocityVector) / Time.deltaTime;
                //and then update the old velocity for the calculation of the acceleration on the next frame
                //oldPlayerVelocityVector = playerVelocityVector;


                //During colorshift on/off, during the last level we don't want to have the funky
                //colors changing so they can apperciate the other effects
                if (shaderOff)
                {
                    Shader.SetGlobalFloat("_colorShift", (float)0.0);
                    //shaderParams.colorShift = 0.0f;
                }
                else
                {
                    Shader.SetGlobalFloat("_colorShift", (float)1);
                    //shaderParams.colorShift = 1.0f;
                }

                //Send velocities and acceleration to shader
                Vector4 vpc = new Vector4(-playerVelocityVector.x, -playerVelocityVector.y, -playerVelocityVector.z, 0) / (float)c;

                //Lorentz transforms are the same for all points in an object,
                // so it saves redundant GPU time to calculate them beforehand.
                float speed = vpc.magnitude;
                float beta = speed;
                float gamma = 1.0f / Mathf.Sqrt(1 - beta * beta);
                playerLorentzMatrix = Matrix4x4.identity;
                //Round very small velocities to zero, to avoid annoying visual jitter.
                if (beta > SRelativityUtil.divByZeroCutoff)
                {
                    Vector4 vpcTransUnit = -vpc / beta;
                    vpcTransUnit.w = 1;
                    Vector4 spatialComp = (gamma - 1) * vpcTransUnit;
                    spatialComp.w = -gamma * beta;
                    Vector4 tComp = -gamma * (new Vector4(beta, beta, beta, -1));
                    tComp.Scale(vpcTransUnit);
                    playerLorentzMatrix.SetColumn(3, tComp);
                    playerLorentzMatrix.SetColumn(0, vpcTransUnit.x * spatialComp);
                    playerLorentzMatrix.SetColumn(1, vpcTransUnit.y * spatialComp);
                    playerLorentzMatrix.SetColumn(2, vpcTransUnit.z * spatialComp);
                    playerLorentzMatrix.m00 += 1;
                    playerLorentzMatrix.m11 += 1;
                    playerLorentzMatrix.m22 += 1;
                    Shader.SetGlobalVector("_vpc", vpc);

                }
                else
                {
                    Shader.SetGlobalVector("_vpc", Vector4.zero);
                }

                Shader.SetGlobalVector("_pap", playerAccelerationVector);
                Shader.SetGlobalVector("_avp", PlayerAngularVelocityVector);
                Shader.SetGlobalMatrix("_vpcLorentzMatrix", playerLorentzMatrix);

                /******************************
                * PART TWO OF ALGORITHM
                * THE NEXT 4 LINES OF CODE FIND
                * THE TIME PASSED IN WORLD FRAME
                * ****************************/
                //find this constant
                sqrtOneMinusVSquaredCWDividedByCSquared = (double)Math.Sqrt(1 - (playerVelocity * playerVelocity) / cSqrd);
                //inverseAcceleratedGamma = SRelativityUtil.InverseAcceleratedGamma(playerAccelerationVector, playerVelocityVector, deltaTimePlayer);

                //Set by Unity, time since last update
                deltaTimePlayer = (double)Time.deltaTime;
                //Get the total time passed of the player and world for display purposes
                totalTimePlayer += deltaTimePlayer;
                //if (!double.IsNaN(inverseAcceleratedGamma))
                if (!double.IsNaN(sqrtOneMinusVSquaredCWDividedByCSquared))
                {
                    //Get the delta time passed for the world, changed by relativistic effects
                    deltaTimeWorld = deltaTimePlayer / sqrtOneMinusVSquaredCWDividedByCSquared;
                    //NOTE: Dan says, there should also be a correction for acceleration in the 00 component of the metric tensor.
                    // This correction is dependent on object position and needs to factored by the RelativisticObject itself.
                    // (Pedagogical explanation at http://aether.lbl.gov/www/classes/p139/homework/eight.pdf.
                    // See "The Metric for a Uniformly Accelerating System.")
                    totalTimeWorld += deltaTimeWorld;
                }

                //Set our rigidbody's velocity
                if (!double.IsNaN(deltaTimePlayer) && !double.IsNaN(sqrtOneMinusVSquaredCWDividedByCSquared))
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

                orientation = Quaternion.AngleAxis(playerRotation.y, Vector3.up) * Quaternion.AngleAxis(playerRotation.x, Vector3.right);
                Quaternion WorldOrientation = Quaternion.Inverse(orientation);
                Normalize(orientation);
                worldRotation = CreateFromQuaternion(WorldOrientation);

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

        private void FixedUpdate()
        {
            if (!MovementFrozen && !double.IsNaN(deltaTimePlayer) && sqrtOneMinusVSquaredCWDividedByCSquared > 0 && !double.IsNaN(sqrtOneMinusVSquaredCWDividedByCSquared) && SpeedOfLight > 0)
            {
                Rigidbody playerRB = GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>();
                Vector3 velocity = -playerVelocityVector;
                //Set our rigidbody's geodesic acceleration, if necessary
                playerRB.velocity = velocity / (float)sqrtOneMinusVSquaredCWDividedByCSquared;
                if (!isMinkowski)
                {
                    Vector3 playerPos = playerTransform.position;
                    Vector3 accel = conformalMap.GetWorldAcceleration(playerPos, playerPos);
                    float test = accel.sqrMagnitude;
                    if (!double.IsNaN(test) && !double.IsInfinity(test))
                    {
                        playerRB.AddForce(accel, ForceMode.Acceleration);
                    }
                }
            }
        }
        #region Matrix/Quat math
        //They are functions that XNA had but Unity doesn't, so I had to make them myself

        //This function takes in a quaternion and creates a rotation matrix from it
        public Matrix4x4 CreateFromQuaternion(Quaternion q)
        {
            double w = q.w;
            double x = q.x;
            double y = q.y;
            double z = q.z;

            double wSqrd = w * w;
            double xSqrd = x * x;
            double ySqrd = y * y;
            double zSqrd = z * z;

            Matrix4x4 matrix;
            matrix.m00 = (float)(wSqrd + xSqrd - ySqrd - zSqrd);
            matrix.m01 = (float)(2 * x * y - 2 * w * z);
            matrix.m02 = (float)(2 * x * z + 2 * w * y);
            matrix.m03 = (float)0;
            matrix.m10 = (float)(2 * x * y + 2 * w * z);
            matrix.m11 = (float)(wSqrd - xSqrd + ySqrd - zSqrd);
            matrix.m12 = (float)(2 * y * z + 2 * w * x);
            matrix.m13 = (float)0;
            matrix.m20 = (float)(2 * x * z - 2 * w * y);
            matrix.m21 = (float)(2 * y * z - 2 * w * x);
            matrix.m22 = (float)(wSqrd - xSqrd - ySqrd + zSqrd);
            matrix.m23 = 0;
            matrix.m30 = 0;
            matrix.m31 = 0;
            matrix.m32 = 0;
            matrix.m33 = 1;
            return matrix;
        }

        //Normalize the given quaternion so that its magnitude is one.
        public Quaternion Normalize(Quaternion quat)
        {
            Vector4 qVec = new Vector4(quat.x, quat.y, quat.z, quat.w);
            Quaternion q;
            float m = qVec.sqrMagnitude;
            if (m != 1)
            {
                m = Mathf.Sqrt(m);
                qVec /= m;
                q = new Quaternion(qVec.x, qVec.y, qVec.z, qVec.w);
            }
            else
            {
                q = quat;
            }
            return q;
        }
        //Transform the matrix by a vector3
        public Vector3 TransformNormal(Vector3 normal, Matrix4x4 matrix)
        {
            Vector3 final;
            final.x = matrix.m00 * normal.x + matrix.m10 * normal.y + matrix.m20 * normal.z;

            final.y = matrix.m02 * normal.x + matrix.m11 * normal.y + matrix.m21 * normal.z;

            final.z = matrix.m03 * normal.x + matrix.m12 * normal.y + matrix.m22 * normal.z;
            return final;
        }
        #endregion
    }
}