using OpenRelativity.Objects;
using System;
using System.Reflection;
using UnityEngine;

namespace Qrack
{
    public class TripletOxygen : RealTimeQasmProgram
    {
        public double alphaParam = 1e-4;

        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            double e0 = Math.Sqrt(1.0 - alphaParam * alphaParam);

            double[] hamiltonian = {
                // Hermitian 2x2 complex array 
                e0, 0.0, -alphaParam, 0.0,
                -alphaParam, 0.0, e0, 0.0,
            };

            TimeEvolveOpHeader teo = new TimeEvolveOpHeader(0, null);

            TimeEvolveOpHeader[] timeEvolveOpHeaders = new TimeEvolveOpHeader[1] { teo };

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                // Iterate every frame
                DeltaTime = 0.0f,
                quantumProgramUpdate = (x, deltaTime) =>
                {
                    QuantumSystem qs = QuantumSystem;
                    RelativisticObject ro = RelativisticObject;

                    qs.TimeEvolve(deltaTime, timeEvolveOpHeaders, hamiltonian);

                    float prob = qs.Prob(0);
                    qs.H(0);
                    float hProb = qs.Prob(0);
                    qs.H(0);

                    ro.transform.localEulerAngles = new Vector3(prob * 360.0f, hProb * 360.0f, 0.0f);
                    ro.riw = qs.transform.rotation;
                }
            });
        }

    }
}
