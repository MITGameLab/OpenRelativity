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
                    RelativisticObject ro = RelativisticObject;

                    qs.Rand(0);

                    float zProb = qs.Prob(0);
                    qs.H(0);
                    float xProb = qs.Prob(0);
                    qs.S(0);
                    float yProb = qs.Prob(0);
                    qs.Z(0);
                    qs.S(0);
                    qs.H(0);

                    HistoryPoints.Add(new RealTimeQasmProgramHistoryPoint
                    {
                        WorldTime = qs.VisualTime,
                        Action = (time) =>
                        {
                            ro.transform.eulerAngles = new Vector3(xProb * 360.0f, yProb * 360.0f, zProb * 360.0f);
                            ro.riw = qs.transform.rotation;
                        }
                    });

                    qs.MCX(new uint[] { 0 }, 1);
                    qs.H(0);
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
