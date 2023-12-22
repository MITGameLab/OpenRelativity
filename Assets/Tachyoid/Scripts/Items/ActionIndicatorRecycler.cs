using UnityEngine;
using System.Collections.Generic;
using System;
using OpenRelativity;
using OpenRelativity.Objects;
using Tachyoid.TimeReversal.Generic;
using Tachyoid.TimeReversal.HistoryPoints;

namespace Tachyoid.Objects
{
    public class ActionIndicatorRecycler : TimeReversibleObject<ActionIndicatorRecyclerHistoryPoint>
    {
        public ActionIndicator visualUnitPrefab;
        private List<ActionIndicator> visualUnitsActive;
        private List<ActionIndicator> visualUnitsFree;
        public GameObject source;
        public GameObject destination;
        public float emissionInterval = 1.0f;
        public float historyLength = 50.0f;

        private Vector3 oldCameraPos;
        //private Vector3 oldCameraVel;

        //On for emitting,
        // off for not.
        private bool isEmitting;

        private float emissionTimer;

        private bool tRevPickupFrame;

        //private float pathTimeLength;
        //private float edgeTimeLength;

        private List<IndicatorState> origState;
        private float origTimer;
        private bool wasAging;

        protected override bool IsUpdatedPreview()
        {
            return true;
        }

        // Use this for initialization
        protected override void Start()
        {
            base.Start();

            wasAging = true;
            isEmitting = false;
            visualUnitsActive = new List<ActionIndicator>();
            visualUnitsFree = new List<ActionIndicator>();
            emissionTimer = 0.0f;

            oldCameraPos = state.playerTransform.position;
            tRevPickupFrame = false;
        }

        // Update is called once per frame
        protected override void FixedUpdate()
        {
            if (tRevPickupFrame)
            {
                //RestoreState();
                tRevPickupFrame = false;
                wasAging = true;

                return;
            }

            if (state.isMovementFrozen)
            {
                if (wasAging)
                {
                    wasAging = false;
                    SaveState();
                }
                else
                {
                    RestoreState();
                }

                return;
            }

            base.FixedUpdate();
        }

        protected override void ReverseTimeUpdate(float earlyTime, float lateTime)
        {
            UpdateForwardOrBackward(lateTime, earlyTime);
        }
        protected override void ForwardTimeUpdate(float earlyTime, float lateTime)
        {
            UpdateForwardOrBackward(earlyTime, lateTime);
        }

        private void UpdateForwardOrBackward(float lastTime, float time)
        {
            float fixedDeltaTimePlayer = state.FixedDeltaTimePlayer;
            float newOpticalTime = time;
            float fixedDeltaWorldTime = time - lastTime;

            lastOpticalTime = newOpticalTime;
            tRevPickupFrame = false;
            wasAging = true;

            if (fixedDeltaTimePlayer == 0.0)
            {
                return;
            }

            TranslateVisualUnits(fixedDeltaTimePlayer);

            Vector3 sourcePos = source.transform.position;
            Vector3 destPos = destination.transform.position;
            int oldTimerMod = (int)Math.Floor(emissionTimer / emissionInterval);
            emissionTimer -= fixedDeltaWorldTime;
            int newTimerMod = (int)Math.Floor(emissionTimer / emissionInterval);
            int lcv;
            if (history.Count == 0)
            {
                while (emissionTimer <= 0.0f) emissionTimer += emissionInterval;
                while (emissionTimer > emissionInterval) emissionTimer -= emissionInterval;
            }
            else if (isEmitting && oldTimerMod > newTimerMod)
            {
                float emissionExcess = emissionTimer - oldTimerMod * emissionInterval;
                lcv = 0;
                float doppler = (source.transform.position.WorldToOptical(Vector3.zero, Vector3.zero)
                    - state.playerTransform.position).magnitude / state.SpeedOfLight;
                float endTime = state.TotalTimeWorld - doppler;
                while ((lcv < history.Count) && (history[lcv].WorldTime < endTime))
                {
                    lcv++;
                }
                if ( (lcv < history.Count && history[lcv].State)
                    || (emissionTimer <= 0.0f && history[history.Count - 1].State)
                    || (emissionTimer > 0.0f && !(history[history.Count - 1].State)) )
                {
                    ActionIndicator emittedVU;
                    if (visualUnitsFree.Count > 0)
                    {
                        emittedVU = visualUnitsFree[0];
                        visualUnitsFree.RemoveAt(0);
                    }
                    else
                    {
                        emittedVU = Instantiate(visualUnitPrefab);
                    }
                    visualUnitsActive.Add(emittedVU);
                    emittedVU.SetState(true);
                    emittedVU.transform.parent = this.transform;
                    emittedVU.transform.position = ((emissionExcess * state.SpeedOfLight) * (sourcePos - destPos).normalized) + sourcePos;
                }
                if (emissionTimer <= 0.0f) emissionTimer += emissionInterval;
            }
        }

