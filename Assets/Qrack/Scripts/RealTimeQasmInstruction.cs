namespace Qrack
{

    public class RealTimeQasmInstruction
    {
        public float DeltaTime { get; set; }

        public delegate void QuantumProgramUpdate(RealTimeQasmProgram realTimeQasmProgram, float frameTime);

        public QuantumProgramUpdate quantumProgramUpdate;
    }

}
