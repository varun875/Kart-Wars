# 2D Progress Bar Toolkit

A lightweight, dependency-free set of segmented progress bars for Unity uGUI.
Compatible with Unity 6000.5 and newer.

## Package contents

| Folder | Contents |
| --- | --- |
| `Demo` | `Showcase` scene with every bar style and a keyboard-driven controller. |
| `Prefabs` | Ready-to-use progress bar prefabs — drop them into any Canvas. |
| `Scripts` | Runtime components (`UniversityOfGames.ProgressBarToolkit` assembly). |
| `Sprites/Core` | White base sprites used by the prefabs (tinted at runtime). |
| `Sprites/Themes` | Decorative bar skins with layered PNGs and source files. |
| `Documentation` | This guide. |

## Quick start

1. Open the `Demo/Showcase` scene — it instantiates **every shipped prefab** so
   each one can be exercised and verified:
   - **Space** — toggle auto-play (a new target value is picked every few seconds)
   - **Tab** or **1–8** — select a single bar / all bars
   - **Left / Right** — adjust the value of the selection
   - **R** — reset the selection to zero
2. Drag a prefab from `Prefabs` into your own Canvas.
3. Adjust the bar in the Inspector:

| Parameter | Description |
| --- | --- |
| **Main Color** | Background color of every segment. |
| **Fill Color** | Fill color of every segment. |
| **Use Fill Gradient / Fill Gradient** | Sample the fill color from a gradient using the current progress (e.g. red at 0, green at 1). |
| **Number Of Segments** | How many progression steps the bar has. |
| **Size Of Notch** | Spacing between neighbouring segments. |
| **Fill Amount** | Normalized progress (0 = empty, 1 = full). |
| **Fill Mode** | *Continuous* fills segments gradually; *Whole Segments* lights a segment up only once the progress fully covers it. |
| **Smoothing Speed** | How fast the displayed value follows the target, in fill units per second. 0 applies changes instantly. |
| **Use Unscaled Time** | Keep animating while the game is paused (`Time.timeScale = 0`). |
| **On Value Changed / On Completed** | Inspector events raised when the displayed value changes / reaches 1. |

Circular bars additionally expose **Start Angle** and **End Angle** (in degrees),
which define the arc the segments are laid out on.

## Prefabs

Every prefab is fully self-contained and resolution-independent — the segment
template stretches with the prefab root, so you can drop a prefab into any
Canvas and simply resize its `RectTransform`.

| Prefab | Description |
| --- | --- |
| `CircularProgressBar_180` | Top gauge covering a 180° arc, 5 segments. |
| `CircularProgressBar_270` | Dial covering a 270° arc, 8 segments. |
| `CircularProgressBar_360` | Full ring, 10 segments. |
| `CircularProgressBar_Solid` | Smooth, unsegmented ring. |
| `CircularProgressBar_Steps` | Full ring in *Whole Segments* mode — a discrete stage indicator. |
| `ClassicProgressBar` | Horizontal bar, 10 segments. |
| `ClassicProgressBar_Solid` | Smooth, unsegmented horizontal bar. |
| `ClassicProgressBar_Gradient` | Horizontal bar whose fill color follows a red→yellow→green gradient. |

## Scripting

All components live in the `UniversityOfGames.ProgressBarToolkit` namespace:

```csharp
using UniversityOfGames.ProgressBarToolkit;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private CircularProgressBar m_ProgressBar;

    private void OnEnable()
    {
        m_ProgressBar.Completed += OnLoadingBarFull;
    }

    private void OnDisable()
    {
        m_ProgressBar.Completed -= OnLoadingBarFull;
    }

    public void OnLoadingProgress(float normalizedProgress)
    {
        m_ProgressBar.FillAmount = normalizedProgress; // clamped to [0, 1]
    }

    private void OnLoadingBarFull() { /* ... */ }
}
```

Key API surface (see the XML documentation for details):

- `FillAmount` — target progress; animates when `SmoothingSpeed > 0`, otherwise applies instantly.
- `SetFillImmediate(float)` — set and display a value instantly, bypassing smoothing.
- `DisplayedFillAmount` — the value currently shown (trails the target while smoothing).
- `IsAnimating`, `IsBuilt` — runtime state queries.
- `MainColor`, `FillColor`, `UseFillGradient`, `FillGradient` — colors, applied to all segments immediately.
- `NumberOfSegments`, `SizeOfNotch`, `StartAngle`, `EndAngle` — layout; setters rebuild the bar.
- `Rebuild()` — regenerate segments after resizing the bar at runtime.
- `ValueChanged` / `Completed` — C# events; `OnValueChanged` / `OnCompleted` — UnityEvents.

## Performance

The components are designed to disappear from the profiler:

- **No per-frame cost while idle.** The `Update` loop only runs while a smoothed
  change is in flight and the component disables itself as soon as the target is
  reached. Bars that are not being animated cost nothing.
- **Allocation-free after initialization.** Segments are stored in plain arrays;
  no allocations happen when values, colors or gradients change.
- **Batching-friendly.** All segments of a bar share one sprite and material, so
  a whole bar renders in a single draw call; `Raycast Target` is disabled on the
  demo graphics so bars never participate in UI raycasts.

## Creating a custom bar

1. Create an empty UI object with a `RectTransform` inside a Canvas.
2. Add a child `Image` — this is the segment template (for circular bars set its
   Image Type to *Filled / Radial 360*).
3. Add a child `Image` under the template — this is the fill graphic
   (*Filled / Radial 360* for circular bars, *Filled / Horizontal* for classic ones).
4. Attach `ClassicProgressBar` or `CircularProgressBar` to the root object and
   configure it in the Inspector. Segments are generated automatically at runtime.

## Support

**University of Games** is a place for indie creators. We ship practical Unity
solutions that speed up game development — tools, ready-made packages, and
knowledge you can apply the same day. Everything comes from real production
experience.

Full documentation for this package and our other products lives on GitBook:

- **Docs home (About):** https://university-of-games.gitbook.io/welcome/
- **Community & channels:** https://university-of-games.gitbook.io/welcome/community
- **Unity Asset Store (publisher):** https://assetstore.unity.com/publishers/25633
- **Medium articles:** https://medium.com/university-of-games
