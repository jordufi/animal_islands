using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Setup report: long straight sight lines (user feedback 1: "in Venice you aim from the Carnevale all the way to the
// Ferrovia"). A line runs along a row, a column or a diagonal of the layout through cells you see through (floor, water,
// doors, low cover, markers) and stops at anything tall (buildings, rocks, tall props, trees...) or where the floor
// level changes (a 2 m step or a humped bridge hides the ground behind it; ramps are see-through). Only lines with a
// standing spot at both ends count. Not counted: lines inside a zone flagged `ancha` (the one wide place of a map, the
// Piazza) and lines that are mostly water (looking along a canal from a bridge).
// Reference (2026-11-18): Fiestas de Villajubilada, the map the friends liked most, has its longest line at 60 m and
// most under 30 m; Isla del Faro 34 m.
public static partial class IslaZombieSetup
{
    const float SightWarn = 40f;   // m: a WARN line in the report from here
    const float SightCount = 30f;  // m: lines this long are counted

    static readonly string SeeThroughMarkers = ".x123456789+@sBDPKJbXLGUMEYTCNSFOI%&Q:";

    static IEnumerable<string> CheckSightLines(Layout L)
    {
        if (L.Multi) yield break;
        bool See(int c, int r)
        {
            char ch = L.At(c, r);
            return L.Zone(ch) != null || SeeThroughMarkers.IndexOf(ch) >= 0;
        }
        bool Stand(int c, int r)
        {
            char ch = L.At(c, r);
            return ch != '.' && !IsDoor(ch) && See(c, r);
        }
        int Lv(int c, int r) => L.HasHeights && L.InGrid(c, r) ? L.level[c, r] : 0;
        bool Ramp(int c, int r) => L.HasHeights && L.IsRamp(c, r);

        var lines = new List<(float m, Vector2Int a, Vector2Int b, string zones)>();
        void Close(List<Vector2Int> seg)
        {
            int i0 = 0, i1 = seg.Count - 1;
            while (i0 <= i1 && !Stand(seg[i0].x, seg[i0].y)) i0++;
            while (i1 >= i0 && !Stand(seg[i1].x, seg[i1].y)) i1--;
            if (i1 <= i0) { seg.Clear(); return; }
            var a = seg[i0]; var b = seg[i1];
            int n = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) + 1;
            float m = n * Cell * (a.x != b.x && a.y != b.y ? 1.414f : 1f);
            var part = seg.GetRange(i0, i1 - i0 + 1);
            int water = part.Count(p => L.At(p.x, p.y) == '.');
            var zs = new string(part.Select(p => L.At(p.x, p.y)).Where(ch => L.Zone(ch) != null).Distinct().OrderBy(ch => ch).ToArray());
            bool wideOnly = zs.Length == 1 && L.Zone(zs[0]).wide;
            if (m >= SightCount && !wideOnly && water * 2 <= part.Count) lines.Add((m, a, b, zs));
            seg.Clear();
        }

        var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1) };
        foreach (var d in dirs)
        {
            var starts = new List<Vector2Int>();
            if (d.y == 0) for (int r = 0; r < L.H; r++) starts.Add(new Vector2Int(0, r));
            else if (d.x == 0) for (int c = 0; c < L.W; c++) starts.Add(new Vector2Int(c, 0));
            else if (d.y == 1) { for (int c = 0; c < L.W; c++) starts.Add(new Vector2Int(c, 0)); for (int r = 1; r < L.H; r++) starts.Add(new Vector2Int(0, r)); }
            else { for (int c = 0; c < L.W; c++) starts.Add(new Vector2Int(c, L.H - 1)); for (int r = 0; r < L.H - 1; r++) starts.Add(new Vector2Int(0, r)); }
            foreach (var s in starts)
            {
                var seg = new List<Vector2Int>();
                int? baseLv = null;
                for (var p = s; L.InGrid(p.x, p.y); p += d)
                {
                    bool see = See(p.x, p.y), ramp = Ramp(p.x, p.y), water = L.At(p.x, p.y) == '.';
                    bool step = see && !ramp && !water && baseLv.HasValue && Lv(p.x, p.y) != baseLv.Value;
                    if (see && !step)
                    {
                        seg.Add(p);
                        if (!ramp && !water && !baseLv.HasValue) baseLv = Lv(p.x, p.y);
                        continue;
                    }
                    Close(seg);
                    baseLv = null;
                    if (step) { seg.Add(p); baseLv = Lv(p.x, p.y); }
                }
                Close(seg);
            }
        }

        LastSightLines = lines.OrderByDescending(x => x.m).Select(x => $"{x.m:0} m ({x.a.x},{x.a.y})-({x.b.x},{x.b.y}) {x.zones}").ToList();
        if (lines.Count == 0) { yield return $"OK  sight lines: none of {SightCount:0} m or more"; yield break; }
        var top = lines.OrderByDescending(x => x.m).First();
        string list = string.Join(", ", LastSightLines.Take(3));
        yield return (top.m >= SightWarn ? "WARN" : "OK  ") + $" sight lines: {lines.Count} of {SightCount:0} m or more, the longest {list}" +
                     (top.m >= SightWarn ? $" (cut lines of {SightWarn:0} m+ with a building, a jog or a covered bridge)" : "");
    }

    public static List<string> LastSightLines = new List<string>();
}
