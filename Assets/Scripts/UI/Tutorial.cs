using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// "Isla del Faro (tutorial)" (user feedback 1, phase 7): a simple guided start on Isla del Faro, solo only, about
// 5 minutes. The rounds wait (RoundManager.Hold) while a panel at the top asks for one thing at a time: move, look,
// jump, shoot a practice old man, reload, the ukulele, the false teeth, the animal's ability, open the first door and
// pick a card, a drink (the light is given for free here) and the Máquina de Granizados. Then the normal game starts
// at round 1. Practice old men can't hurt you (god mode until the end).
// Chosen in the map selector (MapData.tutorial -> Tutorial.Requested); split-screen and online play the normal map.
public class Tutorial : MonoBehaviour
{
    public static bool Requested;                 // MainMenu.PlayMap: the tutorial map was picked
    public static Tutorial Instance { get; private set; }
    public int Step { get; private set; }         // tests
    public string StepName { get; private set; } = "";
    public bool Done { get; private set; }
    public string Note { get; private set; } = "";   // tests: what the current step is waiting on

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Requested = false; Instance = null; }

    GameManager gm;
    PlayerHealth player;
    PlayerShooter shooter;
    PlayerMelee melee;
    PlayerGrenade grenade;
    PlayerAbility ability;
    PlayerController controller;
    Text title, body;
    GameObject panel;
    readonly List<EnemyHealth> practice = new List<EnemyHealth>();
    const int Steps = 12;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; RoundManager.Hold = false; }

    IEnumerator Start()
    {
        gm = GameManager.Instance;
        RoundManager.Hold = true;
        yield return null;   // the player is spawned in GameManager.Start
        player = gm.Player;
        if (player == null) { Destroy(this); yield break; }
        shooter = player.GetComponent<PlayerShooter>();
        melee = player.GetComponent<PlayerMelee>();
        grenade = player.GetComponent<PlayerGrenade>();
        ability = player.GetComponent<PlayerAbility>();
        controller = player.GetComponent<PlayerController>();
        player.GodMode = true;
        BuildPanel();
        yield return Run();
    }

    void BuildPanel()
    {
        var canvas = UIKit.CreateCanvas("TutorialCanvas", 5);
        var p = UIKit.Anchored("Tutorial", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(880, 200));
        UIKit.Image(p, UIKit.PanelColor);
        UIKit.Image(UIKit.Rect("Stripe", p, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), Vector2.zero), UIKit.Accent);
        title = UIKit.Label("Title", p, new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(840, 34), "", 24, TextAnchor.MiddleCenter, UIKit.Accent);
        body = UIKit.Label("Body", p, new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(840, 140), "", 26);
        panel = canvas.gameObject;
    }

    void Update()
    {
        // the cards and the pause cover the screen: the panel steps aside
        if (panel != null) panel.SetActive(gm != null && gm.State == GameState.Playing);
    }

    void Show(int step, string name, string text)
    {
        Step = step;
        StepName = name;
        if (title != null) title.text = $"TUTORIAL · {step} / {Steps}";
        if (body != null) body.text = text;
    }

    IEnumerator Good(string text = "¡Bien!")
    {
        if (body != null) body.text = text;
        yield return new WaitForSeconds(1.2f);
    }

    EnemyHealth Practice(float health, float distance = 7f, float side = 0f)
    {
        var rm = RoundManager.Instance;
        if (rm == null) return null;
        // a free spot near the player (never out of an entrance: the practice old man is there at once)
        var t = player.transform;
        EnemyAI ai = null;
        for (int i = 0; i < 12 && ai == null; i++)
        {
            var dir = Quaternion.Euler(0f, i * 30f, 0f) * t.forward;
            float d = Mathf.Max(2f, distance - (i / 4) * 2f);
            ai = rm.SpawnEnemy(null, t.position + dir * d + t.right * side);
        }
        if (ai == null) return null;
        var h = ai.GetComponent<EnemyHealth>();
        h.Init(health);
        practice.Add(h);
        return h;
    }

    void ClearPractice()
    {
        foreach (var h in practice) if (h != null && !h.IsDead) h.TakeDamage(999999f, false, true);
        practice.Clear();
    }

    void EnsurePoints(int atLeast)
    {
        if (gm.Points < atLeast) gm.AddPoints(atLeast - gm.Points, PointSource.Bonus);
    }

    IEnumerator Run()
    {
        // 1. move
        Show(1, "move", "Muévete: stick izquierdo / WASD.\nPulsa L3 / Mayús para correr.");
        Vector3 from = player.transform.position;
        yield return new WaitUntil(() => Vector3.Distance(Flat(player.transform.position), Flat(from)) > 5f);
        yield return Good();

        // 2. look
        Show(2, "look", "Mira alrededor: stick derecho / ratón.");
        Vector3 fwd = Flat(Camera.main != null ? Camera.main.transform.forward : player.transform.forward);
        yield return new WaitUntil(() => Camera.main == null || Vector3.Angle(Flat(Camera.main.transform.forward), fwd) > 60f);
        yield return Good();

        // 3. jump
        Show(3, "jump", "Salta: X / Espacio.");
        float air = 0f;
        while (air < 0.15f) { air = controller != null && !controller.IsGrounded ? air + Time.deltaTime : 0f; yield return null; }
        yield return Good();

        // 4. shoot a practice old man
        Show(4, "shoot", "Un abuelo de prácticas. Apunta con L2 / clic derecho y dispara con R2 / clic izquierdo.\n(Aquí no te pueden hacer daño.)");
        bool gunKill = false, shot = false;
        System.Action<bool, bool> onFired = (hit, kill) => { shot = true; if (kill) gunKill = true; };
        shooter.Fired += onFired;
        var target = Practice(120f);
        while (!gunKill)
        {
            if (shot && target != null && target.IsDead) break;   // shot down (the last hit may have been a lucky other one)
            if (target == null || target.IsDead) target = Practice(120f);
            Note = target == null ? "no target" : $"{target.name} {target.Current:0}/{target.MaxHealth:0} dead {target.IsDead} at {target.transform.position}";
            yield return null;
        }
        shooter.Fired -= onFired;
        yield return Good("¡Bien! Cada impacto y cada abuelo abatido dan CONCHAS (arriba a la derecha).");

        // 5. reload
        Show(5, "reload", "Recarga: □ / R. Ojo: si tienes algo que usar delante (el karaoke, una puerta...), □ lo usa.\nAléjate un poco y recarga.");
        yield return new WaitUntil(() => shooter.IsReloading || shooter.Current.magazine >= shooter.MagazineSize(shooter.Current) && shooter.Current.reserve == 0);
        yield return Good();

        // 6. the ukulele
        Show(6, "melee", "Cuerpo a cuerpo con el ukelele: R3 / V. Acércate y dale un buen acorde.");
        bool meleeKill = false, swung = false;
        System.Action<bool> onHit = killed => { if (killed) meleeKill = true; };
        System.Action onSwing = () => swung = true;
        if (melee != null) { melee.Hit += onHit; melee.Swung += onSwing; }
        target = Practice(140f, 4f);
        while (!meleeKill && melee != null)
        {
            if (swung && target != null && target.IsDead) break;
            if (target == null || target.IsDead) target = Practice(140f, 4f);
            yield return null;
        }
        if (melee != null) { melee.Hit -= onHit; melee.Swung -= onSwing; }
        yield return Good();

        // 7. the false teeth
        Show(7, "grenade", "Un grupito. Lánzales la dentadura postiza: R1 / G. Muerde y aturde.\n(Tienes dos; la Nevera de munición te las devuelve.)");
        bool thrown = false;
        System.Action<Vector3, Vector3> onThrown = (a, b) => thrown = true;
        if (grenade != null) { grenade.Refill(); grenade.Thrown += onThrown; }
        for (int i = -1; i <= 1; i++) Practice(300f, 8f, i * 1.2f);
        yield return new WaitUntil(() => thrown || grenade == null);
        if (grenade != null) grenade.Thrown -= onThrown;
        yield return new WaitForSeconds(2.5f);
        ClearPractice();
        yield return Good();

        // 8. the animal's ability
        Show(8, "ability", "Tu animal tiene una habilidad: L1 / E. Mira la barra de abajo a la izquierda.");
        yield return new WaitUntil(() => ability == null || !ability.IsReady || ability.IsActive);
        yield return Good();

        // 9. a door
        var door = FindObjectsByType<ZoneDoor>().Where(d => !d.RequiresMission).OrderBy(d => Vector3.Distance(d.transform.position, player.transform.position)).FirstOrDefault();
        int open = ZoneManager.Instance != null ? ZoneManager.Instance.OpenCount : 0;
        EnsurePoints(door != null ? door.Cost + 200 : 1000);
        Show(9, "door", "Las puertas abren zonas nuevas. Ve a la puerta (cartel rojo) y págala con □ / F.\nSolo se puede abrir UNA puerta por ronda.");
        yield return new WaitUntil(() => ZoneManager.Instance == null || ZoneManager.Instance.OpenCount > open);

        // 10. the card
        Show(10, "card", "Cada puerta te deja elegir una CARTA: una bendición y una maldición, para toda la partida.");
        var cards = CardManager.Instance;
        yield return new WaitUntil(() => cards == null || cards.CurrentOffer == null);
        yield return new WaitUntil(() => gm.State == GameState.Playing);
        yield return Good("¡Bien! Las cartas que llevas salen en la pausa (OPTIONS / Esc).");

        // 11. a drink
        if (BoatMission.Instance != null && !BoatMission.PowerOn) BoatMission.Instance.TurnPowerOn();
        var drinks = player.GetComponent<PlayerDrinks>();
        int owned = drinks != null ? drinks.Owned.Count : 0;
        var machine = FindObjectsByType<DrinkMachine>().OrderBy(m => Vector3.Distance(m.transform.position, player.transform.position)).FirstOrDefault();
        EnsurePoints(4000);
        Show(11, "drink", "Las BEBIDAS te mejoran (con efectos secundarios). Compra una en la máquina más cercana.\nEn la partida de verdad, primero hay que DAR LA LUZ en el cuadro eléctrico; aquí ya está dada.");
        yield return new WaitUntil(() => drinks == null || drinks.Owned.Count > owned || machine == null);
        yield return Good();

        // 12. the Máquina de Granizados
        var box = FindAnyObjectByType<MysteryBox>();
        if (box != null)
        {
            var zone = ZoneManager.Instance != null ? ZoneManager.Instance.Zones.Where(z => z.IsOpen && z.BoxSpots.Any()).OrderBy(z => Vector3.Distance(z.transform.position, player.transform.position)).FirstOrDefault() : null;
            if (!box.IsAvailable && zone != null) box.AppearIn(zone);
            EnsurePoints(box.Cost + 500);
            Show(12, "box", "La MÁQUINA DE GRANIZADOS da un arma al azar con 1, 2 o 3 estrellas. Úsala y coge el arma.\nLa LIJADORA DEL TALLER le añade estrellas y el MICROONDAS la deja mejor que nada.");
            bool seenUse = false;
            float until = Time.time + 120f;
            while (Time.time < until)
            {
                if (box.StateName != "Ready") seenUse = true;
                if (seenUse && box.StateName == "Gone") break;
                yield return null;
            }
            yield return Good();
        }

        // the end: the normal game starts
        Show(Steps, "end", "¡Listo! Ahora va en serio: sobrevive a las rondas, abre zonas y cumple la lista de arriba a la\nizquierda para que venga el barco. Empieza la RONDA 1...");
        yield return new WaitForSeconds(6f);
        ClearPractice();
        player.GodMode = false;
        RoundManager.Hold = false;
        Done = true;
        if (panel != null) Destroy(panel);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
