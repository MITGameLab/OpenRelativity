using UnityEngine;
using System.Collections.Generic;
using System;

namespace OpenRelativity
{
    public class MovementScripts : RelativisticBehavior
    {
        //Consts
        protected const int INIT_FRAME_WAIT = 5;
        private const float DEGREE_TO_RADIAN_CONST = 57.2957795f;
        public float dragConstant = 0.75f;
        public float controllerAcceleration = 8.0f;
        public bool useGravity = false;
        //Needed to tell whether we are in free fall
        protected bool isFalling
        {
            get
            {
                return state.IsPlayerFalling;
            }

            set
            {
                state.IsPlayerFalling = value;
            }
        }
        public List<Collider> collidersBelow { get; protected set; }
        public float controllerBoost = 6000;
        //Affect our rotation speed
        public float rotSpeed;
        //Keep track of the camera transform
        public Transform camTransform;
        //Just turn this negative when they press the Y button for inversion.
        protected int inverted;
        //What is our current target for the speed of light?
        public int speedOfLightTarget { get; set; }
        //What is each step we take to reach that target?
        private float speedOfLightStep;
        //For now, you can change this how you like.
        public float mouseSensitivity;
        //So we can use getAxis as keyHit function
        public bool invertKeyDown { get; set; }
        //Keep track of total frames passed
        protected int frames;
        protected Rigidbody myRigidbody;

        // Based on Strano 2019, (preprint).
        // (I will always implement potentially "cranky" features so you can toggle them off, but I might as well.)
        public bool isMonopoleAccel = false;
        // The composite scalar monopole graviton gas is described by statistical mechanics and heat flow equations
        public float gravitonEmissivity = 0.1f;
        // By default, 12g per baryon mole would be carbon-12, and this controls the total baryons estimated in the object
        public float fundamentalAverageMolarMass = 0.012f;
        public float currentAverageMolarMass = 0.012f;
        public Vector3 leviCivitaDevAccel = Vector3.zero;

        public float baryonCount { get; set; }

        //Keep track of our own Mesh Filter
        private MeshFilter meshFilter;

        public virtual void Start()
        {
            collidersBelow = new List<Collider>();

            //same for RigidBody
            myRigidbody = state.playerTransform.GetComponent<Rigidbody>();
            //Assume we are in free fall
            isFalling = true;
            //If we have gravity, this factors into transforming to optical space.
            if (useGravity) state.HasWorldGravity = true;

            //Lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            //Cursor.visible = false;
            //Set the speed of light to the starting speed of light in GameState
            speedOfLightTarget = (int)state.SpeedOfLight;
            //Inverted, at first
            inverted = -1;
            invertKeyDown = false;

            frames = 0;

            meshFilter = transform.parent.GetComponent<MeshFilter>();

            if (myRigidbody != null)
            {
                baryonCount = myRigidbody.mass * SRelativityUtil.avogadroNumber / currentAverageMolarMass;
            }
        }
        //Again, use LateUpdate to solve some collision issues.
        public virtual void LateUpdate()
        {
            if (!state.isMovementFrozen)
            {
                Collider myColl = GetComponent<Collider>();
                Vector3 extents = myColl.bounds.extents;
                //We assume that the world "down" direction is the direction of gravity.
                Vector3 playerPos = state.playerTransform.position;
                Ray rayDown = new Ray(playerPos + 0.5f * extents.y * Vector3.down, Vector3.down);
                RaycastHit hitInfo;
                // TODO: Layer mask
                isFalling = !Physics.Raycast(rayDown, out hitInfo, 0.5f * extents.y);
            }

            if (!isFalling)
            {
                if (myRigidbody.velocity.y < 0)
                {
                    myRigidbody.velocity = new Vector3(myRigidbody.velocity.x, 0, myRigidbody.velocity.z);
                }
            }

            float viewRotX;
            //If we're not paused, update speed and rotation using player input.
            if (!state.isMovementFrozen)
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
                    rotateX = Quaternion.identity;
                    unRotateX = Quaternion.identity;
                }


                //Turn our camera rotation into a Quaternion. This allows us to make where we're pointing the direction of our added velocity.
                //If you want to constrain the player to just x/z movement, with no Y direction movement, comment out the next two lines
                //and uncomment the line below that is marked
                float cameraRotationAngle = -DEGREE_TO_RADIAN_CONST * Mathf.Acos(Vector3.Dot(camTransform.forward, Vector3.forward));
                Quaternion cameraRotation = Quaternion.AngleAxis(cameraRotationAngle, Vector3.Cross(camTransform.forward, Vector3.forward).normalized);