        private void TranslateVisualUnits(float deltaPlayerTime)
        {
            Vector3 cameraPos = state.playerTransform.position;

            float cameraDispChange;
            float gtt;
            Vector3 destPos = destination.transform.position;
            Vector3 vuPos, dispUnit, velUnit;
            float perspectiveFactor;
            int vuIndex = 0;
            while (vuIndex < visualUnitsActive.Count) {
                ActionIndicator vu = visualUnitsActive[vuIndex];
                RelativisticObject vuRO = vu.GetComponent<RelativisticObject>();
                vuPos = vu.transform.position;
                dispUnit = (destPos - vuPos).normalized;
                velUnit = state.SpeedOfLight * dispUnit;
                perspectiveFactor = Mathf.Pow(2, Vector3.Dot((cameraPos - vuPos).normalized, dispUnit));
                cameraDispChange = (oldCameraPos - vuPos).magnitude - (cameraPos - vuPos).magnitude;
                gtt = vuRO.GetTimeFactor();
                Vector3 disp = ((gtt * deltaPlayerTime) * velUnit + cameraDispChange * dispUnit) * perspectiveFactor;
                if (disp.sqrMagnitude > (destPos - vuPos).sqrMagnitude)
                {
                    visualUnitsActive.Remove(vu);
                    visualUnitsFree.Add(vu);
                    vu.SetState(false);
                }
                else
                {
                    vu.transform.position = vuPos + disp;
                    vuRO.piw = vu.transform.position;
                    vuIndex++;
                }
                
            }

            oldCameraPos = cameraPos;
        }

        public void SetState(bool onOrOff)
        {
            if (isEmitting != onOrOff)
            {
                float doppler = (source.transform.position.WorldToOptical(Vector3.zero, Vector3.zero)
                    - state.playerTransform.position).magnitude / state.SpeedOfLight;
                history.Add(new ActionIndicatorRecyclerHistoryPoint()
                {
                    WorldTime = state.TotalTimeWorld - doppler,
                    State = onOrOff
                });
            }   
            isEmitting = onOrOff;
        }

        public override void UpdateTimeTravelPrediction()
        {
        }

        public override void ReverseTime()
        {
            tRevPickupFrame = false;
        }

        public override void UndoTimeTravelPrediction()
        {

        }

        private class IndicatorState
        {
            public Vector3 position { get; set; }
        }

        private void SaveState()
        {
            origTimer = emissionTimer;

            origState = new List<IndicatorState>();
            foreach (ActionIndicator visualUnit in visualUnitsActive)
            {
                origState.Add(new IndicatorState()
                {
                    position = visualUnit.transform.position
                });
            }
        }
        private void RestoreState()
        {
            emissionTimer = origTimer;

            int i = 0;
            foreach (IndicatorState vuState in origState)
            {
                if (i < visualUnitsActive.Count)
                {
                    ActionIndicator vu = visualUnitsActive[i];
                    vu.transform.position = vuState.position;
                    
                }
                else if (i - visualUnitsActive.Count < visualUnitsFree.Count)
                {
                    ActionIndicator vu = visualUnitsFree[i - visualUnitsActive.Count];
                    vu.SetState(true);
                    vu.transform.position = vuState.position;
                    visualUnitsFree.RemoveAt(i - visualUnitsActive.Count);
                    visualUnitsActive.Add(vu);
                }
                else
                {
                    ActionIndicator vu = Instantiate(visualUnitPrefab);
                    visualUnitsActive.Add(vu);
                    vu.SetState(true);
                    vu.transform.parent = this.transform;
                    vu.transform.position = vuState.position;
                }
                i++;
            }

            while (i < visualUnitsActive.Count)
            {
                ActionIndicator vu = visualUnitsActive[i];
                visualUnitsFree.Add(vu);
                visualUnitsActive.RemoveAt(i);
                vu.SetState(false);
            }
        }
    }
}
