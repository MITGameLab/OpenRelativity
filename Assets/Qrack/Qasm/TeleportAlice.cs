using OpenRelativity.Objects;
using UnityEngine;

namespace Qrack
{
    public class TeleportAlice : RealTimeQasmProgram
    {
        public TeleportBob Bob;

        public QcClassicalChannel channelToBob;

        protected override void StartProgram()
        {

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 2.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = QuantumSystem;

                    qs.Rand(0);

                    BlochSphereCoordinates coords = qs.Prob3Axis(0);

                    qs.MCX(new ulong[] { 0 }, 1);
                    qs.H(0);

                    HistoryPoints.Add(new RealTimeQasmProgramHistoryPoint
                    {
                        WorldTime = qs.VisualTime,
                        Action = (time) =>
                        {
                            RelativisticObject ro = RelativisticObject;
                            ro.transform.rotation = Quaternion.Euler((float)coords.inclination * Mathf.Rad2Deg, (float)coords.azimuth * Mathf.Rad2Deg, 0);
                            ro.riw = qs.transform.rotation;
                            ro.localScale = new Vector3((float)coords.r, (float)coords.r, (float)coords.r);
                        }
                    });
                }
            });

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 0.1f,
                quantumProgramUpdate = (x, y) =>
                {
                    ClassicalBitRegisters[0] = QuantumSystem.M(0);
                    ClassicalBitRegisters[1] = QuantumSystem.M(1);
                    channelToBob.EmitBit(0, 0);
                    channelToBob.EmitBit(1, 1);
                }
            });
        }

    }
}
