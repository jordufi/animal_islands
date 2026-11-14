using UnityEngine;

// Smooth movement of something another machine simulates (a remote player, a puppet old man): keeps the
// last snapshots (sender's clock) and shows the state `Delay` seconds in the past, interpolating between
// the two snapshots around that moment. Our estimate of the sender's clock follows the newest snapshot.
public class NetInterp
{
    public const float Delay = 0.12f;   // a bit over two snapshots at 20 Hz (internet jitter)

    struct Snap { public float t; public Vector3 p; public Quaternion r; }

    readonly Snap[] buf = new Snap[12];
    int count;
    float clock;
    bool started;

    public bool HasData => count > 0;
    public float LastTime => count > 0 ? buf[count - 1].t : 0f;

    public void Add(float t, Vector3 p, Quaternion r)
    {
        if (count > 0 && t <= buf[count - 1].t) return;   // old / duplicate
        if (count == buf.Length) { System.Array.Copy(buf, 1, buf, 0, buf.Length - 1); count--; }
        buf[count++] = new Snap { t = t, p = p, r = r };
        if (!started) { clock = t - Delay; started = true; }
    }

    // Teleport (respawn, warp): forget the old samples.
    public void Reset() { count = 0; started = false; }

    public bool Sample(float dt, out Vector3 p, out Quaternion r)
    {
        p = Vector3.zero;
        r = Quaternion.identity;
        if (count == 0) return false;
        clock += dt;
        float target = buf[count - 1].t - Delay;
        float err = target - clock;
        if (Mathf.Abs(err) > 0.75f) clock = target;     // lag spike / long pause: jump
        else clock += err * Mathf.Min(1f, dt * 2f);     // drift towards the newest snapshot

        if (clock <= buf[0].t) { p = buf[0].p; r = buf[0].r; return true; }
        for (int i = 0; i < count - 1; i++)
        {
            var a = buf[i];
            var b = buf[i + 1];
            if (clock > b.t) continue;
            float k = Mathf.InverseLerp(a.t, b.t, clock);
            // a long gap means a teleport: don't slide across the map
            if ((b.p - a.p).sqrMagnitude > 25f) { p = k < 0.5f ? a.p : b.p; r = k < 0.5f ? a.r : b.r; }
            else { p = Vector3.Lerp(a.p, b.p, k); r = Quaternion.Slerp(a.r, b.r, k); }
            return true;
        }
        // past the newest snapshot: a short extrapolation, then hold
        var last = buf[count - 1];
        if (count >= 2)
        {
            var prev = buf[count - 2];
            float over = Mathf.Min(clock - last.t, 0.15f);
            float span = Mathf.Max(0.01f, last.t - prev.t);
            Vector3 v = (last.p - prev.p) / span;
            if (v.sqrMagnitude < 100f) { p = last.p + v * over; r = last.r; return true; }
        }
        p = last.p;
        r = last.r;
        return true;
    }
}
