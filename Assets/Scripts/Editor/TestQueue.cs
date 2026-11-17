using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Runs several play tests one after another (each starts when the previous one has finished and play mode is off) and
// writes one summary: OK / FAIL counts per test and every FAIL / EXCEPTION line. EditorPrefs TestQueue.Log / .Status.
// TestQueue.Run("Smoke", "Base", "Coop") — names in Tests below.
public static class TestQueue
{
    const string Key = "TestQueue.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    // name -> (EditorPrefs prefix of the test, how to start it)
    static readonly Dictionary<string, (string prefix, System.Action start)> Tests = new Dictionary<string, (string, System.Action)>
    {
        { "Smoke", ("SmokePlayTest.", SmokePlayTest.Run) },
        { "SmokeAlturas", ("SmokePlayTest.", SmokePlayTest.RunAlturas) },
        { "SmokeVenecia", ("SmokePlayTest.", SmokePlayTest.RunVenecia) },
        { "Phase1", ("Phase1PlayTest.", Phase1PlayTest.Run) },
        { "Base", ("BasePlayTest.", BasePlayTest.Run) },
        { "Feedback", ("FeedbackPlayTest.", FeedbackPlayTest.Run) },
        { "IslaZombie", ("IslaZombiePlayTest.", IslaZombiePlayTest.Run) },
        { "Coop", ("CoopPlayTest.", CoopPlayTest.Run) },
        { "Venecia", ("VeneciaPlayTest.", VeneciaPlayTest.Run) },
        { "Villajubilada", ("VillajubiladaPlayTest.", VillajubiladaPlayTest.Run) },
        { "Climb", ("ClimbPlayTest.", ClimbPlayTest.Run) },
        { "ClimbVillajubilada", ("ClimbPlayTest.", ClimbPlayTest.RunVillajubilada) },
        { "ClimbVenecia", ("ClimbPlayTest.", ClimbPlayTest.RunVenecia) },
        { "Shots", ("Feedback1Shots.", Feedback1Shots.Run) },
        { "ShotsAlturas", ("Feedback1Shots.", Feedback1Shots.RunAlturas) },
    };

    static List<string> queue = new List<string>();
    static string current;
    static double startedAt;

    public static void Run(params string[] names)
    {
        queue = names.ToList();
        current = null;
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");

    static void Tick()
    {
        if (current != null)
        {
            var (prefix, _) = Tests[current];
            string st = EditorPrefs.GetString(prefix + "Status", "");
            bool timedOut = EditorApplication.timeSinceStartup - startedAt > 900;
            if ((st == "running" || EditorApplication.isPlaying) && !timedOut) return;
            if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
            var log = EditorPrefs.GetString(prefix + "Log", "").Split('\n');
            int ok = log.Count(l => l.StartsWith("OK")), fail = log.Count(l => l.StartsWith("FAIL"));
            Write($"{current}: {st}{(timedOut ? " (TIMEOUT)" : "")}, {ok} OK / {fail} FAIL, {EditorApplication.timeSinceStartup - startedAt:0} s");
            foreach (var l in log.Where(l => l.StartsWith("FAIL") || l.StartsWith("EXCEPTION"))) Write("   " + l);
            current = null;
        }
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (queue.Count == 0)
        {
            EditorPrefs.SetString(Key + "Status", "done");
            EditorApplication.update -= Tick;
            return;
        }
        current = queue[0];
        queue.RemoveAt(0);
        if (!Tests.ContainsKey(current)) { Write(current + ": unknown test"); current = null; return; }
        startedAt = EditorApplication.timeSinceStartup;
        Tests[current].start();
    }
}
