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

        public List<bool> ClassicalBitRegisters;
        public List<QcClassicalChannel> isSignalledSources { get; set; }

        private float nextInstructionTime;
        private float lastInstructionTime;

        protected List<RealTimeQasmInstruction> ProgramInstructions { get; set; }
        protected List<RealTimeQasmProgramHistoryPoint> HistoryPoints { get; set; }
        protected abstract void StartProgram();

        // Present time, according to program settings
        protected float ProgramTime
        {
            get
            {
                return IsVisualTime ? QuantumSystem.VisualTime : QuantumSystem.LocalTime;
            }
        }

        public void ResetTime()
        {
            nextInstructionTime = IsVisualTime ? QuantumSystem.VisualTime : QuantumSystem.LocalTime;
            if (InstructionIndex < ProgramInstructions.Count)
            {
                nextInstructionTime += ProgramInstructions[InstructionIndex].DeltaTime;
            }
            lastInstructionTime = nextInstructionTime;
        }

        public void ResetProgram()
        {
            InstructionIndex = 0;
            ResetTime();

            StopAllCoroutines();

            if (ProgramInstructions.Count > 0)
            {
                StartCoroutine(RunProgram());
            }
        }

        public void SelfResetProgram()
        {
            InstructionIndex = 0;
            ResetTime();
        }

        protected virtual void Start()
        {
            RelativisticObject = QuantumSystem.GetComponent<RelativisticObject>();

            isSignalledSources = new List<QcClassicalChannel>();
            ProgramInstructions = new List<RealTimeQasmInstruction>();
            HistoryPoints = new List<RealTimeQasmProgramHistoryPoint>();

            StartProgram();
            ResetProgram();
        }

        protected virtual void Update()
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

                float time = ProgramTime;

                if (lastInstructionTime < time && nextInstructionTime <= time)
                {
                    rtqi.quantumProgramUpdate(this, time - lastInstructionTime);

                    if (IsVisualTime)
                    {
                        // Update will immediately pop visual history events off, in this case.
                        Update();
                    }

                    lastInstructionTime = time;
                    ++InstructionIndex;

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