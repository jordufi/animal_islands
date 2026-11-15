// Speeds up filler stretches of a play test (waiting for a chase, a round to run out). Rules (Docs/FASE2_TRASPASO.md 3b):
//  - Fast() only around waits that do not measure timing or fine physics. NEVER around: seconds in sight of a spawn,
//    revive, bleed-out, reload, melee lunge, collision walks, and anything of OnlinePlayTest (the client runs at x1).
//  - Every test calls Normal() when it finishes or fails (its Finish()), so the editor never keeps a fast clock.
//  - A failure that happened at x3 is repeated at x1 before it counts as a failure.
using UnityEngine;

public static class TestClock
{
    public const float FastScale = 3f;

    public static void Fast() { Time.timeScale = FastScale; }
    public static void Normal() { Time.timeScale = 1f; }
}
