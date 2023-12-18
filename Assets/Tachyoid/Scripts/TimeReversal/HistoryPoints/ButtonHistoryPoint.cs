namespace Tachyoid.TimeReversal.HistoryPoints
{
    public enum ButtonState
    {
        Pressing, WaitToEndPress, EndPress, PressResolved, PressHeld
    }

    public class ButtonHistoryPoint : HistoryPoint
    {
        public ButtonState State { get; set; }
    }
}
