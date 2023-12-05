using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.Objects {
    public class ChargedObject : RelativisticBehavior
    {
        // The object can be electrically charged
        public float electricCharge = 0.03f;
        // Outside of a radius, the effects of electric charge can be ignored.
        public float electromagnetismRange = 32.0f;
        // Maximum force that can be applied.
        private float maxForce = 1e32f;

        void AddElectromagneticForces()
        {
            if (Mathf.Abs(electricCharge) <= SRelativityUtil.FLT_EPSILON) {
                return;
            }

            RelativisticObject myRO = GetComponent<RelativisticObject>();
            if (!myRO) {
                return;
            }

            Collider[] otherColliders = Physics.OverlapSphere(myRO.piw, electromagnetismRange);
            foreach (Collider otherCollider in otherColliders)
            {
                ChargedObject otherCO = otherCollider.GetComponent<ChargedObject>();
                if (!otherCO) {
                    continue;
                }
                if (Mathf.Abs(otherCO.electricCharge) <= SRelativityUtil.FLT_EPSILON) {
                    continue;
                }

                RelativisticObject otherRO = otherCollider.GetComponent<RelativisticObject>();
                if (!otherRO) {
                    continue;
                }

                Vector3 displacement = myRO.piw - otherRO.piw;
                Vector3 rUnit = displacement.normalized;

                // Electrostatic force:
                float force = (float)(electricCharge * otherCO.electricCharge / (4 * Mathf.PI * state.vacuumPermittivity * displacement.sqrMagnitude));
                if (float.IsInfinity(force) || float.IsNaN(force) || (Mathf.Abs(force) > maxForce)) {
                    myRO.AddForce(((Mathf.Sign(electricCharge) == Mathf.Sign(otherCO.electricCharge)) ? maxForce : -maxForce) * rUnit);
                } else {
                    myRO.AddForce(force * displacement.normalized);
                }

                // Magnetic force:
                float magneticMag = (float)((state.vacuumPermeability *  otherCO.electricCharge) / (4 * Mathf.PI * displacement.sqrMagnitude));
                Vector3 magneticField = magneticMag * Vector3.Cross(otherRO.viw, rUnit);
                Vector3 magneticForce = electricCharge * Vector3.Cross(myRO.viw, magneticField);
                force = magneticForce.magnitude;
                if (float.IsInfinity(force) || float.IsNaN(force) || (Mathf.Abs(force) > maxForce)) {
                    myRO.AddForce(maxForce * magneticForce.normalized);
                } else {
                    myRO.AddForce(magneticForce);
                }
            }
        }

        // Start is called before the first frame update
        void Start()
        {
        
        }

        // FixedUpdate is called once before physics update
        void FixedUpdate()
        {
            AddElectromagneticForces();
        }
    }
}
