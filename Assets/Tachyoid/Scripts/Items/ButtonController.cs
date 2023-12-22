using UnityEngine;
using System.Collections.Generic;
using OpenRelativity;
using OpenRelativity.Objects;
using Tachyoid.TimeReversal.Generic;
using Tachyoid.TimeReversal.HistoryPoints;
using System;

namespace Tachyoid.Objects
{
    public class ButtonController : TimeReversibleObject<ButtonHistoryPoint>
    {
        protected override bool IsUpdatedPreview() { return true; }

        public Transform visualButton;
        public DoorController triggerReceiver;
        public ActionIndicatorRecycler actionIndicator;
        public bool fasterThanLight = false;
        public float upHeight = -0.006f;
        public float downHeight = -0.009f;
        public float depressTime = 0.2f;
        private float depressTimer;
        private ButtonState pressState;

        private List<GameObject> _gameObjectsOnButton;
        private List<GameObject> gameObjectsOnButton
        {
            get
            {
                if (_gameObjectsOnButton == null)
                {
                    _gameObjectsOnButton = new List<GameObject>();
                }
                return _gameObjectsOnButton;
            }
            
            set
            {
                _gameObjectsOnButton = value;
            }
        }

        //private float timeWindowError = 0.05f;

        private float lastActionTime;
        private bool _ignoreExit;
        private bool ignoreExit { get { return _ignoreExit; } set { waitForIgnoreExit = value; _ignoreExit = value; } }
        private bool waitForIgnoreExit;

        private AudioSource btnSound;
        private AudioSource btnSoundReverse;

        //private GameObject exitingTriggerGO;

        // Use this for initialization
        protected override void Start()
        {
            base.Start();

            pressState = ButtonState.PressResolved;
            depressTimer = 0.0f;
            history = new List<ButtonHistoryPoint>();

            AudioSource[] sounds = GetComponents<AudioSource>();
            if (sounds.Length >= 2)
            {
                btnSound = sounds[0];
                btnSoundReverse = sounds[1];
            }

            ignoreExit = false;
        }

        private void NormalTimeUpdate()
        {
            float doppler = myRO.GetTisw();
            float endTime = state.TotalTimeWorld + myRO.localTimeOffset + doppler;

            List<ButtonHistoryPoint> actions = GetHistoryBetweenTimes(lastActionTime, endTime);
            for (int i = 0; i < actions.Count; i++)
            {
                ButtonHistoryPoint action = actions[i];
                lastActionTime = action.WorldTime;
                bool doOverride = !(action.State == ButtonState.PressHeld
                    && pressState != ButtonState.PressResolved);
                if (pressState != action.State && doOverride)
                {
                    pressState = action.State;
                    if (i < actions.Count - 1)
                    {
                        depressTimer = depressTime - (actions[i + 1].WorldTime - action.WorldTime);
                    }
                    else
                    {
                        depressTimer = depressTime - (endTime - action.WorldTime);
                    }

                    if (depressTimer < 0.0f)
                    {
                        depressTimer = 0.0f;
                    }
                }
            }

            if (pressState == ButtonState.WaitToEndPress)
            {
                pressState = ButtonState.EndPress;
            }
            else if (pressState == ButtonState.EndPress)
            {
                float time = state.WorldTimeBeforeReverse + myRO.localTimeOffset + doppler;
                TriggerExit();
                history.Add(new ButtonHistoryPoint()
                {
                    State = ButtonState.PressResolved,
                    WorldTime = time
                });
                pressState = ButtonState.PressResolved;
                history.Sort((x, y) => x.WorldTime.CompareTo(y.WorldTime));
                triggerReceiver.TriggerExit(this, fasterThanLight);
            }

            if (actionIndicator != null)
            {
                if (pressState == ButtonState.Pressing
                    || pressState == ButtonState.PressHeld
                    || pressState == ButtonState.WaitToEndPress)
                {
                    actionIndicator.SetState(true);
                }
                else
                {
                    actionIndicator.SetState(false);
                }
            }

            lastActionTime = endTime;
        }

