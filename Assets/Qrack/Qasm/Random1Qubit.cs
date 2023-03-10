using OpenRelativity.Objects;
using System;
using UnityEngine;

namespace Qrack
{
    public class Random1Qubit : RealTimeQasmProgram
    {
        public float gateInterval = 0.25f;
        public float gateDelay = 0.25f;

        protected bool isGateAdj = false; 
        protected bool isGateActing = true;
        protected int gate = 0;
        protected float timer = 0.0f;

        protected void PickGate() {
            gate = (int)UnityEngine.Random.Range(0, 6);
            if (gate >= 6) {
                gate = 5;
            }
            if (gate >= 4) {
                isGateAdj = (UnityEngine.Random.Range(0, 2) < 1);
            }
        }

        protected void EvolveGate(float dTime, QuantumSystem qs) {
            if  (gate == 0) {
                qs.PowX(dTime, 0);
            } else if (gate == 1) {
                qs.PowY(dTime, 0);
            } else if (gate == 2) {
                qs.PowZ(dTime, 0);
            } else if (gate == 3) {
                qs.PowH(dTime, 0);
            } else if (gate == 4) {
                if (isGateAdj) {
                    qs.PowZ(-dTime / 2, 0);
                } else {
                    qs.PowZ(dTime / 2, 0);
                }
            } else {
                if (isGateAdj) {
                    qs.PowZ(-dTime / 4, 0);
                } else {
                    qs.PowZ(dTime / 4, 0);
                }
            }
        }

        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            PickGate();

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                // Iterate every frame
                DeltaTime = 0,
                quantumProgramUpdate = (x, deltaTime) =>
                {
                    QuantumSystem qs = QuantumSystem;

                    float dTime = deltaTime;
                    timer += deltaTime;
                    if (isGateActing && (timer >= gateInterval)) {
                        EvolveGate(gateInterval + deltaTime - timer, qs);
                        isGateActing = false;
                        timer -= gateInterval;
                    } else if (!isGateActing && (timer >= gateDelay)) {
                        PickGate();
                        isGateActing = true;
                        timer -= gateDelay;
                        dTime = timer;
                    }

                    if (isGateActing) {
                        EvolveGate(dTime, qs);
                    }

                    BlochSphereCoordinates coords = qs.Prob3Axis(0);

                    HistoryPoints.Add(new RealTimeQasmProgramHistoryPoint
                    {
                        WorldTime = qs.VisualTime,
                        Action = (time) =>
                        {
                            RelativisticObject ro = RelativisticObject;
                            ro.transform.eulerAngles = new Vector3((float)coords.inclination * Mathf.Rad2Deg, (float)coords.azimuth * Mathf.Rad2Deg, 0);
                            ro.riw = qs.transform.rotation;
                            ro.localScale = new Vector3((float)coords.r, (float)coords.r, (float)coords.r);
                        }
                    });
                }
            });
        }

    }
}
