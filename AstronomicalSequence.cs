using System;

namespace AstronomicalEvent
{
    // Section boundaries of the selected event (the built-in one comes from analysing its music). The performer's baked choreography is authored against
    // the same clock; Rescale() only stretches the timeline when a different-length track is supplied.
    internal static class AstronomicalSequence
    {
        public static string[] Names =
        {
            "Intro",
            "Stage & Environment",
            "Energy Surge",
            "Moon Rise",
            "Cosmic Environment",
            "Underwater Environment",
            "Finale",
        };
        public static float Lead = 28f; // seconds of the music before the choreography starts
        public static float MusicLength = 500.952f + 28f; // expected music length (8:49 for the built-in event)
        static float[] Bounds = { 0f, 55.5f, 120f, 190.5f, 256f, 376.5f, 477.5f, 528.952f };
        public static float[] Duration = new float[7];
        public static float[] Start = new float[7];
        public static float Total;

        static AstronomicalSequence()
        {
            Rescale(0);
        }

        // Only while idle: makes the selected event's timings current.
        public static void Configure(AstronomicalEventDef d)
        {
            if (d == null || d.Bounds == null || d.Bounds.Length < 8)
                return;
            int n = d.Bounds.Length - 1;
            Bounds = (float[])d.Bounds.Clone();
            Lead = d.Lead;
            MusicLength = d.Bounds[n];
            if (d.SeqNames != null && d.SeqNames.Length == n)
                Names = d.SeqNames;
            else
            {
                Names = new string[n];
                for (int k = 0; k < n; k++)
                    Names[k] = "Part " + (k + 1);
            }
            Rescale(0);
        }

        // A track within a few seconds of the expected length (decoder padding) keeps the exact boundaries.
        public static void Rescale(float totalSeconds)
        {
            float k = totalSeconds > 30f && Math.Abs(totalSeconds - MusicLength) > 6f ? totalSeconds / MusicLength : 1f;
            int n = Bounds.Length - 1;
            if (Start.Length != n)
            {
                Start = new float[n];
                Duration = new float[n];
            }
            for (int i = 0; i < n; i++)
            {
                Start[i] = Bounds[i] * k;
                Duration[i] = (Bounds[i + 1] - Bounds[i]) * k;
            }
            Total = MusicLength * k;
        }

        // Time on the choreography clock for event time t (identical unless the timeline was stretched to another track).
        public static float ChoreoTime(float t)
        {
            float k = Total > 1f ? MusicLength / Total : 1f;
            return Math.Max(0f, t * k - Lead);
        }

        public static int IndexAt(float t, out float u)
        {
            int i = Duration.Length - 1;
            for (int k = 0; k < Duration.Length; k++)
                if (t < Start[k] + Duration[k])
                {
                    i = k;
                    break;
                }
            u = Math.Max(0f, Math.Min(1f, (t - Start[i]) / Duration[i]));
            return i;
        }
    }
}
