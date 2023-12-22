using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Tachyoid
{
    public abstract class TutorialController : MonoBehaviour
    {
        public bool isFinished { get; set; }

        public IEnumerator FadeTextToFullAlpha(float t, Text i)
        {
            i.color = new Color(i.color.r, i.color.g, i.color.b, 0);
            while (i.color.a < 1.0f)
            {
                i.color = new Color(i.color.r, i.color.g, i.color.b, i.color.a + (Time.deltaTime / t));
                yield return null;
            }
        }

        public IEnumerator FadeTextToZeroAlpha(float t, Text i)
        {
            i.color = new Color(i.color.r, i.color.g, i.color.b, 1);
            while (i.color.a > 0.0f)
            {
                i.color = new Color(i.color.r, i.color.g, i.color.b, i.color.a - (Time.deltaTime / t));
                yield return null;
            }
        }
    }
}