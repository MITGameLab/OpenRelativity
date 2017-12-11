using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using OpenRelativity.Objects;

namespace OpenRelativity
{
    public class MovementScripts : MonoBehaviour
    {
        //Consts 
        private const float SLOW_DOWN_RATE = 0.75f;
        private const float ACCEL_RATE = 7.5f;
        private const int INIT_FRAME_WAIT = 5;
        private const float DEGREE_TO_RADIAN_CONST = 57.2957795f;
        public bool useGravity = false;
        //Needed to tell whether we are in free fall
        private bool isFalling;
        private List<Collider> collidersBelow;
        public float controllerBoost = 6000;
        //Affect our rotation speed
        public float rotSpeed;
        //Keep track of the camera transform
        public Transform camTransform;
        //Just turn this negative when they press the Y button for inversion.
        private int inverted;
        //What is our current target for the speed of light?
        public int speedOfLightTarget { get; set; }
        //What is each step we take to reach that target?
        private float speedOfLightStep;
        //For now, you can change this how you like.
        public float mouseSensitivity;
        //So we can use getAxis as keyHit function
        public bool invertKeyDown { get; set; }
        //Keep track of total frames passed
        int frames;
        //How fast are we going to shoot the bullets?
        public float viwMax = 3;
        //Gamestate reference for quick access
        GameState state;

