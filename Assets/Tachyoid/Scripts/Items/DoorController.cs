using UnityEngine;
using System.Collections.Generic;
using OpenRelativity.Objects;
using Tachyoid.TimeReversal.Generic;
using Tachyoid.TimeReversal.HistoryPoints;

namespace Tachyoid.Objects
{
    public class DoorController : TimeReversibleObject<DoorHistoryPoint>, ButtonTriggerReceiver
    {
        protected override bool IsUpdatedPreview() { return false; }

        private Transform triggerDestination;

        public int triggerCount = 1;
        public float upDistance = 12.6f;
        private float downPosition;
        private float upHeight;
        private float downHeight;
        public float openTime = 0.3f;

        private float openTimer;
        public bool isOpen { get; private set; }
        private bool isOpening;

        private List<ButtonController> triggers;

        private AudioSource doorSound;
        private AudioSource doorSoundReverse;

        private bool stuck;

        public DoorLockIndicator[] lockIndicators = new DoorLockIndicator[1];
        public ButtonController[] lockIndicatorTriggers = new ButtonController[1];

        // Use this for initialization
        protected override void Start()
        {
            base.Start();

            downPosition = transform.localPosition.y;
            isOpening = false;
            openTimer = 0.0f;
            history = new List<DoorHistoryPoint>();
            triggers = new List<ButtonController>();
            stuck = false;
            downHeight = transform.localPosition.y;
            upHeight = downHeight + upDistance;
            AudioSource[] sounds = GetComponents<AudioSource>();
            if (sounds.Length >= 2)
            {
                doorSound = sounds[0];
                doorSoundReverse = sounds[1];
            }
            lastOpticalTime = state.TotalTimeWorld + myRO.localTimeOffset + myRO.GetTisw();

            triggerDestination = transform.parent;
        }

        public override void UpdateTimeTravelPrediction()
        {
            ////If player is not aging:
            //float newDoppler = (state.playerTransform.position - ((Vector4)(transform.position)).WorldToOptical(Vector3.zero, state.playerTransform.position, state.PlayerVelocityVector)).magnitude / state.SpeedOfLight;
            //float endTime = state.TotalTimeWorld - newDoppler;
            //float startTime = lastTime;

            //if (startTime > endTime)
            //{
            //    ReverseTimeUpdate(endTime, startTime);
            //    UpdatePosition();
            //}
            //else if (endTime > startTime)
            //{
            //    ForwardTimeUpdate(startTime, endTime);
            //    UpdatePosition();
            //}

            //lastTime = endTime;
        }

        private void TriggerIndicator(ButtonController trigger, bool locked)
        {
            int instanceId = trigger.GetInstanceID();
            int i = 0;
            bool foundTrigger = false;
            while (!foundTrigger && i < lockIndicatorTriggers.Length)
            {
                if (lockIndicators[i] != null && lockIndicatorTriggers[i].GetInstanceID() == instanceId)
                {
                    lockIndicators[i].SetLockedState(locked);
                    foundTrigger = true;
                }
                i++;
            }
        }

        private bool CheckIfDupeTrigger(List<ButtonController> triggers, ButtonController actionTrigger)
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

        private float ClampRange(float val, float min, float max)
        {
            if (val < min)
            {
                val = min;
            }
            else if (val > max)
            {
                val = max;
            }

            return val;
        }

        protected override void ForwardTimeUpdate(float startTime, float endTime)
        {
            List<DoorHistoryPoint> actions = GetHistoryBetweenTimes(startTime, endTime);
            DoorHistoryPoint action = null;
            for (int i = 0; i < actions.Count; i++)
            {
                action = actions[i];
                if (action.Opening && !CheckIfDupeTrigger(triggers, action.Trigger))
                {
                    triggers.Add(action.Trigger);
                    TriggerIndicator(action.Trigger, false);
                }
                else if (!action.Opening && CheckIfDupeTrigger(triggers, action.Trigger))
                {
                    triggers.Remove(action.Trigger);
                    TriggerIndicator(action.Trigger, true);
                }
            }
            bool oldOpening = isOpening;
            if (triggers.Count >= triggerCount)
            {
                isOpening = true;
                isOpen = true;
                
            }
            else if (triggers.Count < triggerCount)
            {
                isOpening = false;
            }
            if (oldOpening != isOpening)
            {
                if (action != null)
                {
                    openTimer = openTime - action.OpenTimer;
                    openTimer -= (endTime - action.WorldTime);
                }
                else
                {
                    openTimer -= (endTime - startTime);
                }

                if (!state.isMovementFrozen)
                {
                    if (isOpening)
                    {
                        if (doorSound != null && !doorSound.isPlaying)
                        {
                            doorSound.Play();
                        }
                    }
                    else
                    {
                        if (doorSoundReverse != null && !doorSoundReverse.isPlaying)
                        {
                            doorSoundReverse.Play();
                        }
                    }
                }
            }
            openTimer = ClampRange(openTimer, 0.0f, openTime);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (stuck)
            {
                return;
            }

            if (openTimer > 0.0f)
            {
                if (!state.isMovementFrozen)
                {
                    openTimer -= Time.fixedDeltaTime;
                    if (openTimer <= 0.0f)
                    {
                        openTimer = 0.0f;
                        if (!isOpening) isOpen = false;
                    }
                }
            }

            UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (isOpening)
            {
                Vector3 position = transform.localPosition;
                position.y = upHeight - upHeight * openTimer / openTime + downPosition;
                transform.localPosition = position;
                myRO.ResetPiw();
            }
            else
            {
                Vector3 position = transform.localPosition;
                position.y = upHeight * openTimer / openTime + downPosition;
                transform.localPosition = position;
                myRO.ResetPiw();
            }
        }

