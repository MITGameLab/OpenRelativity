using UnityEngine;

namespace Qrack
{
    public class TeleportAlice : RealTimeQasmProgram
    {
        public TeleportBob Bob;

        protected override void StartProgram()
        {

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = x.QuantumSystem;
                    qs.Rand(0);

                    float prob = qs.Prob(0);
                    qs.H(0);
                    float hProb = qs.Prob(0);
                    qs.H(0);
                    qs.transform.localEulerAngles = new Vector3(prob * 360.0f, hProb * 360.0f, 0.0f);

                    qs.MCX(new uint[] { 0 }, 1);
                    qs.H(0);
                }
            });

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = x.QuantumSystem;
                    Bob.MeasurementResults[0] = qs.M(0);
                    Bob.MeasurementResults[1] = qs.M(1);

                    Bob.ResetProgram();
                    gameObject.SetActive(false);
                }
            });
        }

    }
}
