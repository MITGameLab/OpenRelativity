using System.Collections.Generic;
using UnityEngine;
using OpenRelativity.Objects;

namespace OpenRelativity.Audio
{
    public class RelativisticAudioSource : RelativisticBehavior
    {
        public Transform AudioSourceTransform;
        public AudioSource[] audioSources;
        public float[] pitches;

        public RelativisticObject relativisticObject { get; protected set; }

        protected RelativisticAudioSystem audioSystem;

        protected class RelativisticAudioSourcePlayTimeHistoryPoint
        {

            public float tiw { get; set; }
            public int audioSourceIndex { get; set; }

            public RelativisticAudioSourcePlayTimeHistoryPoint(float t, int audioSource)
            {
                tiw = t;
                audioSourceIndex = audioSource;
            }
        }

        protected List<RelativisticAudioSourcePlayTimeHistoryPoint> playTimeHistory;

        protected class RelativisticAudioSourcePVHistoryPoint
        {
            public Vector3 viw { get; set; }

            public float tiw { get; set; }

            public RelativisticAudioSourcePVHistoryPoint(Vector3 v, float t)
            {
                viw = v;
                tiw = t;
            }
        }

        protected List<RelativisticAudioSourcePVHistoryPoint> viwHistory;

        public Vector3 piw
        {
            get
            {
                return relativisticObject.piw;
            }
        }

        public Vector3 viw
        {
            get
            {
                return relativisticObject.viw;
            }
        }

        // Rindler metric responds immediately to player local acceleration,
        // but it would be better 
        public Matrix4x4 metric { get; protected set; }

        protected float tisw;

        protected Vector3 listenerPiw
        {
            get
            {
                return RelativisticAudioSystem.PlayerAudioListener.piw;
            }
        }

        protected Vector3 listenerViw
        {
            get
            {
                return RelativisticAudioSystem.PlayerAudioListener.viw;
            }
        }

        public Vector3 soundVelocity
        {
            get
            {
                Vector3 dispUnit = (listenerPiw - piw).normalized;

                if (dispUnit.sqrMagnitude < 0.5f)
                {
                    return dispUnit = audioSystem.WorldSoundMediumRapidity.normalized;
                }

                return (audioSystem.RapidityOfSound * dispUnit + audioSystem.WorldSoundMediumRapidity)
                    .RapidityToVelocity(metric);
            }
        }
        protected Vector3 soundPosition
        {
            get
            {
                return piw + tihw * viw;
            }
        }

        public float tihw
        {
            // soundLightDelayTime is negative
            get
            {
                Vector3 dispUnit = (listenerPiw - piw).normalized;

                if (dispUnit.sqrMagnitude < 0.5f)
                {
                    return 0;
                }

                return tisw  * state.SpeedOfLight / Vector3.Project(soundVelocity, dispUnit).magnitude;
            }
        }

        protected Vector3 lastViw;

        protected float currentSmoothingTime;
        protected float collisionSmoothingSeconds;
        protected Vector3 collisionSoundPos;
        protected Vector3 collisionOpticalPos;

        // Start is called before the first frame update
        void Start()
        {
            audioSystem = RelativisticAudioSystem.Instance;
            relativisticObject = GetComponent<RelativisticObject>();
            playTimeHistory = new List<RelativisticAudioSourcePlayTimeHistoryPoint>();
            viwHistory = new List<RelativisticAudioSourcePVHistoryPoint>();

            if (audioSources == null || audioSources.Length == 0)
            {
                audioSources = AudioSourceTransform.GetComponents<AudioSource>();
            }

            if (pitches == null || pitches.Length == 0)
            {
                pitches = new float[audioSources.Length];
                for (int i = 0; i < audioSources.Length; ++i)
                {
                    pitches[i] = audioSources[i].pitch;

                    // Turn off built-in Doppler
                    audioSources[i].dopplerLevel = 0;
                }
            }

            lastViw = viw;
            viwHistory.Add(new RelativisticAudioSourcePVHistoryPoint(viw, float.NegativeInfinity));
        }

