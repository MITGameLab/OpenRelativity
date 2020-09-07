using OpenRelativity.Objects;
using UnityEngine;

namespace Qrack
{
    public class TeleportBob : RealTimeQasmProgram
    {
        public TeleportEve Eve;
        public TeleportAlice Alice;

        public bool[] MeasurementResults = new bool[2];

        protected override void StartProgram()
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 4.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = QuantumSystem;
                    RelativisticObject ro = RelativisticObject;

                    if (MeasurementResults[0])
                    {
                        qs.Z(0);
                    }

                    if (MeasurementResults[1])
                    {
                        qs.X(0);
                    }

                    float prob = qs.Prob(0);
                    qs.H(0);
                    float hProb = qs.Prob(0);
                    qs.H(0);

                    HistoryPoints.Add(new QrackHistoryPoint
                    {
                        WorldTime = qs.LocalTime,
                        Action = (time) =>
                        {
                            ro.transform.eulerAngles = new Vector3(prob * 360.0f, hProb * 360.0f, 0.0f);
                            ro.riw = ro.transform.rotation;
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
                }
            });
        }

    }
}
