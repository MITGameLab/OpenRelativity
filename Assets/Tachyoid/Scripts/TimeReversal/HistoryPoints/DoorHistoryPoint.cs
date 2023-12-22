using Tachyoid.Objects;

namespace Tachyoid.TimeReversal.HistoryPoints
{
    public class DoorHistoryPoint : HistoryPoint
    {
        public bool Opening { get; set; }
        public ButtonController Trigger { get; set; }
        public float OpenTimer { get; set; }
    }
}
