using UnityEngine;
using UnityEngine.UI;
using Google.XR.Cardboard;
using System.Collections;
using System.Collections.Generic;
using OpenRelativity;
using Tachyoid.Objects;

namespace Tachyoid
{
    public class FTLPlayerController : PlayerController
    {
        public Text menuUI;
        public GameObject menuReticle;
        private string menuUIOffString;
        private string menuUIOnString;
        public GameObject sceneBackground;
        public GameObject selectMenu;
        public List<GameObject> menuParents;
        public SchwarzschildLens schwarzschildLens;
        public SchwarzschildLens schwarzschildMirror;
        public TutorialController tutorialUI;
        public bool isMenuUIOn { get; private set; }

        //Consts 
        //Are we allowing player control?
        public bool allowInput = true;
        public bool verticalLock = false;
        //Let portal visibility system know if we're time traveling through portals:
        public int? pvsInstanceID { get; set; }

        //When we time travel, cache our original velocity,
        // and replace it with a time travel prediction
        private Vector3 pausedPlayerVelocity;

        private float clickTimer;
        private bool wasPressed;
        public float levelStartTimeTravelDelay = 5.0f;
        public float cardboardTriggerCooldown = 0.01f;
        private float wasPressedTime;
        private bool legalTimeTravel = true;
        public double timeTravelCMultiplier = 20.0f;
        public float timeTravelTimeDif { get; private set; }
        public Vector3 timeTravelPosDif { get; private set; }
        public Reticle reticle;
        public Vector3 reticleOffset = new Vector3(0.0f, -3.0f, 0.0f);
        public float maxTimeTravelDistance = 50.0f;
        public float minTimeTravelDistance = 10.0f;
        public float doubleClickInterval = 0.7f;
        public float graspRadius = 2.0f;
        public float graspAngle = 45.0f;
        public float graspCooldownSeconds = 3.0f;
        public float graspCooldownDistance = 5.0f;
        public float itemThrowVelocity = 0.5f;
        private float graspTimer;
        private bool timeTravelInitiated;
        public bool holdingItem { get; private set; }
        public Graspable itemInHand { get; private set; }
        private Graspable itemWasInHand;
        private bool reversingT;
        //We're going to switch the player transform out in order to get the shaders to show a time travel prediction:
        private Transform cachedPlayerTransform;
        private Transform playerHeadTransform;

        private AudioSource warpSound;
        private AudioSource warpConfirmSound;
        private AudioSource warpCancelSound;
        private AudioSource warpBlockedSound;
        private AudioSource warpTargetSound;
        private AudioSource walkingSound;
        public ParticleSystem warpFlash;

        public GameObject hud;

        public Action playerAction { get; set; }
        private bool justReversedTime;

        private const float MOUSE_RESET_SPEED = 2.0f;

        public override void Start()
        {
            base.Start();

            Cursor.lockState = CursorLockMode.None;

            //intro
            menuUIOffString = "Double-press\nfor menu";
            menuUIOnString = "Double-press\nto exit";

            if (schwarzschildLens != null)
            {
                menuUIOffString = "";
            }

            if (menuUI != null) {
                menuUI.text = menuUIOffString;
            }

            isMenuUIOn = false;
            DeactivateMenus();

            clickTimer = 0.0f;
            wasPressed = false;
            wasPressedTime = 0.0f;
            legalTimeTravel = false;
            timeTravelTimeDif = 0.0f;
            timeTravelPosDif = Vector3.zero;
            timeTravelInitiated = false;
            graspTimer = 0.0f;
            reversingT = false;
            holdingItem = false;
            justReversedTime = false;

            AudioSource[] warpSounds = GetComponents<AudioSource>();
            if (warpSounds.Length >= 5)
            {
                warpSound = warpSounds[0];
                warpConfirmSound = warpSounds[1];
                warpCancelSound = warpSounds[2];
                warpBlockedSound = warpSounds[3];
                warpTargetSound = warpSounds[4];

                if (warpSounds.Length >= 6)
                {
                    walkingSound = warpSounds[5];
                }
            }
            else if (warpSounds.Length > 0)
            {
                walkingSound = warpSounds[0];
            } else {
                walkingSound = null;
            }

            cachedPlayerTransform = state.playerTransform;
            playerHeadTransform = Camera.main.transform;
        }

