using UnityEngine;

namespace Pets.UI
{
    /// <summary>A linear float tween advanced by hand — what makes a bar drain over time instead of
    /// jumping. A plain struct with an explicit <see cref="Advance"/> rather than a coroutine, so a
    /// widget can drive it from Update while tests step it deterministically without a player loop.
    /// The default value is a finished tween.</summary>
    public struct ValueTween
    {
        public float From;
        public float To;
        public float Duration;
        public float Elapsed;

        public ValueTween(float from, float to, float duration)
        {
            From = from;
            To = to;
            Duration = Mathf.Max(0f, duration);
            Elapsed = 0f;
        }

        public bool IsRunning => Elapsed < Duration;

        public float Value => Duration <= 0f ? To : Mathf.Lerp(From, To, Mathf.Clamp01(Elapsed / Duration));

        public void Advance(float seconds)
        {
            Elapsed = Mathf.Min(Duration, Elapsed + Mathf.Max(0f, seconds));
        }
    }
}
