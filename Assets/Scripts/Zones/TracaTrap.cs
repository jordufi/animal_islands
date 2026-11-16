using System.Collections;
using UnityEngine;

// Villajubilada: the traca, the first buyable trap. A string of firecrackers hangs along a straight stretch of
// street; pay at its post (EconomyData.tracaCost) and it burns from one end to the other in a few seconds. Old men in
// the lane when the flame goes by die and PAY like a normal kill (to the team's pot); the boss is not hurt. It also
// burns the players in the lane a little. Then it has to be hung again (cooldown).
// Online: the host charges, kills and tells everyone (NetGame.TrapFired); every machine burns its own players.
public class TracaTrap : Interactable
{
    [SerializeField] Vector3 from, to;          // the two ends, on the ground
    [SerializeField] float halfWidth = 1.1f;
    [SerializeField] Transform strand;          // the hanging firecrackers (hidden while it is spent)
    [SerializeField] TextMesh worldLabel;

    float readyAt;
    bool burning;
    static readonly Collider[] hits = new Collider[32];

    public bool Burning => burning;
    public bool Ready => !burning && Time.time >= readyAt;
    public int Kills { get; private set; }
    public Vector3 From => from;
    public Vector3 To => to;

    public void Configure(Vector3 a, Vector3 b, Transform visual, TextMesh label) { from = a; to = b; strand = visual; worldLabel = label; }

    static EconomyData Eco => Game != null ? Game.Economy : null;
    int Cost => Eco != null ? Eco.tracaCost : 750;

    void Update()
    {
        if (strand != null && strand.gameObject.activeSelf != Ready) strand.gameObject.SetActive(Ready);
        if (worldLabel != null) worldLabel.text = burning ? "" : Ready ? "TRACA\n" + Cost + " conchas" : "TRACA\n(colgándola: " + Mathf.CeilToInt(readyAt - Time.time) + " s)";
    }

    public override string Prompt(PlayerInteractor player) => burning ? "" : Ready ? "□  Encender la traca (" + CostText(Cost) + ")" : "Están colgando otra traca (" + Mathf.CeilToInt(readyAt - Time.time) + " s)";
    public override bool CanInteract(PlayerInteractor player) => Ready;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || !Ready || !Pay(Cost, player)) return;   // online client: PlayerInteractor asks the host
        if (NetGame.H != null) NetGame.H.TrapFired(this);
        StartCoroutine(Burn(player));
    }

    // Online client: the host lit it.
    public void NetFired() { if (!burning) StartCoroutine(Burn(null)); }

    // Tests: light it without paying.
    public void DebugFire(Component by) { if (Ready) StartCoroutine(Burn(by)); }

    IEnumerator Burn(Component by)
    {
        burning = true;
        float seconds = Eco != null ? Eco.tracaBurnSeconds : 3f;
        var done = new System.Collections.Generic.HashSet<object>();
        Vector3 dir = (to - from).normalized, side = new Vector3(dir.z, 0f, -dir.x);
        float length = Vector3.Distance(from, to);
        var flame = MakeFlame();
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float d = length * t / seconds;
            Vector3 at = from + dir * d;
            if (flame != null) { flame.position = at + Vector3.up * 1.6f; flame.localScale = Vector3.one * (0.9f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 30f))); }
            // players of this machine in the lane, within 1.5 m of the flame
            var gm = Game;
            if (gm != null)
                foreach (var p in gm.Players)
                {
                    if (p == null || p.IsDead || p.IsDowned || PlayerSlot.IsProxy(p) || done.Contains(p)) continue;
                    Vector3 o = p.transform.position - at;
                    if (Mathf.Abs(o.y) > 2.5f || Mathf.Abs(Vector3.Dot(o, dir)) > 1.5f || Mathf.Abs(Vector3.Dot(o, side)) > halfWidth) continue;
                    done.Add(p);
                    p.TakeDamage(Eco != null ? Eco.tracaPlayerDamage : 25);
                }
            if (Net.Authority)
            {
                int n = Physics.OverlapBoxNonAlloc(at + Vector3.up, new Vector3(halfWidth, 1.5f, 1.5f), hits, Quaternion.LookRotation(dir), 1 << 9, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var e = hits[i].GetComponentInParent<EnemyHealth>();
                    if (e == null || e.IsDead || done.Contains(e) || e.GetComponent<BossAI>() != null) continue;
                    done.Add(e);
                    // a normal kill for the pot (the buyer's stats); the trap ignores masks
                    EnemyHits.ApplyLocal(e, e.Current + 1f, false, HitKind.Ability, by, false, 0f, 0f, at);
                    if (!e.IsDead) e.TakeDamage(e.Current + 1f, false, true);
                    Kills++;
                }
            }
            yield return null;
        }
        if (flame != null) Destroy(flame.gameObject);
        burning = false;
        readyAt = Time.time + (Eco != null ? Eco.tracaCooldown : 90f);
    }

    static Material fire;

    static Transform MakeFlame()
    {
        if (fire == null) fire = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.75f, 0.2f, 0.85f) };
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Traca_Llama";
        Destroy(g.GetComponent<Collider>());
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = fire;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.3f); l.range = 9f; l.intensity = 6f;
        return g.transform;
    }
}
