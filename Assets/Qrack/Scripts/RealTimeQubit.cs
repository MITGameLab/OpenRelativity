using System;
using OpenRelativity.Objects;
using UnityEngine;

namespace Qrack
{
    public class RealTimeQubit : RealTimeQasmProgram
    {
        public float gateTime = 1;
        const int frameCount = 60;

        protected override void StartProgram()
        {
            // Do not start on Awake().
            X(0);
        }

        protected void ContinuousGate(Action<QuantumSystem> gate)
        {
            bool doReset = ProgramInstructions.Count > 0;

            for (int i = 0; i < frameCount; ++i)
            {
                ProgramInstructions.Add(new RealTimeQasmInstruction()
                {
                    DeltaTime = gateTime / frameCount,
                    quantumProgramUpdate = (x, y) =>
                    {
                        QuantumSystem qs = QuantumSystem;

                        gate(qs);

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

            if (doReset)
            {
                ResetProgram();
            }
        }

        protected void X(uint q)
        {
            ContinuousGate(qs => qs.RY(q, 2 * Mathf.PI * gateTime / frameCount));
        }

        protected void Y(uint q)
        {
            ContinuousGate(qs => qs.RX(q, 2 * Mathf.PI * gateTime / frameCount));
        }

        protected void Z(uint q)
        {
            ContinuousGate(qs => qs.RX(q, 2 * Mathf.PI * gateTime / frameCount));
        }

        protected void S(uint q)
        {
            ContinuousGate(qs => qs.RX(q, Mathf.PI * gateTime / frameCount));
        }

        protected void AdjS(uint q)
        {
            ContinuousGate(qs => qs.RX(q, -Mathf.PI * gateTime / frameCount));
        }
    }
}
