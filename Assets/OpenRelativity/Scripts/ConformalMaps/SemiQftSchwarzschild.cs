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
                // Can we actually back-track to perfect 0 folds on the basis of exterior time?
                // We don't know exactly how long the evaporation will take, in the quantum limit.
                // If the black hole is "hairless," shouldn't this only depend on radius, rather than time?
                return Mathf.Log(radius / state.planckLength) / Mathf.Log(2);
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
            float deltaF = (isExterior ? -deltaT : deltaT) / (state.planckTime * Mathf.Pow(2.0f, f));

            float deltaR = Mathf.Pow(2.0f, f) * deltaF * (float)rng.NextDouble() / 2.0f;
            float thermoDeltaR = deltaRadius;

            radius += (isExterior != (deltaR > thermoDeltaR)) ? thermoDeltaR : deltaR;

            if (radius < 0)
            {
                radius = 0;
            }
        }
    }
}
