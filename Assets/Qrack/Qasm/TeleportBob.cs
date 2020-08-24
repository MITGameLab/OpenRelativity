using UnityEngine;

namespace Qrack
{
    public class TeleportBob : RealTimeQasmProgram
    {
        public TeleportEve Eve;

        public bool[] MeasurementResults = new bool[2];

        protected override void StartProgram()
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x) =>
                {
                    QuantumSystem qs = x.QuantumSystem;

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
                    qs.transform.localEulerAngles = new Vector3(prob * 360.0f, hProb * 360.0f, 0.0f);
                }
            });

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x) =>
                {
                    Eve.ResetProgram();
                    gameObject.SetActive(false);
                }
            });
        }

    }
}
