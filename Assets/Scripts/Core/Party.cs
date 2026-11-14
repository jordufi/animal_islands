using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The local players of the next run (split-screen lobby of the main menu). Each member has its own
// devices (one gamepad, or keyboard + mouse) and animal. Empty = solo: one player with every device and
// GameSettings.Animal. Static, so it survives loading the map scene (and Restart).
public static class Party
{
    public const int MaxPlayers = 4;

    public class Member
    {
        public string animal;
        public InputDevice[] devices;   // null = every device (solo)
        public string Label => devices == null || devices.Length == 0 ? "Todos" : devices[0] is Gamepad ? "Mando" : "Teclado";
    }

    static readonly List<Member> members = new List<Member>();
    public static IReadOnlyList<Member> Members => members;
    public static int Count => members.Count;
    public static bool IsSplitScreen => members.Count > 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => members.Clear();   // Enter Play Mode without domain reload keeps statics

    public static void Clear() => members.Clear();

    public static Member Add(string animal, params InputDevice[] devices)
    {
        if (members.Count >= MaxPlayers) return null;
        var m = new Member { animal = animal, devices = devices != null && devices.Length > 0 ? devices : null };
        members.Add(m);
        return m;
    }

    public static void Remove(Member m) => members.Remove(m);

    public static Member WithDevice(InputDevice d) => members.Find(m => m.devices != null && System.Array.IndexOf(m.devices, d) >= 0);

    // Members whose devices were unplugged are dropped.
    public static void Prune() => members.RemoveAll(m => m.devices != null && System.Array.Exists(m.devices, d => d == null || !d.added));
}
