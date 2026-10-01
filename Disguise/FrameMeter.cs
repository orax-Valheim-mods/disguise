#nullable enable
using System.Globalization;

namespace Disguise;

/// <summary>
/// Frame time meter behind "disguise perf".
///
/// It samples Time.unscaledDeltaTime once per frame (a handful of float operations, no
/// allocation, no Unity call beyond reading the property) and reports the window accumulated
/// since the previous report. That window is what tells apart "this option feels slower" from
/// what it actually costs: an own view, a disguise toggle or a mod conflict are only
/// comparable when measured the same way.
///
/// Every report resets the window, so the protocol is: set up the state you want to measure,
/// leave it alone for a few seconds, run "disguise perf", then switch state and repeat.
/// </summary>
internal static class FrameMeter
{
    private static int s_frames;
    private static double s_seconds;
    private static float s_min = float.MaxValue;
    private static float s_max;

    /// <summary>Called once per frame from Disguise.Update, always - the meter is never cold.</summary>
    internal static void Sample(float unscaledDeltaTime)
    {
        if (unscaledDeltaTime <= 0f)
        {
            return;
        }
        s_frames++;
        s_seconds += unscaledDeltaTime;
        if (unscaledDeltaTime < s_min)
        {
            s_min = unscaledDeltaTime;
        }
        if (unscaledDeltaTime > s_max)
        {
            s_max = unscaledDeltaTime;
        }
    }

    /// <summary>
    /// Statistics covering the last window - which this call resets - or null when the window
    /// holds no frame.
    /// </summary>
    internal static string? Report()
    {
        if (s_frames == 0 || s_seconds <= 0d)
        {
            Reset();
            return null;
        }

        double averageMs = s_seconds / s_frames * 1000d;
        double framesPerSecond = s_frames / s_seconds;
        string report = string.Format(
            CultureInfo.InvariantCulture,
            "{0} frames in {1:0.00} s - avg {2:0.00} ms, min {3:0.00} ms, max {4:0.00} ms ({5:0.0} FPS)",
            s_frames,
            s_seconds,
            averageMs,
            s_min * 1000f,
            s_max * 1000f,
            framesPerSecond
        );
        Reset();
        return report;
    }

    private static void Reset()
    {
        s_frames = 0;
        s_seconds = 0d;
        s_min = float.MaxValue;
        s_max = 0f;
    }
}