        void Start()
        {
            collidersBelow = new List<Collider>();

            //grab Game State, we need it for many actions
            state = GetComponent<GameState>();
            //Assume we are in free fall
            isFalling = true;
            //If we have gravity, this factors into transforming to optical space.
            if (useGravity) state.HasWorldGravity = true;

            //grab Game State, we need it for many actions
            state = GetComponent<GameState>();
            //Lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            //Cursor.visible = false;
            //Set the speed of light to the starting speed of light in GameState
            speedOfLightTarget = (int)state.SpeedOfLight;
            //Inverted, at first
            inverted = -1;
            invertKeyDown = false;

            viwMax = Mathf.Min(viwMax, (float)GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>().MaxSpeed);

            frames = 0;
        }
        //Again, use LateUpdate to solve some collision issues.
        void LateUpdate()
        {
            if (true)
            {
                float viewRotX = 0;
                //If we're not paused, update speed and rotation using player input.
                if (!state.MovementFrozen)
                {
                    state.deltaRotation = Vector3.zero;

                    //If they press the Y button, invert all Y axes
                    if (Input.GetAxis("Invert Button") > 0 && !invertKeyDown)
                    {
                        inverted *= -1;
                        invertKeyDown = true;
                    }
                    //And if they released it, set the invertkeydown to false.
                    else if (!(Input.GetAxis("Invert Button") > 0))
                    {
                        invertKeyDown = false;
                    }

                    #region ControlScheme

                    //PLAYER MOVEMENT

                    //If we press W, move forward, if S, backwards.
                    //A adds speed to the left, D to the right. We're using FPS style controls
                    //Here's hoping they work.

                    //The acceleration relation is defined by the following equation
                    //vNew = (v+uParallel+ (uPerpendicular/gamma))/(1+(v*u)/c^2)

                    //Okay, so Gerd found a good equation that doesn't break when your velocity is zero, BUT your velocity has to be in the x direction.
                    //So, we're gonna reuse some code from our relativisticObject component, and rotate everything to be at the X axis.

                    //Cache our velocity
                    Vector3 playerVelocityVector = state.PlayerVelocityVector;

                    //Get our angle between the velocity and the X axis. Get the angle in degrees (radians suck)
                    float rotationAroundX = DEGREE_TO_RADIAN_CONST * Mathf.Acos(Vector3.Dot(playerVelocityVector, Vector3.right) / playerVelocityVector.magnitude);

                    //Make a Quaternion from the angle, one to rotate, one to rotate back. 
                    Quaternion rotateX = Quaternion.AngleAxis(rotationAroundX, Vector3.Cross(playerVelocityVector, Vector3.right).normalized);
                    Quaternion unRotateX = Quaternion.AngleAxis(rotationAroundX, Vector3.Cross(Vector3.right, playerVelocityVector).normalized);


                    //If the magnitude's zero just make these angles zero and the Quaternions identity Q's
                    if (playerVelocityVector.sqrMagnitude == 0)
                    {
                        rotationAroundX = 0;
                        rotateX = Quaternion.identity;
                        unRotateX = Quaternion.identity;
                    }


                    //Store our added velocity into temporary variable addedVelocity
                    Vector3 addedVelocity = Vector3.zero;

                    //Turn our camera rotation into a Quaternion. This allows us to make where we're pointing the direction of our added velocity.
                    //If you want to constrain the player to just x/z movement, with no Y direction movement, comment out the next two lines
                    //and uncomment the line below that is marked
                    float cameraRotationAngle = -DEGREE_TO_RADIAN_CONST * Mathf.Acos(Vector3.Dot(camTransform.forward, Vector3.forward));
                    Quaternion cameraRotation = Quaternion.AngleAxis(cameraRotationAngle, Vector3.Cross(camTransform.forward, Vector3.forward).normalized);

                    //UNCOMMENT THIS LINE if you would like to constrain the player to just x/z movement.
                    //Quaternion cameraRotation = Quaternion.AngleAxis(camTransform.eulerAngles.y, Vector3.up);


                    float temp;
                    //Movement due to left/right input
                    addedVelocity += new Vector3(0, 0, (temp = -Input.GetAxis("Vertical")) * ACCEL_RATE) * Time.deltaTime;
                    if (temp != 0)
                    {
                        state.keyHit = true;
                    }
                    addedVelocity += new Vector3((temp = -Input.GetAxis("Horizontal")) * ACCEL_RATE, 0, 0) * Time.deltaTime;
                    if (temp != 0)
                    {
                        state.keyHit = true;
                    }

                    //And rotate our added velocity by camera angle
                    addedVelocity = cameraRotation * addedVelocity;

                    //AUTO SLOW DOWN CODE BLOCK

                    //If we are not adding velocity this round to our x direction, slow down
                    if (addedVelocity.x == 0)
                    {
                        addedVelocity += new Vector3(-1 * SLOW_DOWN_RATE * playerVelocityVector.x, 0, 0) * Time.deltaTime;
                    }
                    //If we are not adding velocity this round to our z direction, slow down
                    if (addedVelocity.z == 0)
                    {
                        addedVelocity += new Vector3(0, 0, -1 * SLOW_DOWN_RATE * playerVelocityVector.z) * Time.deltaTime;
                    }
                    //If we are not adding velocity this round to our y direction, slow down
                    if (addedVelocity.y == 0)
                    {
                        addedVelocity += new Vector3(0, -1 * SLOW_DOWN_RATE * playerVelocityVector.y, 0) * Time.deltaTime;
                    }

                    /*
                     * IF you turn on this bit of code, you'll get head bob. It's a fun little effect, but if you make the magnitude of the cosine too large it gets sickening.
                    if (!double.IsNaN((float)(0.2 * Mathf.Cos((float)GetComponent<GameState>().TotalTimePlayer) * Time.deltaTime)) && frames > 2)
                    {
                        addedVelocity.y += (float)(0.2 * Mathf.Cos((float)GetComponent<GameState>().TotalTimePlayer) * Time.deltaTime);
                    }
                    */

                    Vector3 totalAccel = Vector3.zero;
                    //Add the velocities here. remember, this is the equation:
                    //vNew = (1/(1+vOld*vAddx/cSqrd))*(Vector3(vAdd.x+vOld.x,vAdd.y/Gamma,vAdd.z/Gamma))
                    if (addedVelocity.sqrMagnitude != 0 || useGravity)
                    {
                        //Rotate our velocity Vector    
                        Vector3 rotatedVelocity = rotateX * playerVelocityVector;

                        if (addedVelocity.sqrMagnitude != 0)
                        {
                            //Rotate our added Velocity
                            addedVelocity = rotateX * addedVelocity;

                            //get gamma so we don't have to bother getting it every time
                            float gamma = (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;

                            //Do relativistic velocity addition as described by the above equation.
                            rotatedVelocity = (1 / (1 + (rotatedVelocity.x * addedVelocity.x) / (float)state.SpeedOfLightSqrd)) *
                                (new Vector3(addedVelocity.x + rotatedVelocity.x, addedVelocity.y * gamma, gamma * addedVelocity.z));
                        }

                        //Unrotate our new total velocity
                        rotatedVelocity = unRotateX * rotatedVelocity;
                        totalAccel = (addedVelocity / Time.deltaTime);
                        totalAccel = totalAccel.Gamma() * totalAccel;

                        //Set it, depending on gravity
                        if (!useGravity)
                        {
                            state.PlayerVelocityVector = rotatedVelocity;
                        }
                        else
                        {
                            if (!isFalling)
                            {
                                if (rotatedVelocity.y > 0.0f)
                                {
                                    rotatedVelocity.y = 0.0f;
                                }
                                state.PlayerVelocityVector = rotatedVelocity;
                                totalAccel -= Physics.gravity;
                            }
                            else
                            {
                                state.PlayerVelocityVector = rotatedVelocity.AddVelocity((-Physics.gravity * Time.deltaTime).RapidityToVelocity());
                            }
                        }
                    }

                    state.PlayerAccelerationVector = totalAccel;

                    //CHANGE the speed of light

                    //Get our input axis (DEFAULT N, M) value to determine how much to change the speed of light
                    int temp2 = (int)(Input.GetAxis("Speed of Light"));
                    //If it's too low, don't subtract from the speed of light, and reset the speed of light
                    if (temp2 < 0 && speedOfLightTarget <= state.MaxSpeed)
                    {
                        temp2 = 0;
                        speedOfLightTarget = (int)state.MaxSpeed;
                    }
                    if (temp2 != 0)
                    {
                        speedOfLightTarget += temp2;

                        speedOfLightStep = Mathf.Abs((float)(state.SpeedOfLight - speedOfLightTarget) / 20);
                    }
                    //Now, if we're not at our target, move towards the target speed that we're hoping for
                    if (state.SpeedOfLight < speedOfLightTarget * .995)
                    {
                        //Then we change the speed of light, so that we get a smooth change from one speed of light to the next.
                        state.SpeedOfLight += speedOfLightStep;
                    }
                    else if (state.SpeedOfLight > speedOfLightTarget * 1.005)
                    {
                        //See above
                        state.SpeedOfLight -= speedOfLightStep;
                    }
                    //If we're within a +-.05 distance of our target, just set it to be our target.
                    else if (state.SpeedOfLight != speedOfLightTarget)
                    {
                        state.SpeedOfLight = speedOfLightTarget;
                    }

                    //MOUSE CONTROLS
                    //Current position of the mouse
                    //Difference between last frame's mouse position
                    //X axis position change
                    float positionChangeX = -(float)Input.GetAxis("Mouse X");

                    //Y axis position change
                    float positionChangeY = (float)inverted * Input.GetAxis("Mouse Y");

                    //Use these to determine camera rotation, that is, to look around the world without changing direction of motion
                    //These two are for X axis rotation and Y axis rotation, respectively
                    float viewRotY = 0;
                    if (Mathf.Abs(positionChangeX) <= 1 && Mathf.Abs(positionChangeY) <= 1)
                    {
                        //Take the position changes and translate them into an amount of rotation
                        viewRotX = (float)(-positionChangeX * Time.deltaTime * rotSpeed * mouseSensitivity * controllerBoost);
                        viewRotY = (float)(positionChangeY * Time.deltaTime * rotSpeed * mouseSensitivity * controllerBoost);
                    }
                    else
                    {
                        //Take the position changes and translate them into an amount of rotation
                        viewRotX = (float)(-positionChangeX * Time.deltaTime * rotSpeed * mouseSensitivity);
                        viewRotY = (float)(positionChangeY * Time.deltaTime * rotSpeed * mouseSensitivity);
                    }
                    //Perform Rotation on the camera, so that we can look in places that aren't the direction of movement
                    //Wait some frames on start up, otherwise we spin during the intialization when we can't see yet
                    if (frames > INIT_FRAME_WAIT)
                    {
                        camTransform.Rotate(new Vector3(0, viewRotX, 0), Space.World);
                        if ((camTransform.eulerAngles.x + viewRotY < 90 && camTransform.eulerAngles.x + viewRotY > 90 - 180) || (camTransform.eulerAngles.x + viewRotY > 270 && camTransform.eulerAngles.x + viewRotY < 270 + 180))
                        {
                            camTransform.Rotate(new Vector3(viewRotY, 0, 0));
                        }
                    }
                    else
                    {
                        //keep track of our frames
                        frames++;
                    }

                    //If we have a speed of light less than max speed, fix it.
                    //This should never happen
                    if (state.SpeedOfLight < state.MaxSpeed)
                    {
                        state.SpeedOfLight = state.MaxSpeed;
                    }


                    #endregion

                    //Send current speed of light to the shader
                    Shader.SetGlobalFloat("_spdOfLight", (float)state.SpeedOfLight);

                    if (Camera.main)
                    {
                        Shader.SetGlobalFloat("xyr", (float)Camera.main.pixelWidth / Camera.main.pixelHeight);
                        Shader.SetGlobalFloat("xs", (float)Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView / 2f));

                        //Don't cull because at high speeds, things come into view that are not visible normally
                        //This is due to the lorenz transformation, which is pretty cool but means that basic culling will not work.
                        Camera.main.layerCullSpherical = true;
                        Camera.main.useOcclusionCulling = false;
                    }


                }
            }
        }