                //UNCOMMENT THIS LINE if you would like to constrain the player to just x/z movement.
                //Quaternion cameraRotation = Quaternion.AngleAxis(camTransform.eulerAngles.y, Vector3.up);

                Vector3 totalAccel = Vector3.zero;

                float temp;
                //Movement due to left/right input
                totalAccel += new Vector3(0, 0, (temp = -Input.GetAxis("Vertical")) * controllerAcceleration);
                if (temp != 0)
                {
                    state.keyHit = true;
                }
                totalAccel += new Vector3((temp = -Input.GetAxis("Horizontal")) * controllerAcceleration, 0, 0);
                if (temp != 0)
                {
                    state.keyHit = true;
                }

                //And rotate our added velocity by camera angle
                totalAccel = cameraRotation * totalAccel;

                //AUTO SLOW DOWN CODE BLOCK

                //Add a fluid drag force (as for air)
                totalAccel -= dragConstant * playerVelocityVector.sqrMagnitude * playerVelocityVector.normalized;

                Vector3 quasiWorldAccel = totalAccel;

                if (!isFalling)
                {
                    if (quasiWorldAccel.y < 0)
                    {
                        quasiWorldAccel.y = 0;
                    }

                    if (totalAccel.y < 0)
                    {
                        totalAccel.y = 0;
                    }

                    if (useGravity)
                    {
                        totalAccel -= Physics.gravity;
                        quasiWorldAccel = new Vector3(quasiWorldAccel.x, 0, quasiWorldAccel.z);
                    }

                    if (state.conformalMap != null)
                    {
                        totalAccel -= state.conformalMap.GetRindlerAcceleration(state.playerTransform.position);
                    }
                }
                else
                {
                    quasiWorldAccel -= state.conformalMap.GetRindlerAcceleration(state.playerTransform.position);
                    if (useGravity)
                    {
                        quasiWorldAccel -= Physics.gravity;
                    }
                }

                //3-acceleration acts as classically on the rapidity, rather than velocity.
                Vector3 totalVel = playerVelocityVector.AddVelocity((quasiWorldAccel * Time.deltaTime).RapidityToVelocity());
                Vector3 projVOnG = Vector3.Project(totalVel, Physics.gravity);
                if (useGravity && !isFalling && ((projVOnG - Physics.gravity).sqrMagnitude <= SRelativityUtil.divByZeroCutoff))
                {
                    totalVel = totalVel.AddVelocity(projVOnG * totalVel.Gamma());
                    totalVel = new Vector3(totalVel.x, 0, totalVel.z);
                }

                float tvMag = totalVel.magnitude;

                if (tvMag >= state.maxPlayerSpeed - .01f)
                {
                    float gamma = totalVel.Gamma();
                    Vector3 diff = totalVel.normalized * (state.maxPlayerSpeed - .01f) - totalVel;
                    totalVel += diff;
                    totalAccel += diff * gamma;
                } else if (float.IsInfinity(tvMag) || float.IsNaN(tvMag) )
                {
                    totalVel = state.PlayerVelocityVector;
                    state.PlayerAccelerationVector = Vector3.zero;
                }

                state.PlayerVelocityVector = totalVel;
                state.PlayerAccelerationVector = totalAccel;

                if (isMonopoleAccel)
                {
                    // Per Strano 2019, acceleration "nudges" the preferred accelerated rest frame.
                    // (Relativity privileges no "inertial" frame, but there is intrinsic observable difference between "accelerated frames.")
                    // (The author speculates, this accelerated frame "nudge" might be equivalent to the 3-vector potential of the Higgs field.
                    // The scalar potential can excite the "fundamental" rest mass. The independence of the rest mass from gravitational acceleration
                    // has been known since Galileo.)

                    // If a gravitating body this RO is attracted to is already excited above the rest mass vacuum,
                    // (which seems to imply the Higgs field vacuum)
                    // then it will spontaneously emit this excitation, with a coupling constant proportional to the
                    // gravitational constant "G" times (baryon) constituent particle rest mass.
                    // (For video game purposes, there's maybe no easy way to precisely model the mass flow, so just control it with an editor variable.)

                    totalAccel += leviCivitaDevAccel;
                    EvaporateMonopole(Time.deltaTime, totalAccel);
                }

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

                    speedOfLightStep = Mathf.Abs((state.SpeedOfLight - speedOfLightTarget) / 20);
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
                float positionChangeX = -Input.GetAxis("Mouse X");

                //Y axis position change
                float positionChangeY = inverted * Input.GetAxis("Mouse Y");