        private void PrepTimeTravelPreview()
        {
            if (warpTargetSound != null)
            {
                warpTargetSound.Play();
            }

            TachyoidGameState tState = (TachyoidGameState)state;

            tState.PlayerVelocityVectorBeforeReverse = tState.PlayerVelocityVector;
            tState.PlayerAccelerationBeforeReverse = tState.PlayerAccelerationVector;
            pausedPlayerVelocity = state.PlayerVelocityVector;
            tState.PlayerPositionBeforeReverse = tState.playerTransform.position;
            tState.PlayerVelocityVector = Vector3.zero;
            tState.isMovementFrozen = true;
            tState.playerTransform = reticle.transform;
            if (hud != null) {
                hud.SetActive(true);
            }
            clickTimer = doubleClickInterval + 0.1f;
        }

        //Again, use LateUpdate to solve some collision issues.
        public override void LateUpdate()
        {
            if (frames <= INIT_FRAME_WAIT)
            {
                frames++;
            }

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

            if (!state.isMovementFrozen)
            {
                if (!isFalling && Vector3.Dot(camTransform.forward, Vector3.down) > 0.70710678118f)
                {
                    if (!isMenuUIOn)
                    {
                        FadeInUI();
                    }
                }
                else if (isMenuUIOn)
                {
                    FadeOutUI();
                }
            }
            else if (isMenuUIOn)
            {
                FadeOutUI();
            }

            if (!isFalling)
                {
                if (myRigidbody.velocity.y < 0)
                {
                    myRigidbody.velocity = new Vector3(myRigidbody.velocity.x, 0, myRigidbody.velocity.z);
                }
            }

            bool isPressed = allowInput && (Input.GetMouseButton(0) || Api.IsTriggerPressed);
            bool isTriggered = isPressed && !wasPressed && Time.time > (wasPressedTime + cardboardTriggerCooldown);
            bool isDoubleTriggered = isTriggered && clickTimer > cardboardTriggerCooldown && clickTimer < doubleClickInterval;
            clickTimer = isTriggered ? 0f : clickTimer + Time.deltaTime;
            bool isCancellable = isTriggered || (clickTimer < doubleClickInterval);
            isTriggered &= !isDoubleTriggered;

            if (isPressed && !wasPressed)
            {
                wasPressedTime = Time.time;
            }

            if ((walkingSound != null) && (!isPressed || state.isMovementFrozen))
            {
                walkingSound.Stop();
            }

            wasPressed = isPressed;

            if (!reversingT)
            {
                if (isTriggered || isDoubleTriggered)
                {
                    if (isMenuUIOn && isDoubleTriggered)
                    {
                        if (sceneBackground.activeSelf)
                        {
                            pausedPlayerVelocity = state.PlayerVelocityVector;
                            state.PlayerVelocityVector = Vector3.zero;
                            sceneBackground.SetActive(false);
                            selectMenu.transform.forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
                            ActivateMenus();
                            reticle.enabled = false;
                            if (tutorialUI != null)
                            {
                                tutorialUI.gameObject.SetActive(false);
                            }
                            menuUI.text = menuUIOnString;
                        }
                        else
                        {
                            sceneBackground.SetActive(true);
                            DeactivateMenus();
                            menuReticle.SetActive(false);
                            if (state.isMovementFrozen) reticle.enabled = true;
                            if (tutorialUI != null && !tutorialUI.isFinished) tutorialUI.gameObject.SetActive(true);
                            menuUI.text = menuUIOffString;
                            state.PlayerVelocityVector = pausedPlayerVelocity;
                            pausedPlayerVelocity = Vector3.zero;
                        }
                    }
                    else if (sceneBackground.activeSelf)
                    {
                        if (state.isMovementFrozen)
                        {
                            if (isTriggered && !timeTravelInitiated)
                            {
                                if (legalTimeTravel)
                                {
                                    if (warpConfirmSound != null)
                                    {
                                        warpConfirmSound.Play();
                                    }
                                    timeTravelInitiated = true;
                                    //reticle.Freeze();
                                }
                                else if (warpBlockedSound != null)
                                {
                                    warpBlockedSound.Play();
                                }
                            } 
                            else if (isDoubleTriggered)
                            {
                                state.playerTransform = cachedPlayerTransform;
                                state.PlayerVelocityVector = pausedPlayerVelocity;
                                state.isMovementFrozen = false;
                                timeTravelInitiated = false;
                                if (hud != null) {
                                   hud.SetActive(false);
                                }
                                reticle.Hide();
                                if (warpCancelSound != null)
                                {
                                    warpCancelSound.Play();
                                }

                                timeTravelTimeDif = 0.0f;
                                timeTravelPosDif = Vector3.zero;
                            }
                        }
                        else if (isDoubleTriggered)
                        {
                            if (holdingItem)
                            {
                                clickTimer = doubleClickInterval + 0.1f;
                                //45 degree angle from straight down, cos(angle)
                                if (Vector3.Dot(camTransform.forward, Vector3.down) > 0.70710678118f)
                                {
                                    Collider myCollider = GetComponent<Collider>();
                                    itemWasInHand = itemInHand;
                                    //itemWasInHandPosition = itemWasInHand.transform.position;
                                    itemInHand.ChangeHolder(null, myCollider, null, state.PlayerVelocityVector + (itemThrowVelocity * playerHeadTransform.forward));
                                    itemInHand = null;
                                    holdingItem = false;
                                    graspTimer = graspCooldownSeconds;
                                }
                                else if (state.TotalTimePlayer > levelStartTimeTravelDelay)
                                {
                                    PrepTimeTravelPreview();
                                }
                            }
                            //If not holding item:
                            else if (state.TotalTimePlayer > levelStartTimeTravelDelay)
                            {
                                PrepTimeTravelPreview();
                            }
                        }
                    }
                }

                if (allowInput && sceneBackground.activeSelf)
                {
                    if (state.isMovementFrozen && !timeTravelInitiated)
                    {
                        UpdateTimeTravelPrediction();
                        if (itemInHand != null)
                        {
                            itemInHand.MoveDirectlyToRestingPoint();
                        }
                    }

                    if (graspTimer > 0.0f)
                    {
                        graspTimer -= Time.deltaTime;
                        if (graspTimer <= 0.0f)
                        {
                            ResetGrasp();
                        }
                    }
                    else if (!holdingItem)
                    {
                        Collider nearest = CheckIfGraspableNear();
                        if (nearest != null)
                        {
                            Graspable pickupItem = nearest.GetComponent<Graspable>();
                            if (pickupItem != itemWasInHand)
                            {
                                Collider myCollider = GetComponent<Collider>();
                                itemInHand = pickupItem;
                                itemInHand.ChangeHolder(camTransform, myCollider, null, state.PlayerVelocityVector);
                                holdingItem = true;
                            }
                        }
                    }

                    #region ControlScheme
                    //Store our added velocity into temporary variable addedVelocity
                    Vector3 playerVelocityVector = state.PlayerVelocityVector;
                    Vector3 totalAccel = Vector3.zero;
                    Vector3 quasiWorldAccel = Vector3.zero;
                    if (isPressed || useGravity)
                    {
                        //If we're not paused, update speed and rotation using player input.
                        if (!state.isMovementFrozen)
                        {
                            //PLAYER MOVEMENT

                            //Pressing the button accelerates the player in the direction that the VR head is pointed.
                            // When the button isn't pressed, we gradually deccelerate to the world frame.

                            if (isPressed)
                            {
                                if (verticalLock)
                                {
                                    totalAccel = -controllerAcceleration * Vector3.ProjectOnPlane(camTransform.transform.forward, Vector3.up).normalized;
                                }
                                else
                                {
                                    totalAccel = -controllerAcceleration * camTransform.transform.forward;
                                }
                                state.keyHit = true;
                                if (!isFalling && walkingSound != null && !walkingSound.isPlaying)
                                {
                                    walkingSound.Play();
                                }

                                //Add the velocities here.
                            }

                            quasiWorldAccel = totalAccel;

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
                                    totalAccel -= ((TachyoidGameState)state).PlayerGravity;
                                }

                                if (state.conformalMap != null)
                                {
                                    totalAccel -= state.conformalMap.GetRindlerAcceleration(state.playerTransform.position);
                                }
                            }
                            else if (useGravity)
                            {
                                quasiWorldAccel -= ((TachyoidGameState)state).PlayerGravity;
                            }

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

                                EvaporateMonopole(Time.deltaTime, totalAccel);
                                quasiWorldAccel += leviCivitaDevAccel;
                                totalAccel += leviCivitaDevAccel;
                            }
                        }
                    }

