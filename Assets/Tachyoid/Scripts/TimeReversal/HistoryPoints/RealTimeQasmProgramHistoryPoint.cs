namespace Tachyoid.TimeReversal.HistoryPoints
{
    public class RealTimeQasmProgramHistoryPoint : Qrack.RealTimeQasmProgramHistoryPoint, IHistoryPoint
    {
        public override float WorldTime { get; set; }
    }
}