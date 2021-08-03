using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class SemiQftSchwarzschild : Schwarzschild
    {
        protected System.Random rng = new System.Random();
        protected float startFold;

        protected float fold
        {
            get
            {
                float tf = startFold + (isExterior ? -1 : 1) * Mathf.Log(state.TotalTimeWorld / state.planckTime) / Mathf.Log(2);
                float rf = Mathf.Log(radius / state.planckLength) / Mathf.Log(2);
                return (rf > tf) ? rf : tf;
            }
        }

        protected virtual void SetStartFold()
        {
            startFold = Mathf.Log(radius / state.planckLength) / Mathf.Log(2);
        }

        public override void Start()
        {
            base.Start();
            SetStartFold();
        }

        // Update is called once per frame
        public override void Update()
        {
            EnforceHorizon();

            if (radius <= 0 || !doEvaporate || state.isMovementFrozen)
            {
                return;
            }

            float deltaT = state.FixedDeltaTimeWorld;

            if (float.IsNaN(deltaT) || float.IsInfinity(deltaT))
            {
                return;
            }

            float f = fold;
            float deltaF = deltaT / (state.planckTime * Mathf.Pow(2.0f, f));
            if (isExterior)
            {
                f *= -1;
                deltaF *= -1;
            }

            float deltaR = Mathf.Pow(2.0f, f) * deltaF * (float)rng.NextDouble() / 2.0f;
            float thermoDeltaR = deltaRadius;

            radius += (deltaR > thermoDeltaR) ? thermoDeltaR : deltaR;

            if (radius < 0)
            {
                radius = 0;
            }
        }
    }
}
