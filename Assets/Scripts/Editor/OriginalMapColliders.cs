// Collision for the Original map's scenery (BeachIsland/Environment), so what you see is what blocks:
// - trees: a chain of capsules up the trunk (the bark), no more whole-tree mesh colliders (you could stand on
//   the canopies and hop over everything);
// - props without collision that look solid (loungers, umbrellas, surfboards, sandcastle, torches, stumps...):
//   boxes where the mesh reaches the ground, seen from above (you walk under an umbrella, not through its pole);
// - flat things (towels, sticks, bottles, shells...) and soft plants keep no collision.
// Idempotent: removes what it added before. Called by TestMapSetup.SetupAll and IslandBuilder.Build.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OriginalMapColliders
{
    const float MinTop = 0.5f;        // solid things at least this tall: the NavMesh (climb 0.45) and the player (step 0.3) agree
    const float FlatBelow = 0.4f;     // lower than this: no collision
    const float ReachesGround = 1.1f; // a column is solid when the mesh comes this close to the ground
    const string Added = "Colision";  // children holding added colliders

    static readonly HashSet<string> NoCollision = new HashSet<string> { "Grave_Gate" };   // the way into the graveyard

    // foot of every stairs (centre of the low edge, on the ground), the way up and the width: doors at the
    // bottom of stairs are lined up with them
    public static readonly List<(Transform stairs, Vector3 foot, Vector3 up, float width)> Feet = new List<(Transform, Vector3, Vector3, float)>();

    public static string Apply(Transform environment)
    {
        int trees = 0, props = 0, ramps = 0;
        Feet.Clear();
        foreach (Transform group in environment)
            foreach (Transform item in group)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
                string asset = src != null ? src.name : item.name;
                if (group.name == "Tree" && asset != "Tree_Stump")
                {
                    Clear(item);
                    Trunk(item);
                    trees++;
                }
                else if (group.name == "Prop" || asset == "Tree_Stump" || asset == "Rock_Small" || asset == "Log_Mossy")
                {
                    if (NoCollision.Contains(asset)) continue;
                    // tall things with an exact mesh (tent, tiki bar, totem...) keep it; low ones (benches, crates,
                    // barrels, small rocks) become boxes at least MinTop tall: with the mesh the NavMesh climbed onto
                    // them (0.45) where the player can't step (0.3) and walking paths got stuck
                    bool hadMesh = item.GetComponentsInChildren<MeshCollider>().Length > 0 && item.Find(Added) == null;
                    if (hadMesh && Height(item) >= 1f) continue;
                    Clear(item);
                    if (Footprint(item)) props++;
                }
                else if (group.name == "Stairs")
                {
                    var old = item.Find(Added);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    if (Ramp(item)) ramps++;
                }
            }
        Physics.SyncTransforms();
        return $"[Colliders] {trees} árboles con tronco, {props} props con huella, {ramps} escaleras con rampa";
    }

    static float Height(Transform item)
    {
        var rs = item.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0f;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }

    // Stone stairs: steps taller than the player's step (0.3) stop the CharacterController, so an invisible
    // ramp lies on the step edges (the mesh stays under it). Run direction = the local axis the height grows along.
    static bool Ramp(Transform stairs)
    {
        var mc = stairs.GetComponentInChildren<MeshCollider>();
        if (mc == null) return false;
        Physics.SyncTransforms();
        var mf = mc.GetComponent<MeshFilter>();
        var lb = mf.sharedMesh.bounds;   // local to the mesh
        var t = mc.transform;
        float Top(Vector3 local)
        {
            var w = t.TransformPoint(local);
            return mc.Raycast(new Ray(new Vector3(w.x, mc.bounds.max.y + 1f, w.z), Vector3.down), out var h, mc.bounds.size.y + 2f) ? h.point.y : float.NaN;
        }
        // profile along local x and local z through the middle
        const int N = 24;
        float[] Profile(int axis)
        {
            var p = new float[N];
            for (int i = 0; i < N; i++)
            {
                var l = lb.center;
                float f = (i + 0.5f) / N;
                if (axis == 0) l.x = Mathf.Lerp(lb.min.x, lb.max.x, f); else l.z = Mathf.Lerp(lb.min.z, lb.max.z, f);
                p[i] = Top(l);
            }
            return p;
        }
        var px = Profile(0); var pz = Profile(2);
        float Rise(float[] p) { var v = p.Where(h => !float.IsNaN(h)).ToArray(); return v.Length < 2 ? 0f : Mathf.Abs(v.Last() - v.First()); }
        int axis = Rise(px) >= Rise(pz) ? 0 : 2;
        var prof = axis == 0 ? px : pz;
        if (prof.Any(float.IsNaN) || Rise(prof) < 0.5f) return false;
        bool upPositive = prof[N - 1] > prof[0];
        // world positions of the samples along the run
        Vector3 dirLocal = axis == 0 ? Vector3.right : Vector3.forward;
        Vector3 runW = t.TransformDirection(dirLocal) * (upPositive ? 1f : -1f);
        runW.y = 0f;
        float lenW = (t.TransformVector(dirLocal * (axis == 0 ? lb.size.x : lb.size.z))).magnitude;
        float widthW = (t.TransformVector(axis == 0 ? Vector3.forward * lb.size.z : Vector3.right * lb.size.x)).magnitude;
        runW.Normalize();
        Vector3 lowEnd = t.TransformPoint(lb.center) - runW * (lenW / 2f);   // centre of the low edge, at any height
        Vector3 side = Vector3.Cross(Vector3.up, runW);
        // walkable top along the run, the steps AND the ground around them (the slope of the hill pokes over the
        // upper steps), from 1.5 m before the stairs to 1.5 m past the top; s = distance from the low edge
        int mask = ~((1 << TestMapSetup.WallLayer) | (1 << TestMapSetup.DoorLayer) | (1 << TestMapSetup.PlayerLayer) | (1 << TestMapSetup.EnemyLayer) | (1 << 2) | (1 << 4));
        float Surface(float sAlong)
        {
            float best = float.NegativeInfinity;
            foreach (float o in new[] { -widthW / 3f, 0f, widthW / 3f })
            {
                var p = lowEnd + runW * sAlong + side * o;
                foreach (var hit in Physics.RaycastAll(new Vector3(p.x, mc.bounds.max.y + 3f, p.z), Vector3.down, mc.bounds.size.y + 8f, mask, QueryTriggerInteraction.Ignore))
                    if (hit.collider == mc || hit.collider.name.StartsWith("Terrain")) best = Mathf.Max(best, hit.point.y);   // not the rocks beside it
            }
            return best;
        }
        var s = new List<float>(); var h = new List<float>();
        for (float x = -1.5f; x <= lenW + 1.5f; x += 0.1f) { s.Add(x); h.Add(Surface(x)); }
        float topY = float.NegativeInfinity;
        for (int i = 0; i < s.Count; i++) if (s[i] >= lenW - 0.5f && s[i] <= lenW + 1f) topY = Mathf.Max(topY, h[i]);
        int iTop = Enumerable.Range(0, s.Count).First(i => s[i] >= 0f && h[i] >= topY - 0.05f);
        float sTop = s[iTop];
        // over every step edge (the ground before the stairs may stay above it: the ramp just dives under it there;
        // stretching the ramp out over open ground left a lip along its sides)
        float slope = float.PositiveInfinity;
        for (int i = 0; i < iTop; i++) if (s[i] >= 0f && h[i] < topY) slope = Mathf.Min(slope, (topY - h[i]) / (sTop - s[i]));
        if (float.IsInfinity(slope) || slope <= 0.05f) return false;
        slope = Mathf.Min(slope, Mathf.Tan(45f * Mathf.Deg2Rad));   // lower slope = the line is higher everywhere: still over every edge
        float groundY = Surface(-1f);
        float s0 = sTop - (topY - groundY) / slope;   // where the ramp meets the ground (may start before the stairs)
        Feet.Add((stairs, new Vector3(lowEnd.x, groundY, lowEnd.z), runW, widthW));
        Vector3 a = new Vector3(lowEnd.x, groundY, lowEnd.z) + runW * s0;
        Vector3 b = new Vector3(lowEnd.x, topY, lowEnd.z) + runW * sTop;
        var holder = Holder(stairs);
        var go = new GameObject("Rampa").transform;
        go.SetParent(holder, false);
        go.gameObject.layer = stairs.gameObject.layer;
        Vector3 along = b - a;
        const float thick = 0.3f;
        go.rotation = Quaternion.LookRotation(along.normalized, Vector3.Cross(along, Vector3.Cross(Vector3.up, along)).normalized);
        go.position = (a + b) / 2f - go.up * (thick / 2f - 0.02f);
        go.localScale = new Vector3(1f / stairs.lossyScale.x, 1f / stairs.lossyScale.y, 1f / stairs.lossyScale.z);
        var box = go.gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(widthW - 0.1f, thick, along.magnitude);
        return true;
    }

    static void Clear(Transform item)
    {
        foreach (var c in item.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var old = item.Find(Added);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    static Transform Holder(Transform item)
    {
        var h = new GameObject(Added).transform;
        h.SetParent(item, false);
        h.gameObject.layer = item.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(h.gameObject, GameObjectUtility.GetStaticEditorFlags(item.gameObject));
        return h;
    }

    // Capsules through the centre of the bark at 0.2 / 1.2 / 2.2 / 3.0 m (palms lean, jungle trees curve).
    static void Trunk(Transform tree)
    {
        var pts = new List<Vector3>();
        foreach (var mf in tree.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var verts = mesh.vertices;
            var mr = mf.GetComponent<MeshRenderer>();
            int sub = -1;
            if (mr != null)
                for (int i = 0; i < mr.sharedMaterials.Length && i < mesh.subMeshCount; i++)
                    if (mr.sharedMaterials[i] != null && mr.sharedMaterials[i].name.Contains("Bark")) sub = i;
            IEnumerable<int> idx = sub >= 0 ? mesh.GetTriangles(sub).Distinct() : Enumerable.Range(0, verts.Length);
            foreach (int i in idx) pts.Add(mf.transform.TransformPoint(verts[i]));
        }
        if (pts.Count == 0) return;
        float minY = pts.Min(v => v.y);
        var low = pts.Where(v => v.y <= minY + 0.45f).ToList();
        Vector3 lc = low.Aggregate(Vector3.zero, (a, b) => a + b) / low.Count;
        // the base: points near the lowest ring's centre (roots and branches don't drag it)
        float radius = Mathf.Clamp(low.Average(v => new Vector2(v.x - lc.x, v.z - lc.z).magnitude) * 0.85f, 0.18f, 0.6f);
        var chain = new List<Vector3> { new Vector3(lc.x, minY, lc.z) };
        foreach (float h in new[] { 1.2f, 2.2f, 3.0f })
        {
            var prev = chain[chain.Count - 1];
            var slice = pts.Where(v => Mathf.Abs(v.y - (minY + h)) <= 0.25f && new Vector2(v.x - prev.x, v.z - prev.z).magnitude <= radius + 0.8f).ToList();
            if (slice.Count == 0) break;
            chain.Add(slice.Aggregate(Vector3.zero, (a, b) => a + b) / slice.Count);
        }
        if (chain.Count == 1) chain.Add(chain[0] + Vector3.up * 3f);
        var holder = Holder(tree);
        for (int i = 1; i < chain.Count; i++)
        {
            Vector3 axis = chain[i] - chain[i - 1];
            var t = new GameObject("Tronco").transform;
            t.SetParent(holder, false);
            t.gameObject.layer = tree.gameObject.layer;
            t.SetPositionAndRotation(chain[i - 1], Quaternion.FromToRotation(Vector3.up, axis));
            t.localScale = new Vector3(1f / tree.lossyScale.x, 1f / tree.lossyScale.y, 1f / tree.lossyScale.z);
            var cap = t.gameObject.AddComponent<CapsuleCollider>();
            cap.radius = radius * (i == 1 ? 1f : 0.85f);
            cap.height = axis.magnitude + cap.radius * 2f;
            cap.center = new Vector3(0f, axis.magnitude / 2f, 0f);
        }
    }

    // Columns seen from above: solid (from the ground to the top of the mesh) where the mesh comes close to
    // the ground; merged along x into boxes.
    static bool Footprint(Transform item)
    {
        var mfs = item.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null).ToArray();
        if (mfs.Length == 0) return false;
        // the ground under it (models are often half buried): "at least MinTop tall" is measured from there
        Physics.SyncTransforms();
        var rb = mfs[0].GetComponent<Renderer>().bounds;
        foreach (var mf in mfs) rb.Encapsulate(mf.GetComponent<Renderer>().bounds);
        float ground = rb.min.y, terrain = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(new Vector3(rb.center.x, rb.max.y + 2f, rb.center.z), Vector3.down, rb.size.y + 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(item) || hit.collider.gameObject.layer == TestMapSetup.WallLayer || hit.point.y > rb.center.y) continue;
            ground = Mathf.Max(ground, hit.point.y);
            if (hit.collider.name.StartsWith("Terrain")) terrain = Mathf.Max(terrain, hit.point.y);
        }
        // tiny things (lanterns, candles, torches, a ball) and small things standing on a bench / table are
        // decoration: you brush through them (the NavMesh can't see them either)
        if (Mathf.Max(rb.size.x, rb.size.z) < 0.45f) return false;
        if (Mathf.Max(rb.size.x, rb.size.z) < 0.7f && terrain > float.NegativeInfinity && ground - terrain > 0.25f) return false;
        var temp = new List<MeshCollider>();
        foreach (var mf in mfs) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
        Physics.SyncTransforms();
        var b = temp[0].bounds;
        foreach (var mc in temp) b.Encapsulate(mc.bounds);
        bool made = false;
        float baseY = Mathf.Max(b.min.y, ground);
        if (b.max.y - baseY >= FlatBelow && Mathf.Max(b.size.x, b.size.z) >= 0.1f && Mathf.Max(b.size.x, b.size.z) < 0.7f)
        {
            // small things (lanterns, torches, a ball): one box, wide enough for the NavMesh to see it too
            var go = new GameObject("Huella").transform;
            go.SetParent(Holder(item), false);
            go.gameObject.layer = item.gameObject.layer;
            float h = Mathf.Max(b.max.y, baseY + MinTop);
            go.SetPositionAndRotation(new Vector3(b.center.x, (b.min.y - 0.3f + h) / 2f, b.center.z), Quaternion.identity);
            go.localScale = new Vector3(1f / item.lossyScale.x, 1f / item.lossyScale.y, 1f / item.lossyScale.z);
            var box = go.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(b.size.x, 0.3f), h - b.min.y + 0.3f, Mathf.Max(b.size.z, 0.3f));
            made = true;
        }
        else if (b.max.y - baseY >= FlatBelow && Mathf.Max(b.size.x, b.size.z) >= 0.1f)
        {
            float step = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) / 3f, 0.08f, 0.35f);
            int nx = Mathf.Max(1, Mathf.CeilToInt(b.size.x / step)), nz = Mathf.Max(1, Mathf.CeilToInt(b.size.z / step));
            // lowest / highest vertex per column (thin poles the rays could miss)
            var vlo = new float[nx, nz];
            var vhi = new float[nx, nz];
            for (int i = 0; i < nx; i++) for (int k = 0; k < nz; k++) { vlo[i, k] = float.PositiveInfinity; vhi[i, k] = float.NegativeInfinity; }
            foreach (var mf in mfs)
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    int i = Mathf.Clamp((int)((w.x - b.min.x) / b.size.x * nx), 0, nx - 1), k = Mathf.Clamp((int)((w.z - b.min.z) / b.size.z * nz), 0, nz - 1);
                    vlo[i, k] = Mathf.Min(vlo[i, k], w.y);
                    vhi[i, k] = Mathf.Max(vhi[i, k], w.y);
                }
            var top = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    float x = b.min.x + (i + 0.5f) * b.size.x / nx, z = b.min.z + (k + 0.5f) * b.size.z / nz;
                    float hi = vhi[i, k], lo = vlo[i, k];
                    foreach (var mc in temp)
                    {
                        // from above: the top and every surface under it (an umbrella canopy is open below)
                        var o = new Vector3(x, b.max.y + 1f, z);
                        for (int n = 0; n < 6 && mc.Raycast(new Ray(o, Vector3.down), out var h1, o.y - b.min.y + 1f); n++)
                        {
                            hi = Mathf.Max(hi, h1.point.y);
                            lo = Mathf.Min(lo, h1.point.y);
                            o = h1.point + Vector3.down * 0.01f;
                        }
                        if (mc.Raycast(new Ray(new Vector3(x, b.min.y - 1f, z), Vector3.up), out var h2, b.size.y + 2f)) lo = Mathf.Min(lo, h2.point.y);
                    }
                    top[i, k] = hi > float.NegativeInfinity && lo - baseY <= ReachesGround ? Mathf.Max(hi, baseY + MinTop) : float.NegativeInfinity;
                }
            var holder = Holder(item);
            float cw = b.size.x / nx, cd = b.size.z / nz;
            // merged into rectangles (along x, then down z while the whole run is solid and about as tall), at
            // least 0.3 m across: thin slivers slipped between the NavMesh voxels (old men walked where the player
            // could not)
            var used = new bool[nx, nz];
            bool Solid(int x, int z, float h) => !used[x, z] && !float.IsNegativeInfinity(top[x, z]) && Mathf.Abs(top[x, z] - h) < 0.4f;
            for (int k = 0; k < nz; k++)
                for (int i = 0; i < nx; i++)
                {
                    if (used[i, k] || float.IsNegativeInfinity(top[i, k])) continue;
                    float h = top[i, k];
                    int j = i;
                    while (j + 1 < nx && Solid(j + 1, k, h)) j++;
                    int m = k;
                    while (m + 1 < nz && Enumerable.Range(i, j - i + 1).All(x => Solid(x, m + 1, h))) m++;
                    for (int x = i; x <= j; x++) for (int z = k; z <= m; z++) { used[x, z] = true; h = Mathf.Max(h, top[x, z]); }
                    var go = new GameObject("Huella").transform;
                    go.SetParent(holder, false);
                    go.gameObject.layer = item.gameObject.layer;
                    go.SetPositionAndRotation(new Vector3(b.min.x + (i + j + 1) * cw / 2f, (b.min.y - 0.3f + h) / 2f, b.min.z + (k + m + 1) * cd / 2f), Quaternion.identity);
                    go.localScale = new Vector3(1f / item.lossyScale.x, 1f / item.lossyScale.y, 1f / item.lossyScale.z);
                    var box = go.gameObject.AddComponent<BoxCollider>();
                    box.size = new Vector3(Mathf.Max((j - i + 1) * cw, 0.3f), h - b.min.y + 0.3f, Mathf.Max((m - k + 1) * cd, 0.3f));
                    made = true;
                }
        }
        foreach (var mc in temp) Object.DestroyImmediate(mc);
        return made;
    }
}
