using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class SemiQftSchwarzschild : Schwarzschild
    {
        protected System.Random rng = new System.Random();

        protected float fold
        {
            get
            {
                float f = Mathf.Log(radius / state.planckLength) / Mathf.Log(2);
                return isExterior ? -f : f;
            }
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
            float deltaF = isExterior ? -deltaT / (state.planckTime * Mathf.Pow(2.0f, -f)) : deltaT / (state.planckTime * Mathf.Pow(2.0f, f));

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
