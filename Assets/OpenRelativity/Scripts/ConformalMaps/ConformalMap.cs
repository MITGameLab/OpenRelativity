using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Comovement
    {
        public Vector4 piw { get; set; }
        public Quaternion riw { get; set; }
    }

    public abstract class ConformalMap : RelativisticBehavior
    {
        // TO DEFINE A CONFORMAL MAP ON A GENERAL RIEMANNIAN BACKGROUND GEOMETRY,
        // Define the 3 following functions...

        // Given an input Unity world coordinate 3-position and intrinsic rotation, tell me how both comove in free fall, over a proper time interval.
        abstract public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw);

        // Given an input Unity world coordinate 3-position, tell me what (gravitational) local acceleration won't change position, at 0 relative velocity. 
        abstract public Vector3 GetRindlerAcceleration(Vector3 piw);

        // Given an input Unity world coordinate 3-position, tell me the velocity of free fall, i.e. at 0 (gravitational + proper) acceleration.
        abstract public Vector3 GetFreeFallVelocity(Vector3 piw);

        // (Optional to override...) Given an input Unity world coordinate 3-position, tell me the metric, as seen by THE PLAYER.
        virtual public Matrix4x4 GetMetric(Vector3 piw)
        {
            return SRelativityUtil.GetRindlerMetric(piw, GetRindlerAcceleration(piw), Vector3.zero);
        }

        // (A "conformal map" LOOKS like flat space LOCALLY for "matter fields," but "conforms" to an underlying curved space-time manifold.)
        // (... In other words, Euclidean geometry for matter fields, warped to "conform" to the true underlying subtle or gross curvature of space-time.)
    }
}