        public void TriggerEnter(ButtonController button, bool ftl, bool isReversingTime = false)
        {
            float delay = (button.transform.position - triggerDestination.position).magnitude / state.SpeedOfLight;
            //if (ftl)
            //{
            //    delay = -delay - openTime;
            //    lastTime = state.TotalTimeWorld + delay + 0.0001;
            //    if (!CheckIfDupeTrigger(triggers, button))
            //    {
            //        triggers.Add(button);
            //        TriggerIndicator(button, false);
            //    }
            //    CheckTriggers(time, openTimer, time);
            //}
            history.Add(new DoorHistoryPoint
            {
                WorldTime = state.TotalTimeWorld + myRO.localTimeOffset + delay,
                Trigger = button,
                Opening = true,
                OpenTimer = ClampRange(openTimer - delay, 0.0f, openTime)
            });
            history.Sort((x, y) => x.WorldTime.CompareTo(y.WorldTime));
            //if (ftl)
            //{
            //    ForwardTimeUpdate(lastTime, state.TotalTimeWorld);
            //}
        }

        public void TriggerStay(ButtonController button, bool ftl, bool isReversingTime = false)
        {
        }

        private DoorHistoryPoint GetFirstHistoryPointAfterOpen(float exitTime)
        {
            int i = 0;
            while (i < history.Count && history[i].WorldTime < exitTime)
            {
                i++;
            }

            DoorHistoryPoint firstAfterOpen = null;
            while (i < history.Count)
            {
                if (history[i].Opening)
                {
                    firstAfterOpen = history[i];
                }
                i++;
            }

            return firstAfterOpen;
        }

        public void TriggerExit(ButtonController button, bool ftl, bool isReversingTime = false)
        {
            float delay = (button.transform.position - triggerDestination.position).magnitude / state.SpeedOfLight;
            float worldTime;
            if (isReversingTime)
            {
                worldTime = state.WorldTimeBeforeReverse + myRO.localTimeOffset + delay;
            }
            else
            {
                worldTime = state.TotalTimeWorld + myRO.localTimeOffset + delay;
            }

            history.Add(new DoorHistoryPoint
            {
                WorldTime = state.TotalTimeWorld + myRO.localTimeOffset + delay,
                Trigger = button,
                Opening = false,
                OpenTimer = ClampRange(openTimer - delay, 0.0f, openTime)
            });

            //if (ftl)
            //{
            //    delay = -delay - openTime;
            //    float exitTime = time + delay;
            //    lastTime = time + delay + 0.0001f;
            //    HistoryPoint actionAfter = GetFirstHistoryPointAfterOpen(exitTime);
            //    if (actionAfter != null)
            //    {
            //        delay = actionAfter.worldTime + 0.001f - time;
            //    }
            //    if (CheckIfDupeTrigger(triggers, button))
            //    {
            //        triggers.Remove(button);
            //        TriggerIndicator(button, true);
            //    }
            //    if (!state.MovementFrozen)
            //    {
            //        CheckTriggers(time, openTimer, time);
            //    }
            //}

            history.Sort((x, y) => x.WorldTime.CompareTo(y.WorldTime));
        }

        public override void ReverseTime()
        {

        }

        public override void UndoTimeTravelPrediction()
        {

        }

        protected override void ReverseTimeUpdate(float startTime, float endTime)
        {
            List<DoorHistoryPoint> actions = GetReversedHistoryAfterTime(startTime);
            DoorHistoryPoint action = null;
            for (int i = 0; i < actions.Count; i++)
            {
                action = actions[i];
                if (action.Opening && CheckIfDupeTrigger(triggers, action.Trigger))
                {
                    triggers.Remove(action.Trigger);
                    TriggerIndicator(action.Trigger, true);
                }
                else if (!action.Opening && !CheckIfDupeTrigger(triggers, action.Trigger))
                {
                    triggers.Add(action.Trigger);
                    TriggerIndicator(action.Trigger, false);
                }
            }
            bool oldOpening = isOpening;
            if (triggers.Count >= triggerCount)
            {
                isOpening = true;
                isOpen = true;
            }
            else if (triggers.Count < triggerCount)
            {
                isOpening = false;
            }
            if (oldOpening != isOpening)
            {
                if (action != null)
                {
                    openTimer = action.OpenTimer;
                    if (openTimer != 0.0f)
                    {
                        openTimer += (action.WorldTime - startTime);
                    }
                }
                else if (openTimer != 0.0f)
                {
                    openTimer += (endTime - startTime);
                }
            }
            if (openTimer > openTime)
            {
                openTimer = 0.0f;
            }
        }

        void OnCollisionEnter(Collision coll)
        {
            if (openTimer > 0.0f && !isOpening) {
                stuck = true;
                isOpen = true;
            }
        }

        void OnCollisionStay(Collision coll)
        {
            if (openTimer > 0.0f && !isOpening)
            {
                stuck = true;
                isOpen = true;
            }
        }

        void OnCollisionExit(Collision coll)
        {
            stuck = false;
        }
    }
}