                    //MOUSE CONTROLS
                    //Difference between last frame's mouse position
                    //Use these to determine camera rotation, that is, to look around the world without changing direction of motion
                    //These two are for X axis rotation and Y axis rotation, respectively
                    //Take the position changes and translate them into an amount of rotation
                    if (!timeTravelInitiated)
                    {
                        if (Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.Z))
                        {
                            float viewRotX = 0;
                            float viewRotY = 0;
                            if (Input.GetKey(KeyCode.X))
                            {
                                viewRotX = Input.GetAxis("Mouse X") * Time.deltaTime * mouseSensitivity;
                            }
                            if (Input.GetKey(KeyCode.Z))
                            {
                                viewRotY = -Input.GetAxis("Mouse Y") * Time.deltaTime * mouseSensitivity;
                            }

                            camTransform.Rotate(new Vector3(viewRotY, viewRotX, 0), Space.Self);
                        }
                        else
                        {
                            Quaternion camRot = Quaternion.FromToRotation(camTransform.up, Vector3.up);
                            camRot = Quaternion.Slerp(Quaternion.identity, camRot, MOUSE_RESET_SPEED * Time.deltaTime);
                            camTransform.Rotate(camRot.eulerAngles, Space.World);
                        }
                    }

                    //AUTO SLOW DOWN CODE BLOCK

