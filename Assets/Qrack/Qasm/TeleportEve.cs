using UnityEngine;

namespace Qrack
{
    public class TeleportEve : RealTimeQasmProgram
    {
        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 0.1f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = x.QuantumSystem;
                    qs.ResetAll();

                    qs.H(1);
                    qs.MCX(new ulong[] { 1 }, 2);
                }
            });
        }

    }
}