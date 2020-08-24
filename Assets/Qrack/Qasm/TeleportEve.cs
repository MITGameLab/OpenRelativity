namespace Qrack
{
    public class TeleportEve : RealTimeQasmProgram
    {
        public TeleportAlice Alice;

        private bool[] MeasurementResults;

        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            MeasurementResults = new bool[2];

            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                DeltaTime = 1.0f,
                quantumProgramUpdate = (x) =>
                {
                    QuantumSystem qs = x.QuantumSystem;
                    qs.SetBit(0, false);
                    qs.SetBit(1, false);
                    qs.SetBit(2, false);

                    qs.H(1);
                    qs.MCX(new uint[] { 1 }, 2);

                    Alice.ResetProgram();
                    gameObject.SetActive(false);
                }
            });
        }

    }
}