                    Vector3 slowDown = -1 * dragConstant * playerVelocityVector;
                    totalAccel += slowDown;
                    quasiWorldAccel += slowDown;

                    //3-acceleration acts as classically on the rapidity, rather than velocity.
                    Vector3 totalVel = playerVelocityVector.AddVelocity((quasiWorldAccel * Time.deltaTime).RapidityToVelocity());
                    Vector3 projVOnG = Vector3.Project(totalVel, ((TachyoidGameState)state).PlayerGravity);
                    if (useGravity && !isFalling && ((projVOnG - ((TachyoidGameState)state).PlayerGravity).sqrMagnitude <= SRelativityUtil.FLT_EPSILON))
                    {
                        totalVel = totalVel.AddVelocity(projVOnG * totalVel.Gamma());
                        totalVel = new Vector3(totalVel.x, 0, totalVel.z);
                    }

                    state.PlayerVelocityVector = totalVel;
                    state.PlayerAccelerationVector = totalAccel;
                    #endregion

                    if (timeTravelInitiated && !isCancellable)
                    {
                        state.playerTransform = cachedPlayerTransform;
                        state.PlayerVelocityVector = pausedPlayerVelocity;
                        ((TachyoidGameState)state).WorldTimePrediction = ((TachyoidGameState)state).WorldTimeBeforeReverse - timeTravelTimeDif;
                        //Physics update happens here:
                        ReverseTime();
                        state.isMovementFrozen = false;
                        if (hud != null) {
                            hud.SetActive(false);
                        }
                        timeTravelInitiated = false;
                    }
                }
            }

            //Send current speed of light to the shader
            if (state.SpeedOfLight > 0)
            {
                Shader.SetGlobalFloat("_spdOfLight", state.SpeedOfLight);
            }
            else
            {
                Shader.SetGlobalFloat("_spdOfLight", 100000f);
            }

            if (Camera.main)
            {
                Shader.SetGlobalFloat("xyr", Camera.main.pixelWidth / Camera.main.pixelHeight);
                Shader.SetGlobalFloat("xs", Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView / 2f));

                //Don't cull because at high speeds, things come into view that are not visible normally
                //This is due to the lorenz transformation, which is pretty cool but means that basic culling will not work.
                Camera.main.layerCullSpherical = true;
                Camera.main.useOcclusionCulling = true;
            }

            TachyoidGameState tState = (TachyoidGameState)state;

            if (tState.ReversingTime)
            {
                playerAction = Action.didBoost;
                justReversedTime = true;
            }
            else if (justReversedTime && !tState.ReversingTime)
            {
                playerAction = Action.exitedBoost;
                justReversedTime = false;
            }
            else if (tState.IsPlayerFalling)
            {
                playerAction = Action.isJumping;
            }
            else if (state.PlayerVelocityVector.sqrMagnitude > 0.0f)
            {
                playerAction = Action.isRunning;
            }
        }

        #region Time Travel
        //Interface methods:
        public void ReverseTime()
        {
            //Debug.Log ("Player reverse method called at " + invClock.time);

            //The call to state.ReverseTime(...) spans multiple frames. We set a flag locally to let us know we're reversing time.
            reversingT = true;
            reticle.Hide();

            ((TachyoidGameState)state).ReverseTime(timeTravelPosDif);

            if (itemInHand != null)
            {
                itemInHand.MoveDirectlyToRestingPoint();
            }

            if (warpSound != null)
            {
                warpSound.Play();
            }
            if (warpFlash != null)
            { 
                warpFlash.Play();
            }

            //Now we release any locks:
            reversingT = false;
        }

        private void UpdateTimeTravelPrediction()
        {
            RaycastTimeTravelPrediction();
        }

        private bool RaycastTimeTravelPrediction()
        {
            const float MinDist = 3.0f;

            bool overallHit = false;
            pvsInstanceID = null;

            TachyoidGameState tState = (TachyoidGameState)state;

            Vector3 forward = camTransform.forward;
            Vector3 lockedForward = verticalLock ? Vector3.ProjectOnPlane(camTransform.forward, Vector3.up) : forward;
            float forwardScale = verticalLock ? lockedForward.magnitude : 1;
            if (forwardScale == 0)
            {
                legalTimeTravel = false;
                return false;
            }

            lockedForward /= forwardScale;

            legalTimeTravel = true;
            reticle.ShowYes(transform.position, forward);
            timeTravelPosDif = Vector3.zero;
            timeTravelTimeDif = 0.0f;
            tState.WorldTimePrediction = tState.WorldTimeBeforeReverse;
            tState.UndoTimeReversalPrediction();

            Vector3 upComp = Vector3.Project(forward, Vector3.up);
            RaycastHit hitInfo;
            //Ignore the "Ignore Raycast" layer:
            LayerMask layerMask = 1 << 2;
            //Ignore the "Time Reversible" layer:
            layerMask = layerMask | (1 << LayerMask.NameToLayer("Time Reversal Ignore"));
            //LayerMask gravityMask = 1 << LayerMask.NameToLayer("AdditiveGravity");
            float limit = maxTimeTravelDistance * forwardScale;
            string tagHit;
            float distance;
            float partDistance;

            bool didHit = Physics.Raycast(state.playerTransform.position, forward, out hitInfo, limit, ~layerMask.value);

            if (didHit)
            {
                distance = forwardScale * hitInfo.distance;
                tagHit = hitInfo.collider.gameObject.tag;
                if (tagHit != "ParadoxSphere") overallHit = true;

                if (distance >= MinDist)
                {
                    partDistance = Mathf.Min(distance * 0.99f, distance - MinDist + 0.5f);
                    timeTravelPosDif += partDistance * lockedForward;
                    timeTravelTimeDif = timeTravelPosDif.magnitude / state.SpeedOfLight;

                    reticle.ShowYes(transform.position + timeTravelPosDif, lockedForward);
                    tState.WorldTimePrediction = tState.WorldTimeBeforeReverse - timeTravelTimeDif;
                    tState.UpdateOtherTimeReversibleObjects();
                }
            }
            else
            {
                timeTravelPosDif += limit * lockedForward;
                timeTravelTimeDif = timeTravelPosDif.magnitude / state.SpeedOfLight;

                reticle.ShowYes(transform.position + timeTravelPosDif, lockedForward);
                tState.WorldTimePrediction = tState.WorldTimeBeforeReverse - timeTravelTimeDif;
                tState.UpdateOtherTimeReversibleObjects();
            }

            float carriedLimit = timeTravelPosDif.magnitude;
            ParadoxSphere[] paradoxSpheres = FindObjectsOfType<ParadoxSphere>();
            float a, b, c, d, t1, t2, loopCount, r;
            bool legalTestPoint;
            Vector3 testPoint;
            foreach (ParadoxSphere sphere in paradoxSpheres)
            {
                if (!sphere.isBlockingTRev)
                {
                    continue;
                }

                loopCount = 0;
                limit = carriedLimit;
                do
                {
                    timeTravelPosDif = limit * lockedForward;
                    timeTravelTimeDif = timeTravelPosDif.magnitude / state.SpeedOfLight;
                    testPoint = transform.position + timeTravelPosDif;
                    r = sphere.GetRadius(tState.WorldTimeBeforeReverse - timeTravelTimeDif, transform.position, testPoint);
                    a = limit * limit;
                    b = 2.0f * (Vector3.Dot(transform.position, timeTravelPosDif) - Vector3.Dot(timeTravelPosDif, sphere.transform.position)); 
                    c = (transform.position.sqrMagnitude + sphere.transform.position.sqrMagnitude) - 2.0f * Vector3.Dot(transform.position, sphere.transform.position) - r * r;

                    d = b * b - 4.0f * a * c;
                    if (d >= 0)
                    {
                        d = Mathf.Sqrt(d);
                        t1 = (-b + d) / (2.0f * a);
                        t2 = (-b - d) / (2.0f * a);

                        if ((t1 < 1.0f && t2 > 1.0f) || (t2 < 1.0f && t1 > 1.0f))
                        {
                            legalTestPoint = false;
                        }
                        else
                        {
                            legalTestPoint = true;
                        }
                    }
                    else
                    {
                        legalTestPoint = true;
                    }

                    loopCount++;
                    if (!legalTestPoint)
                    {
                        limit = carriedLimit * (10 - loopCount) / 10.0f;
                    }
                } while (!legalTestPoint && (loopCount < 10) && (limit >= minTimeTravelDistance));

                carriedLimit = limit;
                timeTravelPosDif = limit * lockedForward;
                timeTravelTimeDif = timeTravelPosDif.magnitude / state.SpeedOfLight;
                reticle.ShowYes(transform.position + timeTravelPosDif, lockedForward);
                tState.WorldTimePrediction = tState.WorldTimeBeforeReverse - timeTravelTimeDif;
                tState.UpdateOtherTimeReversibleObjects();

                Vector3 closestPoint = sphere.myCollider.ClosestPoint(state.playerTransform.position);

                if (!legalTestPoint || (carriedLimit < minTimeTravelDistance) || (state.playerTransform.position == closestPoint))
                {
                    legalTimeTravel = false;
                    break;
                }
            }

            lockedForward = (lockedForward - upComp).normalized;
            if (legalTimeTravel)
            {
                reticle.ShowYes(transform.position + timeTravelPosDif, lockedForward);
            }
            else
            {
                reticle.ShowNo(transform.position + timeTravelPosDif, lockedForward);
            }

            return overallHit;
        }

        private class GraspCheck
        {
            public float sqrDistance { get; set; }
            public float angle { get; set; }
            public Collider obj { get; set; }
        }

        private Collider CheckIfGraspableNear()
        {
            List<Collider> nearbyColls = new List<Collider>();
            nearbyColls.AddRange(Physics.OverlapSphere(transform.position, graspRadius));
            List<GraspCheck> gcs = new List<GraspCheck>();
            int i = 0;
            while (i < nearbyColls.Count)
            {
                if (nearbyColls[i].GetComponent<Graspable>() != null)
                {
                    Transform pT = playerHeadTransform;
                    Transform cT = nearbyColls[i].transform;
                    float angle = Vector3.Angle(pT.forward, cT.position - pT.position);
                    if (angle < graspAngle)
                    {
                        gcs.Add(new GraspCheck()
                        {
                            sqrDistance = (cT.position - pT.position).sqrMagnitude,
                            angle = angle,
                            obj = nearbyColls[i]
                        });
                    }
                }
                i++;
            }
            //TODO: order list by distance and angle before picking
            if (gcs.Count > 0)
            {
                return gcs[0].obj;
            }
            else
            {
                return null;
            }
        }

        private void ResetGrasp()
        {
            graspTimer = 0.0f;
            itemWasInHand = null;
        }
        #endregion

        #region UI
        private const float fadeTime = 1.0f;
        public void FadeInUI()
        {
            isMenuUIOn = true;
            StopCoroutine("FadeTextToZeroAlpha");
            StartCoroutine("FadeTextToFullAlpha");
        }

        public void FadeOutUI()
        {
            isMenuUIOn = false;
            StopCoroutine("FadeTextToFullAlpha");
            StartCoroutine("FadeTextToZeroAlpha");
        }

        public void DeactivateMenus()
        {
            if (selectMenu != null) {
                selectMenu.SetActive(false);
            }
            if (menuParents != null)
            {
                for (int i = 0; i < menuParents.Count; i++)
                {
                    menuParents[i].SetActive(false);
                }
            }

            if (schwarzschildLens != null) schwarzschildLens.enabled = true;
            if (schwarzschildMirror != null) schwarzschildMirror.gameObject.SetActive(true);
        }

        public void ActivateMenus()
        {
            selectMenu.SetActive(true);
            menuReticle.SetActive(true);
            if (schwarzschildLens != null) schwarzschildLens.enabled = false;
            if (schwarzschildMirror != null) schwarzschildMirror.gameObject.SetActive(false);
        }

        public IEnumerator FadeTextToFullAlpha()
        {
            menuUI.color = new Color(menuUI.color.r, menuUI.color.g, menuUI.color.b, 0);
            while (menuUI.color.a < 1.0f)
            {
                menuUI.color = new Color(menuUI.color.r, menuUI.color.g, menuUI.color.b, menuUI.color.a + (Time.deltaTime / fadeTime));
                yield return null;
            }
        }

        public IEnumerator FadeTextToZeroAlpha()
        {
            menuUI.color = new Color(menuUI.color.r, menuUI.color.g, menuUI.color.b, 1);
            while (menuUI.color.a > 0.0f)
            {
                menuUI.color = new Color(menuUI.color.r, menuUI.color.g, menuUI.color.b, menuUI.color.a - (Time.deltaTime / fadeTime));
                yield return null;
            }
        }
        #endregion
    }
}