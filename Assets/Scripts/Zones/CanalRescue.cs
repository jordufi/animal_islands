using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Maps with open water and no invisible walls (La Serenisima): a player who falls into a canal, or who lands in a
// barrio that is still closed (a long jump over a narrow canal), is put back on the last cell of an OPEN zone he
// stood on, with a little damage. So nobody skips a paid bridge and nobody is lost in the water.
// The setup bakes the zone of every cell here (rows of zone letters; '.' = water, '+' = door / neutral cell).
// Only the local players are moved: online every player moves their own body, so nothing is sent over the network.
public class CanalRescue : MonoBehaviour
{
    [SerializeField] string[] rows;            // row 0 = north; one char per cell
    [SerializeField] string letters = "";      // zone letter of zones[i]
    [SerializeField] Zone[] zones;
    [SerializeField] int originCol, originRow;
    [SerializeField] float cell = 2f;
    [SerializeField] float waterY = -0.8f;
    [SerializeField] int damage = 15;
    [SerializeField] float carryTime = 0.55f;

    public static CanalRescue Instance { get; private set; }
    public static event System.Action<PlayerHealth, bool> Rescued;   // (player, fellInWater) - tests
    public float WaterY { get => waterY; set => waterY = value; }    // the acqua alta raises it

    readonly Dictionary<PlayerHealth, Vector3> lastSafe = new Dictionary<PlayerHealth, Vector3>();
    readonly HashSet<PlayerHealth> carrying = new HashSet<PlayerHealth>();

    public void Configure(string[] zoneRows, string zoneLetters, Zone[] zoneRefs, int oc, int or, float cellSize)
    {
        rows = zoneRows; letters = zoneLetters; zones = zoneRefs; originCol = oc; originRow = or; cell = cellSize;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public char LetterAt(Vector3 p)
    {
        int c = Mathf.RoundToInt(p.x / cell) + originCol, r = originRow - Mathf.RoundToInt(p.z / cell);
        if (rows == null || r < 0 || r >= rows.Length || c < 0 || c >= rows[r].Length) return '.';
        return rows[r][c];
    }

    public Zone ZoneAt(Vector3 p)
    {
        int i = letters.IndexOf(LetterAt(p));
        return i >= 0 && i < zones.Length ? zones[i] : null;
    }

    Vector3 CellCentre(Vector3 p) => new Vector3(Mathf.Round(p.x / cell) * cell, p.y, Mathf.Round(p.z / cell) * cell);

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var p in gm.Players)
        {
            if (p == null || carrying.Contains(p) || p.IsDead || PlayerSlot.IsProxy(p)) continue;
            Vector3 pos = p.transform.position;
            var tide = AcquaAlta.Instance;
            bool swimming = tide != null && tide.SwimActive;   // acqua alta: the canals have a floor, you swim them
            if (pos.y < (swimming ? tide.SwimY - 0.6f : waterY + 0.15f)) { Rescue(p, true); continue; }
            var cc = p.GetComponent<CharacterController>();
            if (cc == null || !cc.enabled || !cc.isGrounded) continue;
            char ch = LetterAt(pos);
            if (ch == '+') continue;                       // door cells: neither safe nor forbidden
            if (swimming && pos.y < -0.1f) continue;       // swimming under an arched bridge: still the canal
            var z = ZoneAt(pos);
            if (z == null) continue;
            if (z.IsOpen) { if (pos.y < 7f) lastSafe[p] = CellCentre(pos); }   // never the top of the campanile
            else Rescue(p, false);
        }
    }

    // The tide goes down while someone is still in a canal: back to the last open cell, no damage, no toast.
    public void ToShore(PlayerHealth p)
    {
        if (p == null || carrying.Contains(p)) return;
        Rescue(p, true, false);
    }

    public bool InWater(Vector3 p) => LetterAt(p) == '.';

    void Rescue(PlayerHealth p, bool water, bool hurt = true)
    {
        if (!lastSafe.TryGetValue(p, out var target))
        {
            var spawn = GameObject.Find("PlayerSpawn");
            target = spawn != null ? spawn.transform.position : Vector3.zero;
        }
        StartCoroutine(Carry(p, target + Vector3.up * 0.6f, water, hurt));
    }

    IEnumerator Carry(PlayerHealth p, Vector3 target, bool water, bool hurt)
    {
        carrying.Add(p);
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        Vector3 a = p.transform.position;
        for (float t = 0f; t < carryTime; t += Time.deltaTime)
        {
            float k = t / carryTime;
            p.transform.position = Vector3.Lerp(a, target, k) + Vector3.up * (1.5f * 4f * k * (1f - k));
            yield return null;
        }
        p.transform.position = target;
        if (cc != null) cc.enabled = true;
        if (pc != null) pc.enabled = true;
        carrying.Remove(p);
        if (!hurt) yield break;
        if (water && damage > 0 && !p.IsDowned && p.CurrentHealth > damage + 1) p.TakeDamage(damage);
        var gm = GameManager.Instance;
        if (gm != null) gm.ShowMessageTo(p, water ? "¡Al agua! Sales por donde caíste." : "Ese barrio sigue cerrado: paga el puente.");
        Rescued?.Invoke(p, water);
    }

    void OnDisable()
    {
        foreach (var p in carrying)
        {
            if (p == null) continue;
            var cc = p.GetComponent<CharacterController>();
            var pc = p.GetComponent<PlayerController>();
            if (cc != null) cc.enabled = true;
            if (pc != null) pc.enabled = true;
        }
        carrying.Clear();
    }
}
