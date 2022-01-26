using OpenRelativity.Objects;
using System;
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

                    float zProb = qs.Prob(0);
                    qs.H(0);
                    float xProb = qs.Prob(0);
                    qs.S(0);
                    float yProb = qs.Prob(0);
                    qs.AdjS(0);
                    qs.H(0);

                    HistoryPoints.Add(new RealTimeQasmProgramHistoryPoint
                    {
                        WorldTime = qs.VisualTime,
                        Action = (time) =>
                        {
                            ro.transform.localEulerAngles = new Vector3(xProb * 180.0f, yProb * 180.0f, zProb * 180.0f);
                            ro.riw = qs.transform.rotation;
                        }
                    });
                }
            });
        }

    }
}
