using OpenRelativity.Objects;
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

        public RelativisticObject RelativisticObject { get; set; }

        private float nextInstructionTime;
        private float lastInstructionTime;

        protected List<RealTimeQasmInstruction> ProgramInstructions { get; set; }
        protected List<QrackHistoryPoint> HistoryPoints { get; set; }
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
            InstructionIndex = 0;

            ResetTime();

            StopAllCoroutines();
            StartCoroutine(RunProgram());
        }

        public void SelfResetProgram()
        {
            InstructionIndex = 0;
            nextInstructionTime = QuantumSystem.LocalTime + ProgramInstructions[0].DeltaTime;
            lastInstructionTime = nextInstructionTime;
        }

        private void Start()
        {
            RelativisticObject = QuantumSystem.GetComponent<RelativisticObject>();

            ProgramInstructions = new List<RealTimeQasmInstruction>();
            HistoryPoints = new List<QrackHistoryPoint>();

            StartProgram();

            ResetProgram();
        }

        private void Update()
        {
            while ((HistoryPoints.Count > 0) && (HistoryPoints[0].WorldTime <= QuantumSystem.VisualTime))
            {
                HistoryPoints[0].Action(HistoryPoints[0].WorldTime);
                HistoryPoints.RemoveAt(0);
            }
        }

        IEnumerator RunProgram()
        {
            while (true)
            {
                if (InstructionIndex >= ProgramInstructions.Count)
                {
                    StopAllCoroutines();
                    yield return null;
                }

                RealTimeQasmInstruction rtqi = ProgramInstructions[InstructionIndex];

                if (nextInstructionTime <= QuantumSystem.LocalTime)
                {
                    rtqi.quantumProgramUpdate(this, QuantumSystem.LocalTime - lastInstructionTime);

                    lastInstructionTime = QuantumSystem.LocalTime;
                    InstructionIndex++;

                    if (InstructionIndex >= ProgramInstructions.Count)
                    {
                        if (DoRepeat)
                        {
                            SelfResetProgram();
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