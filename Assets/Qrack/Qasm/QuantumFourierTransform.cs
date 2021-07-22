using System;

namespace Qrack
{
    public class QuantumFourierTransform : RealTimeQasmProgram
    {
        public int maxQubits = 28;
        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            uint i;
            for (i = 0; i < maxQubits; i++)
            {
                AddLayer(i);
            }
        }

        private void AddLayer(uint i)
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = x.QuantumSystem;

                    Random rng = new Random();
                    double a1 = 2 * Math.PI * rng.NextDouble();
                    double a2 = 2 * Math.PI * rng.NextDouble();
                    double a3 = 2 * Math.PI * rng.NextDouble();

                    qs.U(i, a1, a2, a3);

                    for (uint j = 0; j < i; j++)
                    {
                        uint[] c = new uint[1] { i };
                        uint t = (i - 1U) - j;
                        double lambda = 2 * Math.PI / Math.Pow(2.0, j);
                        qs.MCU(c, t, 0, 0, lambda);
                    }
                    qs.H(i);
                }
            });
        }
    }
}
