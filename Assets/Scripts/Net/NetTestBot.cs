using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

// Driver for the automated online play test (Tools > Island > Play Test Online). Exists ONLY when the game
// is started with -netbot: it keeps joining the address given with -netjoin, runs the commands the host's
// test sends (NetMsg.TestCmd: teleport, shoot, use, pick a card, revive, screenshot...) and answers with
// reports of what this machine sees (NetMsg.TestReport). Normal players never have it.
public class NetTestBot : MonoBehaviour
{
    readonly List<string> toasts = new List<string>();
    float nextJoin;
    bool quitting;
    GameManager hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!NetSession.HasArg("-netbot")) return;
        var go = new GameObject("NetTestBot");
        DontDestroyOnLoad(go);
        go.AddComponent<NetTestBot>();
    }

    void Awake()
    {
        Application.runInBackground = true;
        nextJoin = Time.unscaledTime + 3f;
    }

    void Update()
    {
        NetSession.On(NetMsg.TestCmd, OnCmd);   // (re)registered: handlers are static and survive scene loads
        var gm = GameManager.Instance;
        if (gm != null && gm != hooked) { hooked = gm; gm.Message += t => { toasts.Add(t); if (toasts.Count > 8) toasts.RemoveAt(0); }; }

        // keep trying to join until the host is up
        string join = NetSession.Arg("-netjoin");
        var ns = NetSession.Instance;
        if (!quitting && join != null && Time.unscaledTime >= nextJoin && (ns == null || (!ns.Active && !ns.Busy)))
        {
            nextJoin = Time.unscaledTime + 4f;
            NetSession.Ensure().Join(join);
        }
    }

    static PlayerHealth Me => NetGame.Instance != null ? NetGame.Instance.LocalPlayer : GameManager.Instance != null ? GameManager.Instance.Player : null;

    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    void OnCmd(int sender, NetRead r)
    {
        string line = r.Str();
        var a = line.Split(' ');
        var me = Me;
        try
        {
            switch (a[0])
            {
                case "tp":
                    if (me == null) break;
                    var cc = me.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    me.transform.SetPositionAndRotation(new Vector3(F(a[1]), F(a[2]), F(a[3])), Quaternion.Euler(0f, a.Length > 4 ? F(a[4]) : 0f, 0f));
                    if (cc != null) cc.enabled = true;
                    break;
                case "look":
                    var rig = PlayerSlot.RigOf(me);
                    if (rig != null) rig.LookAtPoint(new Vector3(F(a[1]), F(a[2]), F(a[3])));
                    break;
                case "shoot": StartCoroutine(Shoot(int.Parse(a[1]), a.Length > 2 ? int.Parse(a[2]) : 0)); break;
                case "hit":
                    var e = NearestEnemy();
                    if (e != null && me != null) EnemyHits.Apply(e, F(a[1]), a.Length > 2 && a[2] == "head", HitKind.Gun, me, true);
                    break;
                case "use": if (me != null) me.GetComponent<PlayerInteractor>().Press(); break;
                case "card":
                    var cm = CardManager.Instance;
                    if (cm != null && cm.CurrentOffer != null)
                    {
                        int i = Mathf.Clamp(int.Parse(a[1]), 0, cm.CurrentOffer.Length - 1);
                        cm.Select(i);
                        cm.Choose(cm.CurrentOffer[i]);
                    }
                    break;
                case "revive":
                    if (me == null) break;
                    var it = me.GetComponent<PlayerInteractor>();
                    it.HoldOverride = a[1] == "on";
                    if (it.HoldOverride) it.Press();
                    break;
                case "animal": if (NetSession.Instance != null) NetSession.Instance.SetMyAnimal(a[1]); break;
                case "god": if (me != null) me.GodMode = a[1] == "on"; break;
                case "empty":   // empties the reserve of the weapon in hand (to see a Max Ammo refill it)
                    if (me != null) { var s = me.GetComponent<PlayerShooter>().Current; if (s.data != null) s.reserve = 0; }
                    break;
                case "report": Report(a.Length > 1 ? a[1] : "r"); break;
                case "shot": ScreenCapture.CaptureScreenshot(line.Substring(5)); break;
                case "leave": quitting = true; if (NetSession.Instance != null) NetSession.Instance.Leave(); break;
                case "quit": quitting = true; Application.Quit(); break;
            }
        }
        catch (System.Exception ex) { Debug.LogException(ex); Send("error", line + " -> " + ex.Message); }
    }

    static EnemyHealth NearestEnemy()
    {
        var me = Me;
        if (me == null) return null;
        return Object.FindObjectsByType<EnemyHealth>().Where(e => !e.IsDead && e.NetId > 0)
            .OrderBy(e => (e.transform.position - me.transform.position).sqrMagnitude).FirstOrDefault();
    }

    int shots, shotHits, shotFails;

    // shoots the old man with that id (0 = the nearest), aiming the camera at his chest
    IEnumerator Shoot(int n, int id)
    {
        var me0 = Me;
        if (me0 == null) yield break;
        var shooter = me0.GetComponent<PlayerShooter>();
        System.Action<bool, bool> onFired = (hit, kill) => { shots++; if (hit) shotHits++; };
        shooter.Fired += onFired;
        for (int i = 0; i < n; i++)
        {
            var me = Me;
            var e = id > 0 ? Object.FindObjectsByType<EnemyHealth>().FirstOrDefault(x => x.NetId == id && !x.IsDead) : NearestEnemy();
            if (me == null || e == null) break;
            var rig = PlayerSlot.RigOf(me);
            Vector3 aim = e.transform.position + Vector3.up * 1.0f * e.transform.localScale.y;
            for (int f = 0; f < 4; f++) { if (rig != null) rig.LookAtPoint(aim); yield return null; }
            if (!shooter.TestFire()) shotFails++;
            yield return new WaitForSeconds(0.4f);
        }
        shooter.Fired -= onFired;
    }

    static void Send(string tag, string text)
    {
        var ns = NetSession.Instance;
        if (ns != null) ns.ToHost(NetBuf.Make(NetMsg.TestReport).Str(tag + "|" + text));
    }

    void Report(string tag)
    {
        var sb = new StringBuilder();
        void Kv(string k, object v) => sb.Append(k).Append('=').Append(System.Convert.ToString(v, CultureInfo.InvariantCulture)).Append(';');
        var ns = NetSession.Instance;
        var gm = GameManager.Instance;
        var me = Me;
        Kv("scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        Kv("active", ns != null && ns.Active);
        Kv("index", Net.LocalIndex);
        Kv("ready", NetGame.Instance != null && NetGame.Instance.Ready);
        if (gm != null)
        {
            Kv("players", gm.PlayerCount);
            Kv("points", gm.Points);
            Kv("state", gm.State);
            Kv("doors", gm.DoorsOpened);
            Kv("kills", gm.Kills);
            Kv("bossDefeated", gm.BossDefeated);
        }
        if (me != null)
        {
            var p = me.transform.position;
            Kv("pos", p.x.ToString("0.00", CultureInfo.InvariantCulture) + "," + p.y.ToString("0.00", CultureInfo.InvariantCulture) + "," + p.z.ToString("0.00", CultureInfo.InvariantCulture));
            Kv("hp", me.CurrentHealth);
            Kv("maxhp", me.MaxHealth);
            Kv("downed", me.IsDowned);
            Kv("dead", me.IsDead);
            var sh = me.GetComponent<PlayerShooter>();
            Kv("weapons", string.Join(",", sh.Slots.Select(s => s.data == null ? "-" : s.data.name + (s.upgraded ? "+" : ""))));
            Kv("drinks", me.GetComponent<PlayerDrinks>().Owned.Count);
            var carry = me.GetComponent<PlayerCarry>();
            Kv("carry", carry != null && carry.Carried != null);
            Kv("ignored", me.IgnoredByEnemies);
        }
        var rm = RoundManager.Instance;
        if (rm != null) { Kv("round", rm.Round); Kv("remaining", rm.RemainingInRound); Kv("night", rm.IsNight); Kv("intermission", rm.InIntermission); }
        Kv("enemies", Object.FindObjectsByType<EnemyHealth>().Count(e => !e.IsDead));
        foreach (var e in Object.FindObjectsByType<EnemyHealth>().Where(x => !x.IsDead && x.NetId > 0).OrderBy(x => x.NetId).Take(6))
            Kv("e" + e.NetId, e.transform.position.x.ToString("0.00", CultureInfo.InvariantCulture) + "," + e.transform.position.z.ToString("0.00", CultureInfo.InvariantCulture));
        Kv("chanclas", ChanclaProjectile.Created);
        Kv("shots", shots + "/" + shotHits + "/" + shotFails);
        Kv("coolers", Object.FindObjectsByType<AmmoPickup>().Length);
        if (me != null) Kv("reserve", me.GetComponent<PlayerShooter>().Current.reserve);
        var egg = EasterEgg.Instance;
        if (egg != null) { Kv("discs", egg.DiscsFound); Kv("eggDone", egg.Completed); }
        var karaoke = Object.FindAnyObjectByType<Karaoke>();
        if (karaoke != null) Kv("singing", karaoke.IsSinging);
        var gold = Object.FindObjectsByType<Carryable>().FirstOrDefault();
        Kv("gold", gold == null ? "none" : gold.IsCarried ? "carried" : "floor");
        var be = BossEncounter.Instance;
        Kv("boss", be != null && be.Boss != null ? (be.Boss.Health.IsDead ? "dead" : "alive") : "none");
        if (ZoneManager.Instance != null) Kv("zonesOpen", ZoneManager.Instance.OpenCount);
        var box = Object.FindAnyObjectByType<MysteryBox>();
        if (box != null) { var bp = box.transform.position; Kv("box", box.StateName + "@" + bp.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + bp.z.ToString("0.0", CultureInfo.InvariantCulture)); }
        var cm = CardManager.Instance;
        if (cm != null)
        {
            Kv("cards", cm.Taken.Count);
            Kv("offer", cm.CurrentOffer != null ? cm.CurrentOffer.Length + "@J" + (NetGame.IndexOf(cm.Chooser) + 1) : "none");
            Kv("doorCost", RunModifiers.Get(Stat.DoorCost).ToString("0.00", CultureInfo.InvariantCulture));
        }
        var key = MasterKey.Instance;
        if (key != null) Kv("key", key.Used ? "used" : key.Holder != null ? "J" + (NetGame.IndexOf(key.Holder) + 1) : "floor");
        var bm = BoatMission.Instance;
        if (bm != null) Kv("quest", "P" + (bm.Power ? 1 : 0) + "G" + (bm.Gas ? 1 : 0) + "R" + (bm.Radio ? 1 : 0) + "C" + (bm.Called ? 1 : 0) + "D" + (bm.Complete ? 1 : 0));
        // La Serenisima
        if (bm != null && bm.HasTasks) Kv("tasks", "S" + (bm.Shovel ? 1 : 0) + "G" + (bm.Giuseppe ? 1 : 0) + "O" + (bm.Gondolier ? 1 : 0) + "B" + (bm.Power ? 1 : 0) + "@" + bm.GondolierSpot);
        var fences = Object.FindObjectsByType<ZoneFence>(FindObjectsInactive.Include);
        if (fences.Length > 0) Kv("fences", fences.Count(f => f.Up) + "/" + fences.Length);
        var tide = AcquaAlta.Instance;
        if (tide != null) Kv("tide", (tide.Flooding ? "up" : "down") + (tide.SwimActive ? ",swim" : "") + "," + tide.WaterY.ToString("0.00", CultureInfo.InvariantCulture));
        var stops = Object.FindObjectsByType<GondolaStop>();
        if (stops.Length > 0) Kv("gondolasBusy", stops.Count(s => s.Busy));
        foreach (var look in Object.FindObjectsByType<MaskLook>())
        {
            var owner = look.GetComponentInParent<EnemyHealth>();
            string paint = look.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null && m.name.StartsWith("VN_MaskPaint_")).Select(m => m.name.Substring(13)).FirstOrDefault() ?? "?";
            if (owner != null && owner.NetId > 0) Kv("m" + owner.NetId, paint);
        }
        if (gm != null)
            foreach (var p in gm.Players)
            {
                var np = p != null ? p.GetComponent<NetPlayer>() : null;
                if (np == null || np.IsLocal) continue;
                var q = p.transform.position;
                Kv("remote" + (np.Index + 1), q.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + q.z.ToString("0.0", CultureInfo.InvariantCulture) +
                   ",age=" + (Time.unscaledTime - np.LastSnapTime).ToString("0.00", CultureInfo.InvariantCulture) + ",downed=" + p.IsDowned + ",dead=" + p.IsDead + ",hp=" + p.CurrentHealth +
                   ",gun=" + (p.GetComponent<PlayerShooter>().Current.data != null ? p.GetComponent<PlayerShooter>().Current.data.name : "-"));
            }
        Kv("toasts", string.Join(" / ", toasts));
        Send(tag, sb.ToString());
    }
}