        protected override void ForwardTimeUpdate(float startTime, float endTime)
        {
            if (!state.isMovementFrozen)
            {
                NormalTimeUpdate();
                return;
            }

            List<ButtonHistoryPoint> actions = GetHistoryBetweenTimes(startTime, endTime);
            for (int i = 0; i < actions.Count; i++)
            {
                ButtonHistoryPoint action = actions[i];
                lastActionTime = action.WorldTime;
                bool doOverride = !(action.State == ButtonState.PressHeld
                    && pressState != ButtonState.PressResolved);
                if (pressState != action.State && doOverride)
                {
                    pressState = action.State;
                    if (i < actions.Count - 1)
                    {
                        depressTimer = depressTime - (actions[i + 1].WorldTime - action.WorldTime);
                    }
                    else
                    {
                        depressTimer = depressTime - (endTime - action.WorldTime);
                    }
                    if (depressTimer < 0.0f)
                    {
                        depressTimer = 0.0f;
                    }
                }
            }

            if (depressTimer > 0.0f)
            {
                depressTimer -= (endTime - startTime);
                if (depressTimer < 0.0f)
                {
                    depressTimer = 0.0f;
                }
            }

            UpdatePosition();
        }

        protected override void ReverseTimeUpdate(float startTime, float endTime)
        {
            List<ButtonHistoryPoint> actions = GetHistoryBetweenTimes(startTime, endTime);
            for (int i = 0; i < actions.Count; i++)
            {
                float origWorldTime = actions[i].WorldTime;
                ButtonState origState = actions[i].State;
                ButtonHistoryPoint action = GetLastPointBefore(origWorldTime);
                if (action == null)
                {
                    pressState = ButtonState.PressResolved;
                    depressTimer = 0.0f;
                    lastActionTime = startTime;
                }
                else
                {
                    lastActionTime = action.WorldTime;
                    bool doOverride = !(origState == ButtonState.PressHeld
                        && action.State != ButtonState.PressResolved);
                    if (pressState != action.State && doOverride)
                    {
                        if (action.State == ButtonState.PressHeld || action.State == ButtonState.Pressing)
                        {
                            if (btnSound != null && !btnSound.isPlaying)
                            {
                                btnSound.Play();
                            }
                        }
                        else if (action.State == ButtonState.EndPress || action.State == ButtonState.PressResolved)
                        {
                            if (btnSoundReverse != null && !btnSoundReverse.isPlaying)
                            {
                                btnSoundReverse.Play();
                            }
                        }
                        pressState = action.State;
                        depressTimer = depressTimer - depressTime - (origWorldTime - action.WorldTime);
                        if (depressTimer < 0.0f)
                        {
                            depressTimer = 0.0f;
                        }
                    }
                }
            }
            if (depressTimer < 2.0f && depressTimer > 0.0f)
            {
                depressTimer += (endTime - startTime);
                if (depressTimer > 2.0f)
                {
                    depressTimer = 0.0f;
                }
            }
            UpdatePosition();
        }

        public void TriggerExit()
        {
            if (btnSoundReverse != null && !btnSoundReverse.isPlaying)
            {
                btnSoundReverse.Play();
            }
            depressTimer = depressTime - depressTimer;
            pressState = ButtonState.PressResolved;
        }