        private void Update()
        {
            if (state.isMovementFrozen)
            {
                for (int i = 0; i < audioSources.Length; ++i)
                {
                    audioSources[i].pitch = 0;
                }

                return;
            }

            metric = SRelativityUtil.GetRindlerMetric(piw);
            tisw = relativisticObject.GetTisw();

            float soundWorldTime = state.TotalTimeWorld + tihw;

            if (lastViw != viw)
            {
                lastViw = viw;
                currentSmoothingTime = 0;
                collisionSmoothingSeconds = tisw - tihw;

                // This is a hack, but acceptable for gaming. If multiple collisions happen,
                // the audio source will interpolate between in-flight position and ultimate end point
                // from light and collision physics simulation.
                // TODO: Consider tracing exact idealized in-flight collision history.
                collisionSoundPos = AudioSourceTransform.position;

                collisionOpticalPos = relativisticObject.opticalPiw;
                viwHistory.Add(new RelativisticAudioSourcePVHistoryPoint(viw, state.TotalTimeWorld + tisw));
            }

            while (viwHistory.Count > 1)
            {
                if (viwHistory[1].tiw <= soundWorldTime)
                {
                    viwHistory.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }

            if (currentSmoothingTime < collisionSmoothingSeconds)
            {
                // If velocity changes, this helps smooth out a collision that puts the new sound time behind the old sound time.
                currentSmoothingTime += Time.deltaTime;

                if (currentSmoothingTime < collisionSmoothingSeconds)
                {
                    AudioSourceTransform.position = Vector3.Lerp(collisionSoundPos, collisionOpticalPos, currentSmoothingTime / collisionSmoothingSeconds);
                }
                else
                {
                    AudioSourceTransform.position = soundPosition;
                }
            }
            else
            {
                AudioSourceTransform.position = soundPosition;
            }

            WorldSoundDopplerShift();

            while ((playTimeHistory.Count > 0) && (playTimeHistory[0].tiw <= soundWorldTime))
            {
                audioSources[playTimeHistory[0].audioSourceIndex].Play();
                playTimeHistory.RemoveAt(0);
            }
        }

        public void WorldSoundDopplerShift()
        {
            Vector3 unitDisplacementSR = listenerPiw - (piw + tihw * viwHistory[0].viw);
            unitDisplacementSR.Normalize();
            if (unitDisplacementSR == Vector3.zero)
            {
                unitDisplacementSR = Vector3.up;
            }

            Vector3 sourceRapidity = viwHistory[0].viw;
            sourceRapidity = sourceRapidity * sourceRapidity.Gamma(metric);
            sourceRapidity -= audioSystem.WorldSoundMediumRapidity;

            Vector3 receiverRapidity = listenerViw;
            receiverRapidity = receiverRapidity * receiverRapidity.Gamma();
            receiverRapidity -= audioSystem.WorldSoundMediumRapidity;

            float frequencyFactor = (audioSystem.RapidityOfSound + Vector3.Dot(sourceRapidity, unitDisplacementSR))
                / (audioSystem.RapidityOfSound + Vector3.Dot(receiverRapidity, -unitDisplacementSR));

            ShiftPitches(frequencyFactor);
        }

        public void ShiftPitches(float frequencyFactor)
        {
            for (int i = 0; i < pitches.Length; ++i)
            {
                audioSources[i].pitch = frequencyFactor * pitches[i];
            }
        }

        /// <summary>
        /// Play a sound delayed to match being seen happen now.
        /// </summary>
        /// <param name="audioSourceIndex"></param>
        public void PlaySeenNow(int audioSourceIndex = 0)
        {
            playTimeHistory.Add(new RelativisticAudioSourcePlayTimeHistoryPoint(state.TotalTimeWorld + tisw, audioSourceIndex));
        }

        /// <summary>
        /// Play a sound delayed to match happening "now" on the world frame clock
        /// </summary>
        /// <param name="audioSourceIndex"></param>
        public void PlayOnWorldClock(int audioSourceIndex = 0)
        {
            playTimeHistory.Add(new RelativisticAudioSourcePlayTimeHistoryPoint(state.TotalTimeWorld, audioSourceIndex));
        }

        /// <summary>
        /// Play a sound heard right now.
        /// </summary>
        /// <param name="audioSourceIndex"></param>
        public void PlayHeardNow(int audioSourceIndex = 0)
        {
            audioSources[audioSourceIndex].Play();
        }
    }
}