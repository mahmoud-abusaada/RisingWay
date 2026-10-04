// The game's sound for a recorded clip (VideoRecorder). A clip is rendered frame by frame on a
// fixed time step, far slower than real time, while Unity's audio plays in real time: what the
// speakers play while recording has nothing to do with the clip. So instead the sound is written
// down on the clip's own clock - every one-shot as it is played (SoundManager.shot), and once a
// frame the state of every playing source (its clip, volume, pitch, muffling) - and
// Tools/Social/mix_audio.py plays the same sound files back to that score, in step with the
// picture.
//
// Output: <clip folder>/sound.jsonl, one JSON object a line:
//   {"t":1.25,"shot":"Assets/...wav","src":"Sfx","vol":0.8,"pitch":1,"cut":22000,"q":1}
//   {"t":1.25,"frame":[{"src":"Run Bed#123","clip":"Assets/...wav","vol":0.5,"pitch":1.1,"loop":true,"cut":2000,"q":1,"pos":0.31}, ...]}
// t is seconds into the clip; pos is the source's own place in its clip (seconds), used only
// when it starts.
//
// Editor only: the clips' file paths come from the AssetDatabase.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class SoundLog
{
    private static StreamWriter writer;
    private static float now;
    private static readonly StringBuilder sb = new StringBuilder();

    public static bool On => writer != null;

    public static void Begin(string dir)
    {
        End();
        writer = new StreamWriter(Path.Combine(dir, "sound.jsonl"), false, new UTF8Encoding(false));
        now = 0f;
    }

    /// <summary>The clip's clock: seconds since its first frame.</summary>
    public static void SetTime(float seconds) { now = seconds; }

    public static void Shot(AudioSource source, AudioClip clip, float scale)
    {
        if (writer == null || clip == null || source == null)
            return;
        string path = UnityEditor.AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path))
            return;
        sb.Clear();
        sb.Append("{\"t\":").Append(F(now)).Append(",\"shot\":\"").Append(path).Append("\",\"src\":\"").Append(source.name)
          .Append("\",\"vol\":").Append(F(Loudness(source) * scale)).Append(",\"pitch\":").Append(F(source.pitch));
        Filter(source);
        sb.Append('}');
        writer.WriteLine(sb.ToString());
    }

    /// <summary>The source was stopped: its one-shots still sounding stop too.</summary>
    public static void Stop(AudioSource source)
    {
        if (writer == null || source == null)
            return;
        writer.WriteLine("{\"t\":" + F(now) + ",\"stop\":\"" + source.name + "\"}");
    }

    /// <summary>Once a frame: every source that is playing a clip of its own.</summary>
    public static void Frame()
    {
        if (writer == null)
            return;
        sb.Clear();
        sb.Append("{\"t\":").Append(F(now)).Append(",\"listener\":").Append(F(AudioListener.pause ? 0f : AudioListener.volume)).Append(",\"frame\":[");
        bool first = true;
        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (s.clip == null || !s.isPlaying || !s.isActiveAndEnabled)
                continue;
            string path = UnityEditor.AssetDatabase.GetAssetPath(s.clip);
            if (string.IsNullOrEmpty(path))
                continue;
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append("{\"src\":\"").Append(s.name).Append('#').Append(s.GetEntityId().ToString()).Append("\",\"clip\":\"").Append(path)
              .Append("\",\"vol\":").Append(F(Loudness(s))).Append(",\"pitch\":").Append(F(s.pitch))
              .Append(",\"loop\":").Append(s.loop ? "true" : "false").Append(",\"pos\":").Append(F(s.time));
            Filter(s);
            sb.Append('}');
        }
        sb.Append("]}");
        writer.WriteLine(sb.ToString());
    }

    public static void End()
    {
        if (writer == null)
            return;
        writer.Flush();
        writer.Dispose();
        writer = null;
    }

    private static float Loudness(AudioSource s) => s.mute ? 0f : s.volume;

    private static void Filter(AudioSource s)
    {
        AudioLowPassFilter lp = s.GetComponent<AudioLowPassFilter>();
        if (lp != null && lp.enabled)
            sb.Append(",\"cut\":").Append(F(lp.cutoffFrequency)).Append(",\"q\":").Append(F(lp.lowpassResonanceQ));
    }

    private static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
}
#endif
