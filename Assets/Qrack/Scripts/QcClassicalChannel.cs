#if OPEN_RELATIVITY_INCLUDED
using UnityEngine;
using System.Collections.Generic;
using OpenRelativity;
using OpenRelativity.Objects;

namespace Qrack
{
    public class QcClassicalChannel : RelativisticBehavior
    {
        public ActionIndicator visualUnitPrefab;
        private List<ActionIndicator> visualUnitsActive;
        private List<ActionIndicator> visualUnitsFree;
        private Dictionary<int, QcClassicalSignal> transmittingSignals;

        public RealTimeQasmProgram source;
        public RealTimeQasmProgram destination;
        public float emissionInterval = 1;
        public float historyLength = 50;

        private Vector3 oldCameraPos;

        public class QcClassicalSignal
        {
            public bool bit { get; set; }
            public int destination { get; set; }
        }

        // Use this for initialization
        protected void Start()
        {
            visualUnitsActive = new List<ActionIndicator>();
            visualUnitsFree = new List<ActionIndicator>();
            transmittingSignals = new Dictionary<int, QcClassicalSignal>();

            oldCameraPos = state.playerTransform.position;
        }

        public void EmitBit(int sourceIndex, int destIndex)
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
            transmittingSignals[sourceIndex] = (new QcClassicalSignal { bit = source.ClassicalBitRegisters[sourceIndex], destination = destIndex });
            emittedVU.SetState(true);
            emittedVU.transform.parent = transform;
            emittedVU.transform.position = source.RelativisticObject.opticalPiw;
        }

        public void FixedUpdate()
        {
            if (!state.isMovementFrozen)
            {
                TranslateSignals(state.FixedDeltaTimeWorld);
            }
        }

        private void TranslateSignals(float deltaWordTime)
        {
            Vector3 cameraPos = state.playerTransform.position;

            float cameraDispChange;
            float gtt;
            Vector3 destPos = destination.RelativisticObject.opticalPiw;
            Vector3 vuPos, dispUnit, velUnit;
            float perspectiveFactor;
            int vuIndex = 0;
            int signalIndex = 0;
            while (vuIndex < visualUnitsActive.Count) {
                ActionIndicator vu = visualUnitsActive[vuIndex];
                RelativisticObject vuRO = vu.GetComponent<RelativisticObject>();
                vuPos = vu.transform.position;
                dispUnit = (destPos - vuPos).normalized;
                velUnit = state.SpeedOfLight * dispUnit;
                perspectiveFactor = Mathf.Pow(2, Vector3.Dot((cameraPos - vuPos).normalized, dispUnit));
                cameraDispChange = (oldCameraPos - vuPos).magnitude - (cameraPos - vuPos).magnitude;
                gtt = vuRO.GetTimeFactor();
                Vector3 disp = (gtt * deltaWordTime * velUnit + cameraDispChange * dispUnit) * perspectiveFactor;
                if (disp.sqrMagnitude > (destPos - vuPos).sqrMagnitude)
                {
                    visualUnitsActive.Remove(vu);
                    QcClassicalSignal signal = transmittingSignals[signalIndex];
                    transmittingSignals.Remove(signalIndex);
                    destination.ClassicalBitRegisters[signal.destination] = signal.bit;
                    destination.isSignalledSources.Add(this);
                    visualUnitsFree.Add(vu);
                    vu.SetState(false);
                }
                else
                {
                    vu.transform.position = vuPos + disp;
                    vuRO.piw = vu.transform.position;
                    ++vuIndex;
                }
                ++signalIndex;
            }
            oldCameraPos = cameraPos;
        }
    }
}
#endif