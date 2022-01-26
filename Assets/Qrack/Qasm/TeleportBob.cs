using OpenRelativity.Objects;
using UnityEngine;

namespace Qrack
{
    public class TeleportBob : RealTimeQasmProgram
    {
        public TeleportEve Eve;
        public TeleportAlice Alice;

        public QcClassicalChannel channelFromAlice;

        protected override void Update()
        {
            if (isSignalledSources.Count > 0)
            {
                isSignalledSources.Clear();
                ProgramInstructions.Clear();
                StartTriggeredProgram();
                ResetProgram();
            }

            base.Update();
        }

        protected override void StartProgram()
        {
            // Do not start on Awake().
        }

        protected void StartTriggeredProgram()
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 0.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = QuantumSystem;
                    RelativisticObject ro = RelativisticObject;

                    if (ClassicalBitRegisters[0])
                    {
                        qs.Z(0);
                    }

                    if (ClassicalBitRegisters[1])
                    {
                        qs.X(0);
                    }

                    BlochSphereCoordinates coords = qs.Prob3Axis(0);

                    HistoryPoints.Add(new RealTimeQasmProgramHistoryPoint
                    {
                        WorldTime = qs.VisualTime,
                        Action = (time) =>
                        {
                            ro.transform.eulerAngles = new Vector3((float)(coords.x * 360.0), (float)(coords.y * 360.0), (float)(coords.z * 360.0));
                            ro.riw = qs.transform.rotation;
                        }
                    });
                }
            });

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    Eve.ResetProgram();
                    Alice.ResetProgram();
                    ProgramInstructions.Clear();
                }
            });
        }

    }
}
