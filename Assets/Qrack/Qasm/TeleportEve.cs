namespace Qrack
{
    public class TeleportEve : RealTimeQasmProgram
    {
        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 0.0f,
                quantumProgramUpdate = (x, y) =>
                {
                    QuantumSystem qs = x.QuantumSystem;
                    qs.SetBit(0, false);
                    qs.SetBit(1, false);
                    qs.SetBit(2, false);

                    qs.H(1);
                    qs.MCX(new uint[] { 1 }, 2);
                }
            });
        }

    }
}