                //Use these to determine camera rotation, that is, to look around the world without changing direction of motion
                //These two are for X axis rotation and Y axis rotation, respectively
                float viewRotY;
                if (Mathf.Abs(positionChangeX) <= 1 && Mathf.Abs(positionChangeY) <= 1)
                {
                    //Take the position changes and translate them into an amount of rotation
                    viewRotX = -positionChangeX * Time.deltaTime * rotSpeed * mouseSensitivity * controllerBoost;
                    viewRotY = positionChangeY * Time.deltaTime * rotSpeed * mouseSensitivity * controllerBoost;
                }
                else
                {
                    //Take the position changes and translate them into an amount of rotation
                    viewRotX = -positionChangeX * Time.deltaTime * rotSpeed * mouseSensitivity;
                    viewRotY = positionChangeY * Time.deltaTime * rotSpeed * mouseSensitivity;
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
                Shader.SetGlobalFloat("_spdOfLight", state.SpeedOfLight);

                if (Camera.main)
                {
                    Shader.SetGlobalFloat("xyr", Camera.main.pixelWidth / Camera.main.pixelHeight);
                    Shader.SetGlobalFloat("xs", Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView / 2f));

                    //Don't cull because at high speeds, things come into view that are not visible normally
                    //This is due to the lorenz transformation, which is pretty cool but means that basic culling will not work.
                    Camera.main.layerCullSpherical = true;
                    Camera.main.useOcclusionCulling = false;
                }


            }
        }

        protected void EvaporateMonopole(float deltaTime, Vector3 myAccel)
        {
            // If the RelativisticObject is at rest on the ground, according to Strano 2019, (not yet peer reviewed,)
            // it loses surface acceleration, (not weight force, directly,) the longer it stays in this configuration.
            // The Rindler horizon evaporates as would Schwarzschild, for event horizon surface acceleration equivalent
            // between the Rindler and Schwarzschild metrics. Further, Hawking(-Unruh, et al.) acceleration might have
            // the same effect.

            // The Rindler horizon evaporates as a Schwarzschild event horizon with the same surface gravity, according to Strano.
            // We add any background radiation power.
            double alpha = myAccel.magnitude;
            bool isNonZeroTemp = alpha > SRelativityUtil.divByZeroCutoff;

            double r = double.PositiveInfinity;
            // If alpha is in equilibrium with the background temperature, there is no evaporation.
            if (isNonZeroTemp)
            {
                // Surface acceleration at event horizon:
                r = state.SpeedOfLightSqrd / (2 * alpha);
                r = SRelativityUtil.EffectiveRaditiativeRadius((float)r, state.gravityBackgroundPlanckTemperature);
            }

            if (!double.IsInfinity(r) && !double.IsNaN(r))
            {
                isNonZeroTemp = true;
                double alphaF = state.SpeedOfLightSqrd / (2 * (r + SRelativityUtil.SchwarzschildRadiusDecay(deltaTime, r)));
                leviCivitaDevAccel -= (float)(alpha - alphaF) * myAccel.normalized;
            }

            if (r < state.planckLength)
            {
                // For minimum area calculation, below.
                r = state.planckLength;
            }

            if (myRigidbody != null)
            {

                double myTemperature = 0;

                double nuclearMass = myRigidbody.mass / baryonCount;
                double fundamentalNuclearMass = fundamentalAverageMolarMass / SRelativityUtil.avogadroNumber;

                // Per Strano 2019, due to the interaction with the thermal graviton gas radiated by the Rindler horizon,
                // there is also a change in mass. However, the monopole waves responsible for this are seen from a first-person perspective,
                // (i.e. as due to "player" acceleration).
                if (nuclearMass > fundamentalNuclearMass)
                {
                    // If a gravitating body this RO is attracted to is already excited above the rest mass vacuum,
                    // (which seems to imply the Higgs field vacuum)
                    // then it will spontaneously emit this excitation, with a coupling constant proportional to the
                    // gravitational constant "G" times (baryon) constituent particle rest mass.
                    myTemperature = 2 * (myRigidbody.mass - fundamentalNuclearMass) / (baryonCount * state.planckMass);
                }
                //... But just turn "doDegradeAccel" off, if you don't want this effect for any reason.
                // (We ignore the "little bit" of acceleration from collisions, but maybe we could add that next.)

                double surfaceArea;
                if (meshFilter == null)
                {
                    Vector3 lwh = transform.localScale;
                    surfaceArea = 2 * (lwh.x * lwh.y + lwh.x * lwh.z + lwh.y * lwh.z) / state.planckArea;
                }
                else
                {
                    surfaceArea = meshFilter.sharedMesh.SurfaceArea() / state.planckArea;
                }
                // This is the ambient temperature, including contribution from comoving accelerated rest temperature.
                double ambientTemperature = isNonZeroTemp ? SRelativityUtil.SchwarzRadiusToPlanckScaleTemp(r) : state.gravityBackgroundPlanckTemperature;
                double dm = gravitonEmissivity * surfaceArea * SRelativityUtil.sigmaPlanck * (Math.Pow(myTemperature, 4) - Math.Pow(ambientTemperature, 4));

                // Momentum is conserved. (Energy changes.)
                Vector3 momentum = myRigidbody.mass * state.PlayerVelocityVector;

                myRigidbody.mass -= (float)dm;

                if (myRigidbody.mass > SRelativityUtil.divByZeroCutoff)
                {
                    state.PlayerVelocityVector = momentum / myRigidbody.mass;
                }

                float camm = myRigidbody.mass * SRelativityUtil.avogadroNumber / baryonCount;
                currentAverageMolarMass = camm > fundamentalAverageMolarMass ? camm : fundamentalAverageMolarMass;
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

            if (otherRB != null && !otherRB.isKinematic)
            {
                return;
            }

            // Vector3 origPlayerVel = state.PlayerVelocityVector;

            Collider myColl = GetComponent<Collider>();
            Vector3 extents = myColl.bounds.extents;
            //We assume that the world "down" direction is the direction of gravity.
            Vector3 playerPos = state.playerTransform.position;
            Ray rayDown = new Ray(playerPos + 0.5f * extents.y * Vector3.down, Vector3.down);
            Ray rayUp = new Ray(playerPos + extents.y * Vector3.down, Vector3.up);
            Ray rayLeft = new Ray(playerPos + extents.x * Vector3.right, Vector3.left);
            Ray rayRight = new Ray(playerPos + extents.x * Vector3.left, Vector3.right);
            Ray rayForward = new Ray(playerPos + extents.z * Vector3.back, Vector3.forward);
            Ray rayBack = new Ray(playerPos + extents.z * Vector3.forward, Vector3.back);
            RaycastHit hitInfo;
            float dist;
            if (collider.Raycast(rayDown, out hitInfo, 0.5f * extents.y))
            {
                if (frames > INIT_FRAME_WAIT)
                {
                    Vector3 pVel = state.PlayerVelocityVector;
                    Vector3 pVelPerp = new Vector3(pVel.x, 0, pVel.z);
                    if (pVel.y > 0.0f)
                    {
                        state.PlayerVelocityVector = state.PlayerVelocityVector.AddVelocity(new Vector3(0.0f, -pVel.y * pVelPerp.Gamma(), 0.0f));
                        Vector3 totalVel = state.PlayerVelocityVector;
                        state.PlayerVelocityVector = new Vector3(totalVel.x, 0, totalVel.z);
                        Rigidbody myRB = transform.parent.GetComponent<Rigidbody>();
                        myRB.velocity = new Vector3(myRB.velocity.x, 0, myRB.velocity.z);
                    }

                    Vector3 pAccel = state.PlayerAccelerationVector;
                    if (pAccel.y > 0.0f)
                    {
                        pAccel.y = 0.0f;
                        state.PlayerAccelerationVector = pAccel;
                    }

                    
                    dist = 0.5f * extents.y - hitInfo.distance;
                    if (dist > 0.02f)
                    {
                        Vector3 pos = state.playerTransform.position;
                        state.playerTransform.position = new Vector3(pos.x, pos.y + dist, pos.z);
                    }
                }
            }

            Vector3 direction = Vector3.zero;
            if (collider.Raycast(rayForward, out hitInfo, 2.0f * extents.z))
            {
                direction = Vector3.forward;
            }
            else if (collider.Raycast(rayBack, out hitInfo, 2.0f * extents.z))
            {
                direction = Vector3.back;
            }
            else if (collider.Raycast(rayLeft, out hitInfo, 2.0f * extents.x))
            {
                direction = Vector3.left;
            }
            else if (collider.Raycast(rayRight, out hitInfo, 2.0f * extents.x))
            {
                direction = Vector3.right;
            }
            else if (collider.Raycast(rayUp, out hitInfo, 2.0f * extents.y))
            {
                direction = Vector3.up;
            }

            if (direction != Vector3.zero)
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
            }

            // Vector3 accel = (state.PlayerVelocityVector - origPlayerVel) / state.FixedDeltaTimePlayer;
            // EvaporateMonopole(state.FixedDeltaTimePlayer, accel);
        }
    }
}