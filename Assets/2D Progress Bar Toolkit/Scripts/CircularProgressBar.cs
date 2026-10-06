// -----------------------------------------------------------------------------
// 2D Progress Bar Toolkit
// © University of Games
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UniversityOfGames.ProgressBarToolkit
{
    /// <summary>
    /// A radial progress bar that arranges its segments along a circle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Angles are measured in degrees, clockwise from the top of the circle, and the
    /// segments occupy the arc between <see cref="StartAngle"/> and
    /// <see cref="EndAngle"/>. A full ring is <c>0 → 360</c>; a bottom half-circle is
    /// <c>90 → 270</c> (rotate the bar's root 180° for a classic top gauge, as the
    /// shipped prefabs do). <see cref="SegmentedProgressBar.SizeOfNotch"/> is the gap
    /// between segments, also in degrees.
    /// </para>
    /// <para>
    /// The segment template and its fill are expected to be
    /// <see cref="Image.Type.Filled"/> images using <i>Radial 360</i> with origin
    /// <i>Top</i>; every shipped circular prefab is set up this way. Internally all
    /// arcs are tracked as fractions of a full circle, which is the unit
    /// <see cref="Image.fillAmount"/> works in.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/2D Progress Bar Toolkit/Circular Progress Bar")]
    public sealed class CircularProgressBar : SegmentedProgressBar
    {
        #region Serialized fields

        [Header("Angles")]
        [Tooltip("Angle (in degrees, clockwise from the top) at which the first segment starts.")]
        [FormerlySerializedAs("startAngle")]
        [SerializeField, Range(0f, 360f)]
        private float m_StartAngle = 40f;

        [Tooltip("Angle (in degrees, clockwise from the top) at which the last segment ends.")]
        [FormerlySerializedAs("endAngle")]
        [SerializeField, Range(0f, 360f)]
        private float m_EndAngle = 320f;

        #endregion

        /// <summary>Arc of a single segment as a fraction of a full circle; cached by <see cref="OnBeforeBuild"/>.</summary>
        private float m_SegmentSize;

        #region Properties

        /// <summary>Angle (in degrees) at which the first segment starts. Assigning a new value rebuilds the bar.</summary>
        public float StartAngle
        {
            get => m_StartAngle;
            set
            {
                float angle = Mathf.Clamp(value, 0f, 360f);
                if (Mathf.Approximately(angle, m_StartAngle))
                {
                    return;
                }

                m_StartAngle = angle;
                Rebuild();
            }
        }

        /// <summary>Angle (in degrees) at which the last segment ends. Assigning a new value rebuilds the bar.</summary>
        public float EndAngle
        {
            get => m_EndAngle;
            set
            {
                float angle = Mathf.Clamp(value, 0f, 360f);
                if (Mathf.Approximately(angle, m_EndAngle))
                {
                    return;
                }

                m_EndAngle = angle;
                Rebuild();
            }
        }

        #endregion

        #region SegmentedProgressBar implementation

        protected override void OnBeforeBuild(int segmentCount)
        {
            // Work in fractions of a full circle (the unit Image.fillAmount uses):
            // whatever the leading gap, trailing gap and notches don't consume is
            // divided evenly among the segments.
            float startFraction = NormalizeAngle(m_StartAngle);
            float endFraction = NormalizeAngle(360f - m_EndAngle);
            float notchesFraction = (segmentCount - 1) * NormalizeAngle(SizeOfNotch);
            float availableFraction = 1f - startFraction - endFraction - notchesFraction;

            m_SegmentSize = availableFraction / segmentCount;
        }

        protected override void LayoutSegment(Image background, Image fill, int index)
        {
            background.fillAmount = m_SegmentSize;

            // Each segment is a partially filled radial image rotated into place:
            // segment i starts after the leading gap, i whole segments and i notches.
            // Negative Z because uGUI rotates counter-clockwise for positive angles.
            float zRotation = m_StartAngle + index * FractionToAngle(m_SegmentSize) + index * SizeOfNotch;
            background.transform.localRotation = Quaternion.Euler(0f, 0f, -zRotation);
        }

        /// <summary>A fully covered radial segment fills exactly its own arc, not the whole circle.</summary>
        protected override float SegmentFillScale => m_SegmentSize;

        #endregion

        #region Helpers

        /// <summary>Converts an angle in degrees to a fraction of a full circle, clamped to [0, 1].</summary>
        private static float NormalizeAngle(float angle)
        {
            return Mathf.Clamp01(angle / 360f);
        }

        /// <summary>Converts a fraction of a full circle to an angle in degrees.</summary>
        private static float FractionToAngle(float fraction)
        {
            return 360f * fraction;
        }

        #endregion
    }
}
