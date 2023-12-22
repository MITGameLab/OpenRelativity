using System;

namespace Qrack
{
    public class RealTimeQasmProgramHistoryPoint
    {
        public virtual float WorldTime { get; set; }
        public Action<float> Action { get; set; }
    }
}