        void OnTriggerEnter(Collider collider)
        {
            OnTrigger(collider);
        }

        void OnTriggerStay(Collider collider)
        {
            OnTrigger(collider);
        }

        //Note that this method assumes that walls are locked at right angles to the world coordinate axes.
        private void OnTrigger(Collider collider)
        {
            if (collider.isTrigger)
            {
                return;
            }

            Rigidbody otherRB = collider.GetComponent<Rigidbody>();
            GameObject otherGO = collider.gameObject;
            RelativisticObject otherRO = otherGO.GetComponent<RelativisticObject>();
            if (otherRO != null && otherRB != null && otherRB.isKinematic)
            {
                Collider myColl = GetComponent<Collider>();
                Vector3 extents = myColl.bounds.extents;
                //We assume that the world "down" direction is the direction of gravity.
                Vector3 playerPos = state.playerTransform.position;
                Ray rayDown = new Ray(playerPos + extents.y * Vector3.up, Vector3.down);
                Ray rayUp = new Ray(playerPos + extents.y * Vector3.down, Vector3.up);
                Ray rayLeft = new Ray(playerPos + extents.x * Vector3.right, Vector3.left);
                Ray rayRight = new Ray(playerPos + extents.x * Vector3.left, Vector3.right);
                Ray rayForward = new Ray(playerPos + extents.z * Vector3.back, Vector3.forward);
                Ray rayBack = new Ray(playerPos + extents.z * Vector3.forward, Vector3.back);
                RaycastHit hitInfo;
                float dist;
                if (collider.Raycast(rayDown, out hitInfo, 2.0f * extents.y))
                {
                    isFalling = false;
                    Vector3 pVel = state.PlayerVelocityVector;
                    Vector3 pVelPerp = new Vector3(pVel.x, 0, pVel.z);
                    if (pVel.y > 0.0f)
                    {
                        state.PlayerVelocityVector = state.PlayerVelocityVector.AddVelocity(new Vector3(0.0f, -pVel.y * pVelPerp.Gamma(), 0.0f));
                    }

                    otherRO.UpdateColliderPosition(collider);
                    Ray longDown = new Ray(playerPos + 8.0f * extents.y * Vector3.up, Vector3.down);
                    if (collider.Raycast(longDown, out hitInfo, 16.0f * extents.y))
                    {
                        Vector3 newPos = hitInfo.point + new Vector3(0.0f, extents.y - 0.1f, 0.0f);
                        dist = transform.position.y - newPos.y;
                        if (Mathf.Abs(dist) > 0.01f)
                        {
                            state.playerTransform.position = newPos;
                        }
                    }

                    bool foundCollider = false;
                    for (int i = 0; i < collidersBelow.Count; i++)
                    {
                        if (collidersBelow[i].Equals(collider))
                        {
                            foundCollider = true;
                        }
                    }

                    if (!foundCollider)
                    {
                        collidersBelow.Add(collider);
                    }
                }

                Vector3 direction = Vector3.zero;
                dist = 0.0f;
                if (collider.Raycast(rayForward, out hitInfo, 2.0f * extents.z))
                {
                    dist = 2.0f * extents.z - hitInfo.distance;
                    direction = Vector3.forward;
                }
                else if (collider.Raycast(rayBack, out hitInfo, 2.0f * extents.z))
                {
                    dist = 2.0f * extents.z - hitInfo.distance;
                    direction = Vector3.back;
                }
                else if (collider.Raycast(rayLeft, out hitInfo, 2.0f * extents.x))
                {
                    dist = 2.0f * extents.x - hitInfo.distance;
                    direction = Vector3.left;
                }
                else if (collider.Raycast(rayRight, out hitInfo, 2.0f * extents.x))
                {
                    dist = 2.0f * extents.x - hitInfo.distance;
                    direction = Vector3.right;
                }
                else if (collider.Raycast(rayUp, out hitInfo, 2.0f * extents.y))
                {
                    dist = 2.0f * extents.y - hitInfo.distance;
                    direction = Vector3.up;
                }

                if (dist > 0.0f)
                {
                    Vector3 pVel = state.PlayerVelocityVector;
                    if (Vector3.Dot(pVel, direction) < 0.0f)
                    {
                        //Decompose velocity in parallel and perpendicular components:
                        Vector3 myParraVel = Vector3.Project(pVel, direction) * 2.0f;
                        //Vector3 myPerpVel = Vector3.Cross(direction, Vector3.Cross(direction, pVel));
                        //Relativistically cancel the downward velocity:
                        state.PlayerVelocityVector = state.PlayerVelocityVector - myParraVel;
                    }
                    state.playerTransform.position -= dist * direction;
                }
            }
        }

        void OnTriggerExit(Collider collider)
        {
            collidersBelow.Remove(collider);
            if (collidersBelow.Count == 0)
            {
                isFalling = true;
            }
        }
    }
}