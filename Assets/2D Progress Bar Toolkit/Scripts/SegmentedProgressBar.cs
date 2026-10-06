// -----------------------------------------------------------------------------
// 2D Progress Bar Toolkit
// © University of Games
// -----------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UniversityOfGames.ProgressBarToolkit
{
    /// <summary>
    /// Base class for all segmented progress bars in the toolkit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bar is authored as a single <b>segment template</b>: the first child
    /// <see cref="Image"/> of the component, with its own child <see cref="Image"/>
    /// acting as the fill graphic. On <c>Awake</c> the template is cloned once per
    /// segment and laid out by the concrete bar type; the template itself is then
    /// deactivated. Because segments are plain uGUI images sharing one sprite and
    /// material, a whole bar renders in a single draw call.
    /// </para>
    /// <para><b>Performance.</b> The component is built to disappear from the
    /// profiler: its <c>Update</c> loop runs only while a smoothed value change is
    /// in flight and the component disables itself the moment the target is
    /// reached, so idle bars cost nothing per frame. All per-segment state lives in
    /// plain arrays and no managed allocations happen after initialization.
    /// </para>
    /// <example>
    /// Driving a bar from a loading routine:
    /// <code>
    /// [SerializeField] private CircularProgressBar m_Bar;
    ///
    /// private void OnEnable()  => m_Bar.Completed += OnBarFull;
    /// private void OnDisable() => m_Bar.Completed -= OnBarFull;
    ///
    /// private void ReportProgress(float normalized)
    /// {
    ///     m_Bar.FillAmount = normalized;      // animated when SmoothingSpeed > 0
    /// }
    ///
    /// private void SkipToEnd()
    /// {
    ///     m_Bar.SetFillImmediate(1f);         // bypasses smoothing entirely
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class SegmentedProgressBar : MonoBehaviour
    {
        /// <summary>Controls how the fill is distributed across segments.</summary>
        public enum FillMode
        {
            /// <summary>Segments fill gradually, one after another.</summary>
            Continuous = 0,

            /// <summary>A segment lights up only once the progress fully covers it.</summary>
            WholeSegments = 1
        }

        #region Serialized fields

        [Header("Colors")]
        [Tooltip("Background color applied to every segment.")]
        [FormerlySerializedAs("mainColor")]
        [SerializeField]
        private Color m_MainColor = Color.white;

        [Tooltip("Color applied to the fill graphic of every segment.")]
        [FormerlySerializedAs("fillColor")]
        [SerializeField]
        private Color m_FillColor = Color.green;

        [Tooltip("When enabled, the fill color is sampled from the gradient using the current progress instead of using a constant color.")]
        [SerializeField]
        private bool m_UseFillGradient;

        [Tooltip("Gradient sampled with the current progress when Use Fill Gradient is enabled (e.g. red at 0, green at 1).")]
        [SerializeField]
        private Gradient m_FillGradient = new Gradient();

        [Header("Layout")]
        [Tooltip("Number of segments the bar is split into.")]
        [FormerlySerializedAs("numberOfSegments")]
        [SerializeField, Min(1)]
        private int m_NumberOfSegments = 5;

        [Tooltip("Spacing between neighbouring segments.")]
        [FormerlySerializedAs("sizeOfNotch")]
        [SerializeField, Min(0f)]
        private float m_SizeOfNotch = 5f;

        [Header("Progress")]
        [Tooltip("Normalized progress of the bar (0 = empty, 1 = full).")]
        [FormerlySerializedAs("fillAmount")]
        [SerializeField, Range(0f, 1f)]
        private float m_FillAmount;

        [Tooltip("Continuous fills segments gradually; Whole Segments lights a segment up only once the progress fully covers it.")]
        [SerializeField]
        private FillMode m_FillMode = FillMode.Continuous;

        [Header("Smoothing")]
        [Tooltip("How fast the displayed value follows the target value, in fill units per second. 0 applies changes instantly.")]
        [SerializeField, Min(0f)]
        private float m_SmoothingSpeed;

        [Tooltip("Animate with unscaled time so the bar keeps moving while the game is paused (Time.timeScale = 0).")]
        [SerializeField]
        private bool m_UseUnscaledTime;

        [Header("Events")]
        [Tooltip("Invoked with the displayed value every time it changes.")]
        [SerializeField]
        private UnityEvent<float> m_OnValueChanged = new UnityEvent<float>();

        [Tooltip("Invoked once each time the displayed value reaches 1.")]
        [SerializeField]
        private UnityEvent m_OnCompleted = new UnityEvent();

        #endregion

        #region Runtime state

        private Image m_Template;
        private GameObject[] m_SegmentObjects;
        private Image[] m_SegmentBackgrounds;
        private Image[] m_SegmentFills;
        private float m_VisualFill;
        private bool m_IsBuilt;
        private bool m_CompletedFired;

        #endregion

        #region Events

        /// <summary>Raised with the displayed value every time it changes. C# counterpart of <see cref="OnValueChanged"/>.</summary>
        public event Action<float> ValueChanged;

        /// <summary>Raised once each time the displayed value reaches 1. C# counterpart of <see cref="OnCompleted"/>.</summary>
        public event Action Completed;

        /// <summary>Invoked with the displayed value every time it changes. Wire persistent listeners in the Inspector.</summary>
        public UnityEvent<float> OnValueChanged => m_OnValueChanged;

        /// <summary>Invoked once each time the displayed value reaches 1. Wire persistent listeners in the Inspector.</summary>
        public UnityEvent OnCompleted => m_OnCompleted;

        #endregion

        #region Properties

        /// <summary>
        /// Target progress of the bar in the [0, 1] range. With <see cref="SmoothingSpeed"/>
        /// set to 0 the change is applied instantly, otherwise the displayed value animates
        /// towards the target. Values are clamped.
        /// </summary>
        public float FillAmount
        {
            get => m_FillAmount;
            set
            {
                m_FillAmount = Mathf.Clamp01(value);
                if (!m_IsBuilt)
                {
                    return;
                }

                if (m_SmoothingSpeed > 0f)
                {
                    enabled = true; // Wake the animation loop; it sleeps again on arrival.
                }
                else
                {
                    SetVisualFill(m_FillAmount);
                }
            }
        }

        /// <summary>The value the bar is currently displaying. Trails <see cref="FillAmount"/> while smoothing.</summary>
        public float DisplayedFillAmount => m_VisualFill;

        /// <summary>True while the displayed value is animating towards the target value.</summary>
        public bool IsAnimating => m_IsBuilt && !Mathf.Approximately(m_VisualFill, m_FillAmount);

        /// <summary>True once the segments have been generated (after <c>Awake</c>).</summary>
        public bool IsBuilt => m_IsBuilt;

        /// <summary>Background color applied to every segment. Assigning it recolors the bar immediately.</summary>
        public Color MainColor
        {
            get => m_MainColor;
            set
            {
                m_MainColor = value;
                if (m_Template != null)
                {
                    m_Template.color = value;
                }
                if (m_SegmentBackgrounds != null)
                {
                    for (int i = 0; i < m_SegmentBackgrounds.Length; i++)
                    {
                        m_SegmentBackgrounds[i].color = value;
                    }
                }
            }
        }

        /// <summary>
        /// Fill color applied to every segment. Assigning it recolors the bar immediately.
        /// Ignored while <see cref="UseFillGradient"/> is enabled.
        /// </summary>
        public Color FillColor
        {
            get => m_FillColor;
            set
            {
                m_FillColor = value;
                if (!m_UseFillGradient)
                {
                    ApplyFillColor(value);
                }
            }
        }

        /// <summary>When enabled, the fill color is sampled from <see cref="FillGradient"/> using the displayed value.</summary>
        public bool UseFillGradient
        {
            get => m_UseFillGradient;
            set
            {
                m_UseFillGradient = value;
                ApplyFillColor(value ? m_FillGradient.Evaluate(m_VisualFill) : m_FillColor);
            }
        }

        /// <summary>Gradient sampled with the displayed value when <see cref="UseFillGradient"/> is enabled.</summary>
        public Gradient FillGradient
        {
            get => m_FillGradient;
            set => m_FillGradient = value ?? new Gradient();
        }

        /// <summary>Number of segments the bar is split into. Assigning a new value rebuilds the bar.</summary>
        public int NumberOfSegments
        {
            get => m_NumberOfSegments;
            set
            {
                int count = Mathf.Max(1, value);
                if (count == m_NumberOfSegments)
                {
                    return;
                }

                m_NumberOfSegments = count;
                Rebuild();
            }
        }

        /// <summary>Spacing between neighbouring segments. Assigning a new value rebuilds the bar.</summary>
        public float SizeOfNotch
        {
            get => m_SizeOfNotch;
            set
            {
                float notch = Mathf.Max(0f, value);
                if (Mathf.Approximately(notch, m_SizeOfNotch))
                {
                    return;
                }

                m_SizeOfNotch = notch;
                Rebuild();
            }
        }

        /// <summary>How the fill is distributed across segments.</summary>
        public FillMode Mode
        {
            get => m_FillMode;
            set
            {
                if (value == m_FillMode)
                {
                    return;
                }

                m_FillMode = value;
                if (m_IsBuilt)
                {
                    ApplySegmentFills(m_VisualFill);
                }
            }
        }

        /// <summary>How fast the displayed value follows the target, in fill units per second. 0 disables smoothing.</summary>
        public float SmoothingSpeed
        {
            get => m_SmoothingSpeed;
            set => m_SmoothingSpeed = Mathf.Max(0f, value);
        }

        /// <summary>Animate with unscaled time so the bar keeps moving while the game is paused.</summary>
        public bool UseUnscaledTime
        {
            get => m_UseUnscaledTime;
            set => m_UseUnscaledTime = value;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the target progress and displays it immediately, bypassing smoothing.
        /// Useful for initializing a bar to a known state (e.g. restoring a save).
        /// </summary>
        /// <param name="value">Normalized progress; clamped to [0, 1].</param>
        public void SetFillImmediate(float value)
        {
            m_FillAmount = Mathf.Clamp01(value);
            if (!m_IsBuilt)
            {
                return;
            }

            SetVisualFill(m_FillAmount);
            enabled = false; // Nothing left to animate.
        }

        /// <summary>
        /// Destroys all spawned segments and builds the bar again with the current
        /// settings. Call it after resizing the bar's <see cref="RectTransform"/> or
        /// reconfiguring it through code. No-op before the bar has initialized.
        /// </summary>
        public void Rebuild()
        {
            if (!m_IsBuilt)
            {
                return;
            }

            for (int i = 0; i < m_SegmentObjects.Length; i++)
            {
                Destroy(m_SegmentObjects[i]);
            }

            m_IsBuilt = false;
            BuildSegments();
        }

        #endregion

        #region Unity lifecycle

        private void Awake()
        {
            m_Template = GetComponentInChildren<Image>();
            if (m_Template == null)
            {
                Debug.LogError($"{GetType().Name} requires a child Image to use as a segment template.", this);
                enabled = false;
                return;
            }

            Transform templateTransform = m_Template.transform;
            if (templateTransform.childCount == 0 ||
                templateTransform.GetChild(0).GetComponent<Image>() == null)
            {
                Debug.LogError($"{GetType().Name} requires the segment template to contain a child Image used as the fill graphic.", this);
                enabled = false;
                return;
            }

            m_Template.color = m_MainColor;
            m_Template.gameObject.SetActive(false);

            BuildSegments();
        }

        private void Update()
        {
            // Only runs while a smoothed change is in flight; BuildSegments and
            // SetFillImmediate disable the component whenever the bar is at rest.
            float deltaTime = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float next = Mathf.MoveTowards(m_VisualFill, m_FillAmount, m_SmoothingSpeed * deltaTime);
            SetVisualFill(next);

            if (Mathf.Approximately(next, m_FillAmount))
            {
                enabled = false;
            }
        }

        protected virtual void OnValidate()
        {
            m_NumberOfSegments = Mathf.Max(1, m_NumberOfSegments);
            m_SizeOfNotch = Mathf.Max(0f, m_SizeOfNotch);
            m_FillAmount = Mathf.Clamp01(m_FillAmount);

            if (m_IsBuilt)
            {
                if (m_SmoothingSpeed > 0f)
                {
                    enabled = true;
                }
                else
                {
                    SetVisualFill(m_FillAmount);
                }
            }
        }

        #endregion

        #region Bar type contract

        /// <summary>
        /// Called once before the segments are instantiated. Use it to cache any values
        /// the layout pass depends on, e.g. the size of a single segment.
        /// </summary>
        /// <param name="segmentCount">The number of segments about to be created (always ≥ 1).</param>
        protected abstract void OnBeforeBuild(int segmentCount);

        /// <summary>Positions and sizes a single freshly instantiated segment.</summary>
        /// <param name="background">The segment's background image (the template clone).</param>
        /// <param name="fill">The segment's fill image (first child of the background).</param>
        /// <param name="index">Zero-based index of the segment.</param>
        protected abstract void LayoutSegment(Image background, Image fill, int index);

        /// <summary>
        /// The <see cref="Image.fillAmount"/> of a fully covered segment. Radial bars
        /// override this with the arc fraction of a single segment so a segment's fill
        /// never paints outside the segment itself.
        /// </summary>
        protected virtual float SegmentFillScale => 1f;

        #endregion

        #region Internals

        private void BuildSegments()
        {
            int segmentCount = Mathf.Max(1, m_NumberOfSegments);
            OnBeforeBuild(segmentCount);

            m_SegmentObjects = new GameObject[segmentCount];
            m_SegmentBackgrounds = new Image[segmentCount];
            m_SegmentFills = new Image[segmentCount];

            Color fillColor = m_UseFillGradient ? m_FillGradient.Evaluate(m_FillAmount) : m_FillColor;
            GameObject templateObject = m_Template.gameObject;
            Transform parent = transform;

            for (int i = 0; i < segmentCount; i++)
            {
                GameObject segment = Instantiate(templateObject, parent, false);
                segment.SetActive(true);

                Image background = segment.GetComponent<Image>();
                Image fill = background.transform.GetChild(0).GetComponent<Image>();
                fill.color = fillColor;

                m_SegmentObjects[i] = segment;
                m_SegmentBackgrounds[i] = background;
                m_SegmentFills[i] = fill;

                LayoutSegment(background, fill, i);
            }

            m_IsBuilt = true;

            // Show the serialized value right away; events are not raised for this initial state.
            m_VisualFill = m_FillAmount;
            m_CompletedFired = m_FillAmount >= 1f;
            ApplySegmentFills(m_VisualFill);

            // Idle by default — Update only runs while a smoothed change is in flight.
            enabled = false;
        }

        private void SetVisualFill(float value)
        {
            if (Mathf.Approximately(value, m_VisualFill))
            {
                return;
            }

            m_VisualFill = value;
            ApplySegmentFills(value);

            if (m_UseFillGradient)
            {
                ApplyFillColor(m_FillGradient.Evaluate(value));
            }

            m_OnValueChanged.Invoke(value);
            ValueChanged?.Invoke(value);

            if (value >= 1f)
            {
                if (!m_CompletedFired)
                {
                    m_CompletedFired = true;
                    m_OnCompleted.Invoke();
                    Completed?.Invoke();
                }
            }
            else
            {
                m_CompletedFired = false;
            }
        }

        private void ApplySegmentFills(float visualFill)
        {
            // 'covered - i' is how much of segment i the progress covers, in [0, 1];
            // the scale maps that coverage onto the segment's own fill arc so a fill
            // never paints outside its segment.
            float covered = visualFill * m_SegmentFills.Length;
            float scale = SegmentFillScale;

            if (m_FillMode == FillMode.Continuous)
            {
                for (int i = 0; i < m_SegmentFills.Length; i++)
                {
                    m_SegmentFills[i].fillAmount = Mathf.Clamp01(covered - i) * scale;
                }
            }
            else
            {
                // Whole segments: a segment is either fully lit or fully dark.
                for (int i = 0; i < m_SegmentFills.Length; i++)
                {
                    m_SegmentFills[i].fillAmount = covered - i >= 1f ? scale : 0f;
                }
            }
        }

        private void ApplyFillColor(Color color)
        {
            if (m_SegmentFills == null)
            {
                return;
            }

            for (int i = 0; i < m_SegmentFills.Length; i++)
            {
                m_SegmentFills[i].color = color;
            }
        }

        #endregion
    }
}
