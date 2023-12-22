#if QRACK_INCLUDED
using Qrack;
using Tachyoid.TimeReversal.Generic;

namespace Tachyoid.QrackInterop {
    public abstract class ReversibleRealTimeQasmProgram : RealTimeQasmProgram, ITimeReversibleObject
    {

        public float HistoryRetentionSeconds;

        private float lastVisualWorldTime;

        protected override void Start()
        {
            lastVisualWorldTime = QuantumSystem.VisualTime;

            base.Start();
        }

        // Update is called once per frame
        protected override void Update()
        {
            // TODO
        }

        public void ReverseTime()
        {
            throw new System.NotImplementedException();
        }

        public void UndoTimeTravelPrediction()
        {
            throw new System.NotImplementedException();
        }

        public void UpdateTimeTravelPrediction()
        {
            throw new System.NotImplementedException();
        }
    }
}
#endif