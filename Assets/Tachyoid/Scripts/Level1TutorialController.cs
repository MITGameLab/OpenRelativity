using UnityEngine;
using UnityEngine.UI;
using Google.XR.Cardboard;
using System.Collections;

namespace Tachyoid
{
    public class Level1TutorialController : TutorialController
    {
        public Transform srCamera;
        public Transform canvasRotate;
        public OpenRelativity.GameState state;

        public Text text;

        private float timer;
        private const float minTime = 5.0f;

        IEnumerator Start()
        {

            isFinished = false;

            //intro
            text.text = "Hold-press the button to walk.";
            yield return FadeTextToFullAlpha(1.0f, text);

            timer = 0.0f;
            while (timer < minTime || !(Input.GetMouseButton(0) || Api.IsTriggerPressed))
            {
                timer += Time.deltaTime;
                yield return null;
            }

            yield return FadeTextToZeroAlpha(0.5f, text);

            text.text = "Double-click the button to enter\nfaster-than-light targetting.";

            yield return FadeTextToFullAlpha(1.0f, text);

            timer = 0.0f;
            while (!state.isMovementFrozen)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            yield return FadeTextToZeroAlpha(0.2f, text);

            if (state.isMovementFrozen)
            {
                text.text = "Click the button again to travel\nfaster than light,\n(or double-click the button to cancel).";

                yield return FadeTextToFullAlpha(1.0f, text);

                while (state.isMovementFrozen)
                {
                    yield return null;
                }

                yield return FadeTextToZeroAlpha(0.2f, text);
            }

            timer = 0.0f;
            while (timer < 0.5f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            Vector3 pos = state.playerTransform.position;
            while (!((((pos.z > -28) && (pos.z < 28)) || (pos.z > 78)) && (pos.x > -28) && (pos.x < 28)))
            {
                pos = state.playerTransform.position;
                yield return null;
            }

            canvasRotate.forward = Vector3.ProjectOnPlane(srCamera.forward, Vector3.up).normalized;
            text.text = "When you travel faster than light,\nyou travel slightly back in time.\n";
            yield return FadeTextToFullAlpha(1.0f, text);

            timer = 0.0f;
            while (timer < minTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            text.text = "You cannot time travel back\ninto a region of paradox.\nThese regions are blocked from\ntime travel targetting.";
            yield return FadeTextToFullAlpha(1.0f, text);

            timer = 0.0f;
            while (timer < minTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            yield return FadeTextToZeroAlpha(0.2f, text);

            text.text = "Nothing moves faster than\nthe speed of light,\n(except you, maybe,)\nbut light is very slow, here.";
            yield return FadeTextToFullAlpha(1.0f, text);

            timer = 0.0f;
            while (timer < minTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            yield return FadeTextToZeroAlpha(0.2f, text);

            text.text = "Each room is a puzzle,\nto solve with time travel.\nEscape, and be rewarded.";
            yield return FadeTextToFullAlpha(1.0f, text);

            timer = 0.0f;
            while (timer < minTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            yield return FadeTextToZeroAlpha(0.2f, text);

            isFinished = true;

            canvasRotate.gameObject.SetActive(false);
        }
    }
}