// -----------------------------------------------------------------------------
// 2D Progress Bar Toolkit
// © University of Games
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

namespace UniversityOfGames.ProgressBarToolkit.Demo
{
    /// <summary>
    /// Drives every progress bar in the showcase scene so each shipped prefab can be
    /// exercised and verified interactively.
    /// </summary>
    /// <remarks>
    /// Controls:
    /// <list type="bullet">
    /// <item><b>Space</b> — toggle auto-play (a new target value is picked every few seconds).</item>
    /// <item><b>Tab</b> — cycle the selection: all bars, then each bar individually.</item>
    /// <item><b>1–9</b> — select a single bar directly.</item>
    /// <item><b>Left / Right</b> — decrease / increase the value of the selection.</item>
    /// <item><b>R</b> — reset the selection to zero.</item>
    /// </list>
    /// The status label is only rebuilt when the displayed percentage or the selection
    /// actually changes, so the controller allocates nothing while the scene is idle.
    /// </remarks>
    public sealed class ProgressBarShowcase : MonoBehaviour
    {
        /// <summary>Sentinel selection index meaning "control every bar at once".</summary>
        private const int SelectAll = -1;

        [Tooltip("Progress bars driven by the showcase, in selection order.")]
        [SerializeField]
        private SegmentedProgressBar[] m_Bars;

        [Tooltip("Label displaying the current selection and its displayed value.")]
        [SerializeField]
        private Text m_StatusLabel;

        [Tooltip("Seconds between automatically picked target values.")]
        [SerializeField, Min(0.5f)]
        private float m_StepInterval = 2.5f;

        [Tooltip("How much the arrow keys change the value per press.")]
        [SerializeField, Range(0.01f, 0.5f)]
        private float m_ManualStep = 0.1f;

        private bool m_AutoPlay = true;
        private float m_NextStepTime;
        private float m_TargetValue;
        private int m_Selected = SelectAll;
        private int m_LastShownPercent = -1;
        private int m_LastShownSelection = int.MinValue;

        private void Start()
        {
            m_NextStepTime = Time.unscaledTime + m_StepInterval;
            ApplyTarget(0.65f);
        }

        private void Update()
        {
            HandleInput();

            if (m_AutoPlay && Time.unscaledTime >= m_NextStepTime)
            {
                m_NextStepTime = Time.unscaledTime + m_StepInterval;
                ApplyTarget(Random.value);
            }

            RefreshStatusLabel();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_AutoPlay = !m_AutoPlay;
                m_NextStepTime = Time.unscaledTime + m_StepInterval;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Cycle: all bars -> bar 0 -> bar 1 -> ... -> all bars.
                m_Selected = m_Selected + 1 >= m_Bars.Length ? SelectAll : m_Selected + 1;
            }

            for (int i = 0; i < m_Bars.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    m_Selected = i;
                }
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                m_AutoPlay = false;
                ApplyTarget(m_TargetValue + m_ManualStep);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                m_AutoPlay = false;
                ApplyTarget(m_TargetValue - m_ManualStep);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                m_AutoPlay = false;
                ApplyTarget(0f);
            }
        }

        /// <summary>Sets the target value on the current selection (a single bar or all of them).</summary>
        private void ApplyTarget(float value)
        {
            m_TargetValue = Mathf.Clamp01(value);

            if (m_Selected == SelectAll)
            {
                for (int i = 0; i < m_Bars.Length; i++)
                {
                    m_Bars[i].FillAmount = m_TargetValue;
                }
            }
            else
            {
                m_Bars[m_Selected].FillAmount = m_TargetValue;
            }
        }

        /// <summary>Rebuilds the status text only when the percentage or selection changed.</summary>
        private void RefreshStatusLabel()
        {
            if (m_StatusLabel == null || m_Bars.Length == 0)
            {
                return;
            }

            SegmentedProgressBar observed = m_Selected == SelectAll ? m_Bars[0] : m_Bars[m_Selected];
            int percent = Mathf.RoundToInt(observed.DisplayedFillAmount * 100f);

            if (percent == m_LastShownPercent && m_Selected == m_LastShownSelection)
            {
                return;
            }

            m_LastShownPercent = percent;
            m_LastShownSelection = m_Selected;

            string subject = m_Selected == SelectAll ? "All bars" : observed.gameObject.name;
            m_StatusLabel.text = subject + " — " + percent + "%";
        }
    }
}
