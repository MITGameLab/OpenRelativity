using OpenRelativity.Objects;
using System;
using UnityEngine;

namespace Qrack
{
    public class YorktownCouplers : RealTimeQasmProgram
    {
        public float gateInterval = 0.25f;
        public float gateDelay = 0.25f;
 
        protected bool isGateActing = false;
        protected bool isDiagonal = false;
        protected bool isDiagonalReversed = true;
        protected bool isSpokeReversed = false;
        protected float timer = 0.0f;
        protected ulong direction = 0;

        protected void EvolveGate(float dTime, QuantumSystem qs) {
            if (isDiagonal) {
                if (isDiagonalReversed) {
                    qs.PowMCX(new ulong[] { 1 }, dTime, 0);
                    qs.PowMCX(new ulong[] { 4 }, dTime, 3);
                } else {
                    qs.PowMCX(new ulong[] { 0 }, dTime, 1);
                    qs.PowMCX(new ulong[] { 3 }, dTime, 4);
                }
            } else if (isSpokeReversed) {
                ulong target = (direction < 2) ? direction : (direction + 1);
                qs.PowMCX(new ulong[] { 2 }, dTime, target);
            } else {
                ulong control = (direction < 2) ? direction : (direction + 1);
                qs.PowMCX(new ulong[] { control }, dTime, 2);
            }
        }

        // Prepare a Bell pair for Alice and Bob to share
        protected override void StartProgram()
        {
            ProgramInstructions.Add(new RealTimeQasmInstruction()
            {
                // Iterate every frame
                DeltaTime = 0,
                quantumProgramUpdate = (x, deltaTime) =>
                {
                    QuantumSystem qs = QuantumSystem;

                    float dTime = deltaTime;
                    timer += deltaTime;
                    if (isGateActing && (timer >= gateInterval)) {
                        EvolveGate(gateInterval + deltaTime - timer, qs);
                        isGateActing = false;
                        timer -= gateInterval;
                    } else if (!isGateActing && (timer >= gateDelay)) {
                        isGateActing = true;
                        timer -= gateDelay;
                        dTime -= timer;
                        isDiagonal = !isDiagonal;
                        if (isDiagonal) {
                            isDiagonalReversed = !isDiagonalReversed;
                        } else {
                            ++direction;
                            if (direction > 3) {
                                direction -= 4;
                                isSpokeReversed = !isSpokeReversed;
                            }
                        }
                    }

                    if (isGateActing) {
                        EvolveGate(dTime, qs);
                    }
                }
            });
        }

    }
}
