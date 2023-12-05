using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.Objects {
    public class ChargedObject : RelativisticBehavior
    {
        // The object can be electrically charged
        public float electricCharge = 1;
        // Outside of a radius, the effects of electric charge can be ignored.
        public float electromagnetismRange = 16.0f;
        // Maximum force that can be applied.
        private float maxForce = 256.0f;

        void AddElectricForce()
        {
            if (electricCharge <= SRelativityUtil.FLT_EPSILON) {
                return;
            }
        
            Collider myCollider = GetComponent<Collider>();
            if (!myCollider) {
                return;
            }

            RelativisticObject myRO = GetComponent<RelativisticObject>();
            if (!myRO) {
                return;
            }

            Collider[] otherColliders = Physics.OverlapSphere(myCollider.transform.position, electromagnetismRange);
            foreach (Collider otherCollider in otherColliders)
            {
                ChargedObject otherCO = otherCollider.GetComponent<ChargedObject>();
                if (!otherCO) {
                    continue;
                }
                if (otherCO.electricCharge <= SRelativityUtil.FLT_EPSILON) {
                    continue;
                }
                Vector3 displacement = myCollider.transform.position - otherCollider.transform.position;
                float force = (float)(electricCharge * otherCO.electricCharge / (4 * Mathf.PI * state.vacuumPermittivity * displacement.sqrMagnitude));
                if (float.IsInfinity(force) || float.IsNaN(force) || (Mathf.Abs(force) > maxForce)) {
                    myRO.AddForce(((Mathf.Sign(electricCharge) == Mathf.Sign(otherCO.electricCharge)) ? maxForce : -maxForce) * displacement.normalized);
                } else {
                    myRO.AddForce(force * displacement.normalized);
                }
            }
        }

        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            AddElectricForce();
        }
    }
}
