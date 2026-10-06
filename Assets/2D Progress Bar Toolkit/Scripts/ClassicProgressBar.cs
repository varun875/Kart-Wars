// -----------------------------------------------------------------------------
// 2D Progress Bar Toolkit
// © University of Games
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

namespace UniversityOfGames.ProgressBarToolkit
{
    /// <summary>
    /// A horizontal progress bar that lays its segments out in a row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The row is centered on the component's <see cref="RectTransform"/> and spans
    /// its width exactly: the width of a single segment is
    /// <c>(barWidth - (n - 1) * notch) / n</c>. Resize the bar's rect (or let it
    /// stretch with its parent) and call <see cref="SegmentedProgressBar.Rebuild"/>
    /// to re-layout at the new size.
    /// </para>
    /// <para>
    /// The segment template keeps its authored vertical anchoring — a template that
    /// stretches to the bar's height keeps stretching on every segment — while the
    /// horizontal axis is managed by the layout. The fill image is expected to be a
    /// <see cref="Image.Type.Filled"/> image using <i>Horizontal</i> fill.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/2D Progress Bar Toolkit/Classic Progress Bar")]
    public sealed class ClassicProgressBar : SegmentedProgressBar
    {
        /// <summary>Width of a single segment in local units; cached by <see cref="OnBeforeBuild"/>.</summary>
        private float m_SegmentWidth;

        #region SegmentedProgressBar implementation

        protected override void OnBeforeBuild(int segmentCount)
        {
            // rect.width respects stretched anchors, unlike sizeDelta.
            float barWidth = ((RectTransform)transform).rect.width;
            m_SegmentWidth = (barWidth - (segmentCount - 1) * SizeOfNotch) / segmentCount;
        }

        protected override void LayoutSegment(Image background, Image fill, int index)
        {
            RectTransform backgroundRect = background.rectTransform;

            // Segments are centered horizontally with explicit offsets, but the vertical
            // anchoring is left exactly as authored on the template — a template that
            // stretches to the bar's height keeps doing so on every segment.
            Vector2 anchorMin = backgroundRect.anchorMin;
            Vector2 anchorMax = backgroundRect.anchorMax;
            anchorMin.x = 0.5f;
            anchorMax.x = 0.5f;
            backgroundRect.anchorMin = anchorMin;
            backgroundRect.anchorMax = anchorMax;

            Vector2 pivot = backgroundRect.pivot;
            pivot.x = 0.5f;
            backgroundRect.pivot = pivot;

            Vector2 size = backgroundRect.sizeDelta;
            size.x = m_SegmentWidth;
            backgroundRect.sizeDelta = size;

            Vector2 position = backgroundRect.anchoredPosition;
            position.x = (index - (NumberOfSegments - 1) * 0.5f) * (m_SegmentWidth + SizeOfNotch);
            backgroundRect.anchoredPosition = position;

            // Fills that stretch with their parent resize automatically; only explicitly
            // sized fills need their width updated to the new segment width.
            RectTransform fillRect = fill.rectTransform;
            if (fillRect.anchorMin.x == fillRect.anchorMax.x)
            {
                Vector2 fillSize = fillRect.sizeDelta;
                fillSize.x = m_SegmentWidth;
                fillRect.sizeDelta = fillSize;
            }
        }

        #endregion
    }
}
