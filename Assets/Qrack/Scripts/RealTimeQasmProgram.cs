using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Qrack
{
    public abstract class RealTimeQasmProgram : MonoBehaviour
    {
        public QuantumSystem QuantumSystem;
        public bool DoRepeat = false;
        public int InstructionIndex = 0;

        private float nextInstructionTime;
        private float lastInstructionTime;

        protected List<RealTimeQasmInstruction> ProgramInstructions { get; set; }
        protected abstract void StartProgram();

        public void ResetTime()
        {
            if (ProgramInstructions == null)
            {
                ProgramInstructions = new List<RealTimeQasmInstruction>();

                StartProgram();
            }

            nextInstructionTime = QuantumSystem.LocalTime + ProgramInstructions[InstructionIndex].DeltaTime;
            lastInstructionTime = nextInstructionTime;
        }

        public void ResetProgram()
        {
            ResetTime();

            gameObject.SetActive(true);

            StartCoroutine(RunProgram());
        }

        private void Start()
        {
            ProgramInstructions = new List<RealTimeQasmInstruction>();

            StartProgram();

            ResetProgram();
        }

        IEnumerator RunProgram()
        {
            while (true)
            {
                RealTimeQasmInstruction rtqi = ProgramInstructions[InstructionIndex];

                if (nextInstructionTime <= QuantumSystem.LocalTime)
                {
                    rtqi.quantumProgramUpdate(this, QuantumSystem.LocalTime - lastInstructionTime);

                    lastInstructionTime = QuantumSystem.LocalTime;
                    InstructionIndex++;

                    if (InstructionIndex >= ProgramInstructions.Count)
                    {
                        InstructionIndex = 0;
                        if (!DoRepeat)
                        {
                            gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        rtqi = ProgramInstructions[InstructionIndex];
                        nextInstructionTime += rtqi.DeltaTime;
                    }
                }

                yield return null;
            }
        }

    }

}