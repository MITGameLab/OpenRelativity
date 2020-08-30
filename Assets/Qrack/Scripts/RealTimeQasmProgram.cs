using OpenRelativity.Objects;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Qrack
{
    public abstract class RealTimeQasmProgram : MonoBehaviour
    {
        public QuantumSystem QuantumSystem;
        public bool IsVisualTime = true;
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
                HistoryPoints[0].Action(QuantumSystem.VisualTime);
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

                float time = IsVisualTime ? QuantumSystem.VisualTime : QuantumSystem.LocalTime;

                if (nextInstructionTime <= time)
                {
                    rtqi.quantumProgramUpdate(this, time - lastInstructionTime);

                    if (IsVisualTime)
                    {
                        // Update will immediately pop visual history events off, in this case.
                        Update();
                    }

                    lastInstructionTime = time;
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