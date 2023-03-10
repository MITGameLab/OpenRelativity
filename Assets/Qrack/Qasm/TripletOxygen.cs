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
            double e0 = Math.Sqrt(1 - alphaParam * alphaParam);

            double[] hamiltonian = {
                // Hermitian 2x2 complex array 
                e0, 0, -alphaParam, 0,
                -alphaParam, 0, e0, 0,
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

                    qs.TimeEvolve(deltaTime, timeEvolveOpHeaders, hamiltonian);

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
