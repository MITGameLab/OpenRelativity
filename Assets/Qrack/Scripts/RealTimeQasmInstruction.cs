namespace Qrack
{

    public class RealTimeQasmInstruction
    {
        public float DeltaTime { get; set; }

        public delegate void QuantumProgramUpdate(RealTimeQasmProgram realTimeQasmProgram);

        public QuantumProgramUpdate quantumProgramUpdate;
    }

}
