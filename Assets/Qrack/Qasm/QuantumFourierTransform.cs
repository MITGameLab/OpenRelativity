using System;
using System.Collections.Generic;

namespace Qrack
{
    public class QuantumFourierTransform : RealTimeQasmProgram
    {
        public OpenRelativity.GameState state;
        public OpenRelativity.ConformalMaps.Schwarzschild schwarzschild;

        public ulong maxQubits = 28;

        protected class QftHistoryPoint
        {
            public float Time { get; set; }
            public float Radius { get; set; }
        }

        protected void InitRandomQubit(QuantumSystem qs, ulong i)
        {
            System.Random rng = new System.Random();
            double a1 = 2 * Math.PI * rng.NextDouble();
            double a2 = 2 * Math.PI * rng.NextDouble();
            double a3 = 2 * Math.PI * rng.NextDouble();
            qs.U(i, a1, a2, a3);
        }

        protected List<QftHistoryPoint> expectationFrames = new List<QftHistoryPoint>();
        protected override void StartProgram()
        {
            InitRandomQubit(QuantumSystem, 0);
            QuantumSystem.H(0);
            ulong[] bits = new ulong[1] { 0 };

            expectationFrames.Add(new QftHistoryPoint
            {
                Time = (float)state.planckTime,
                Radius = QuantumSystem.PermutationExpectation(bits)
            });

            schwarzschild.schwarzschildRadius = expectationFrames[0].Radius / 2;

            for (ulong i = 1; i < maxQubits; i++)
            {
                AddLayer(i);
            }
        }

        private void AddLayer(ulong i)
        {
            // We need to calculate 1 time ahead of the current fold.
            // We also need to calculate all folds before the starting TotalTimeWorld.
            float totTime = (float)(state.planckTime * Math.Pow(2, i));
            float deltaTime = totTime / 2;
            if (deltaTime < state.TotalTimeWorld)
            {
                deltaTime = 0;
            }
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = deltaTime,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = x.QuantumSystem;
                    InitRandomQubit(qs, i);

                    for (ulong j = 0; j < i; j++)
                    {
                        ulong[] c = new ulong[1] { i };
                        ulong t = (i - 1U) - j;
                        double lambda = 2 * Math.PI / Math.Pow(2, j);
                        qs.MCU(c, t, 0, 0, lambda);
                    }
                    qs.H(i);
                    List<ulong> expBits = new List<ulong>();
                    for (ulong bit = 0; bit <= i; bit++)
                    {
                        expBits.Add(bit);
                    }
                    // For the output terms of the DFT, X_k, lower wavenumber "k" has longer wavelength.
                    // However, the QFT _ALREADY_ reverses the output order of the (inverse) DFT.
                    // expBits.Reverse();

                    expectationFrames.Add(new QftHistoryPoint
                    {
                        Time = totTime,
                        Radius = qs.PermutationExpectation(expBits.ToArray())
                    });

                    if (qs.QubitCount < maxQubits)
                    {
                        qs.QubitCount++;
                    }
                }
            });
        }

        protected override void Update()
        {
            base.Update();

            if (state.isMovementFrozen || (expectationFrames.Count == 0))
            {
                return;
            }

            schwarzschild.EnforceHorizon();

            int nextFrame = 1;

            while ((nextFrame < expectationFrames.Count) && (expectationFrames[nextFrame].Time < state.TotalTimeWorld))
            {
                nextFrame++;
            }

            if ((nextFrame >= expectationFrames.Count) || (expectationFrames[nextFrame].Time >= state.TotalTimeWorld))
            {
                schwarzschild.doEvaporate = true;
                return;
            }

            schwarzschild.doEvaporate = false;

            int lastFrame = nextFrame - 1;

            float r0 = expectationFrames[lastFrame].Radius;
            float t0 = expectationFrames[lastFrame].Time;
            float r1 = expectationFrames[nextFrame].Radius;
            float t1 = expectationFrames[nextFrame].Time;
            float t = state.TotalTimeWorld;

            schwarzschild.schwarzschildRadius = r0 + t * (r1 - r0) / (t1 - t0);

            schwarzschild.EnforceHorizon();
        }
    }
}