        private void UpdatePosition()
        {
            if (pressState == ButtonState.Pressing || pressState == ButtonState.PressHeld)
            {
                Vector3 position = visualButton.localPosition;
                position = new Vector3(position.x, position.y, (upHeight - downHeight) * depressTimer / depressTime + downHeight);
                visualButton.localPosition = position;
            }
            else
            {
                Vector3 position = visualButton.localPosition;
                position = new Vector3(position.x, position.y, upHeight - (upHeight - downHeight) * depressTimer / depressTime);
                visualButton.localPosition = position;
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (ignoreExit)
            {
                if (waitForIgnoreExit)
                {
                    waitForIgnoreExit = false;
                }
                else
                {
                    ignoreExit = false;
                }
            }

            if (depressTimer > 0.0f)
            {
                depressTimer -= Time.fixedDeltaTime;
                if (depressTimer < 0.0f)
                {
                    depressTimer = 0.0f;
                }

                UpdatePosition();
            }

            gameObjectsOnButton.RemoveAll(q => q == null);
            if (gameObjectsOnButton.Count == 0
                && pressState != ButtonState.PressHeld
                && pressState != ButtonState.PressResolved)
            {
                pressState = ButtonState.EndPress;
            }
        }

        private bool CheckIfDupeTrigger(List<GameObject> triggers, GameObject actionTrigger)
        {
            int i = 0;
            bool anyTrigger = false;
            while (i < triggers.Count && !anyTrigger)
            {
                if (actionTrigger == triggers[i])
                {
                    anyTrigger = true;
                }
                i++;
            }

            return anyTrigger;
        }

        void OnTriggerEnter(Collider collider)
        {
            int layerID = collider.gameObject.layer;
            LayerMask layerMask = (1 << LayerMask.NameToLayer("No Button Trigger")) | (1 << LayerMask.NameToLayer("Paradox Sphere"));
            string tag = collider.gameObject.tag;
            if (((layerMask.value & (1 << layerID)) == 0) && (tag != "TRev;ImageManager"))
            {
                if (!CheckIfDupeTrigger(gameObjectsOnButton, collider.gameObject)) {
                    gameObjectsOnButton.Add(collider.gameObject);
                }
                if (pressState != ButtonState.PressHeld && pressState != ButtonState.Pressing)
                {
                    TriggerEnter();
                    float time = state.TotalTimeWorld + myRO.localTimeOffset + myRO.GetTisw();
                    history.Add(new ButtonHistoryPoint()
                    {
                        State = ButtonState.PressHeld,
                        WorldTime = time
                    });
                    history.Sort((x, y) => x.WorldTime.CompareTo(y.WorldTime));
                    if (triggerReceiver != null)
                    {
                        triggerReceiver.TriggerEnter(this, fasterThanLight);
                    }
                }
            }
        }

        public void TriggerEnter()
        {
            if (btnSound != null && !btnSound.isPlaying)
            {
                btnSound.Play();
            }
            depressTimer = depressTime - depressTimer;
            pressState = ButtonState.Pressing;
        }

        void OnTriggerStay(Collider collider)
        {
            int layerID = collider.gameObject.layer;
            LayerMask layerMask = (1 << LayerMask.NameToLayer("No Button Trigger")) | (1 << LayerMask.NameToLayer("Paradox Sphere"));
            string tag = collider.gameObject.tag;
            if (((layerMask.value & (1 << layerID)) == 0) && (tag != "TRev;ImageManager"))
            {
                pressState = ButtonState.Pressing;
            }
        }

        void OnTriggerExit(Collider collider)
        {
            gameObjectsOnButton.RemoveAll(go => go.GetInstanceID() == collider.gameObject.GetInstanceID());
            if (!ignoreExit 
                && gameObjectsOnButton.Count == 0
                //&& pressState != ButtonState.PressHeld
                && pressState != ButtonState.PressResolved)
            {
                pressState = ButtonState.WaitToEndPress;
            }
        }

        public override void ReverseTime()
        {
            FTLPlayerController player = state.GetComponent<FTLPlayerController>();
            float doppler = SRelativityUtil.GetTisw(state.PlayerPositionBeforeReverse, Vector3.zero, Vector4.zero);
            if ((gameObjectsOnButton.Count == 1 || gameObjectsOnButton.Count == 2)
                && (CheckIfDupeTrigger(gameObjectsOnButton, player.gameObject)
                    || (player.itemInHand != null && CheckIfDupeTrigger(gameObjectsOnButton, player.itemInHand.gameObject))))
            {
                float time = state.WorldTimeBeforeReverse + myRO.localTimeOffset + doppler;
                triggerReceiver.TriggerExit(this, fasterThanLight, true);
                history.Add(new ButtonHistoryPoint()
                {
                    State = ButtonState.PressResolved,
                    WorldTime = time
                });
                history.Sort((x, y) => x.WorldTime.CompareTo(y.WorldTime));
                if (pressState != ButtonState.PressResolved)
                {
                    depressTimer = depressTime - depressTimer;
                }
                lastActionTime = Mathf.Min(time - 0.01f, lastActionTime);
                ignoreExit = true;
            }
            lastActionTime = Mathf.Min(lastActionTime, state.TotalTimeWorld + myRO.localTimeOffset + doppler);
        }

        public override void UpdateTimeTravelPrediction()
        {
        }

        public override void UndoTimeTravelPrediction()
        {

        }
    }
}
