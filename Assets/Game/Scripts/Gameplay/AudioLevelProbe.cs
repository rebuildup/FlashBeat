using UnityEngine;

/// <summary>
/// Measures the level of the final audio mix. Attach it to the GameObject that
/// carries the AudioListener (done at runtime by MusicManager) and read
/// <see cref="LastRms"/> to prove that the song is actually audible - the player
/// log then carries the evidence for the exhibition build.
/// </summary>
public class AudioLevelProbe : MonoBehaviour
{
    float lastRms;
    float peakRms;

    /// <summary>Root mean square of the most recent audio buffer (0 = silence).</summary>
    public float LastRms => lastRms;

    /// <summary>Highest RMS seen since the last <see cref="ResetPeak"/>.</summary>
    public float PeakRms => peakRms;

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (data == null || data.Length == 0) return;

        double sum = 0.0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += (double)data[i] * data[i];
        }

        lastRms = Mathf.Sqrt((float)(sum / data.Length));
        if (lastRms > peakRms) peakRms = lastRms;
    }

    /// <summary>Resets the peak counter (call it right before the song starts).</summary>
    public void ResetPeak()
    {
        peakRms = 0f;
        lastRms = 0f;
    }
}
