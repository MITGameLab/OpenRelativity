namespace Tachyoid.TimeReversal.Generic {
	public interface ITimeReversibleObject {
        void UpdateTimeTravelPrediction();
        void ReverseTime();
        void UndoTimeTravelPrediction();
    }
}