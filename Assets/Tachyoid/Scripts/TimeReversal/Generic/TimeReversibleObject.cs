using OpenRelativity.Objects;
using System.Collections.Generic;
using Tachyoid.TimeReversal.HistoryPoints;
using UnityEngine;

namespace Tachyoid.TimeReversal.Generic
{
    public abstract class TimeReversibleObject<THistoryPoint> : MonoBehaviour, ITimeReversibleObject where THistoryPoint : IHistoryPoint
    {
        protected TachyoidGameState state;
        
        protected RelativisticObject myRO;

        public abstract void UpdateTimeTravelPrediction();
        public abstract void ReverseTime();
        public abstract void UndoTimeTravelPrediction();

        protected abstract bool IsUpdatedPreview();
        protected abstract void ReverseTimeUpdate(float earlyTime, float lateTime);
        protected abstract void ForwardTimeUpdate(float earlyTime, float lateTime);

        protected float lastOpticalTime;

        protected List<THistoryPoint> history;

        protected List<THistoryPoint> GetHistoryBetweenTimes(float startTime, float endTime)
        {
            List<THistoryPoint> currentPoints = new List<THistoryPoint>();
            int i = 0;
            while (i < history.Count && history[i].WorldTime <= endTime)
            {
                if (history[i].WorldTime > startTime)
                {
                    currentPoints.Add(history[i]);
                }
                i++;
            }

            return currentPoints;
        }

        protected List<THistoryPoint> GetReversedHistoryAfterTime(float startTime)
        {
            List<THistoryPoint> currentPoints = new List<THistoryPoint>();
            int i = history.Count - 1;
            while (i >= 0)
            {
                if (history[i].WorldTime > startTime)
                {
                    currentPoints.Add(history[i]);
                }
                i--;
            }

            return currentPoints;
        }

        protected THistoryPoint GetLastPointBefore(float origWorldTime)
        {
            int i = 0;
            THistoryPoint action = default;
            while (i < history.Count && history[i].WorldTime < origWorldTime)
            {
                action = history[i];
                i++;
            }
            return action;

        }

        // Start is called before the first frame update
        protected virtual void Start()
        {
            state = GameObject.FindGameObjectWithTag("Player").GetComponent<TachyoidGameState>();
            myRO = GetComponent<RelativisticObject>();
            history = new List<THistoryPoint>();
            lastOpticalTime = state.TotalTimeWorld + myRO.localTimeOffset + myRO.GetTisw();
        }

        // Update is called once per frame
        protected virtual void FixedUpdate()
        {
            if (!IsUpdatedPreview() && state.isMovementFrozen)
            {
                return;
            }

            float endTime = state.TotalTimeWorld + myRO.localTimeOffset + myRO.GetTisw();
            float startTime = lastOpticalTime;

            if (startTime > endTime)
            {
                ReverseTimeUpdate(endTime, startTime);
            }
            else
            {
                ForwardTimeUpdate(startTime, endTime);
            }

            if (state.isMovementFrozen)
            {
                return;
            }

            endTime -= TachyoidConstants.WaitToDestroyHistory;
            while ((history.Count > 1) && (history[0].WorldTime < endTime))
            {
                history.RemoveAt(0);
            }
        }
    }
}
