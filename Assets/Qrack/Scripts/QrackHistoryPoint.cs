using System;

namespace Qrack
{
    public class QrackHistoryPoint
    {
        public float WorldTime { get; set; }
        public Action<float> Action { get; set; }
    }
}