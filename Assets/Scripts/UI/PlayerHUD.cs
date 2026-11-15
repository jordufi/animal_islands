using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// One player's part of the HUD, drawn inside their camera's part of the screen (split-screen): health,
// weapon + ammo, reload, ability, drinks, crosshair, use prompt, what they carry, damage flash, the
// Eagle Eye markers and, in co-op, their tag (J1..J4), "CAÍDO" + bleed-out timer, the revive bar and
// markers over downed teammates. The shared HUD (round, conchas, toasts, boss, cards, menus) is GameHUD.
public class PlayerHUD : MonoBehaviour
{
    PlayerHealth player;
    PlayerSlot slot;
    PlayerShooter shooter;
    PlayerInteractor interactor;
    PlayerDrinks drinks;
    PlayerAbility ability;
    PlayerCarry carry;
    Camera cam;

    RectTransform canvasRect, view;
    Text healthText, weaponText, ammoText, otherWeaponText, promptText, abilityText, carryText, keyText, tagText, downText, reviveText;
    RectTransform healthFill, reloadFill, abilityFill, reviveFill;
    Image healthFillImage, damageFlash;
    Image[] crosshair;
    GameObject reloadBar, downPanel, reviveBar;
    Transform drinksRoot;
    readonly List<Image> enemyMarkers = new List<Image>();
    readonly List<Text> mateMarkers = new List<Text>();
    float crosshairFlashUntil, damageFlashAlpha;

    public PlayerHealth Player => player;
    public string Prompt => promptText != null ? promptText.text : "";
    public string DownText => downPanel != null && downPanel.activeSelf ? downText.text : "";
    public RectTransform View => view;

    // playerCount = views on this screen (scale); coop = more players in the run (online: elsewhere)
    public static PlayerHUD Create(PlayerHealth p, int playerCount, bool coop = false)
    {
        var slot = PlayerSlot.Of(p);
        var canvas = UIKit.CreateCanvas("HUD_" + (slot != null ? slot.Tag : "J1"), 9);
        // split-screen: smaller elements in smaller views
        float k = playerCount <= 1 ? 1f : playerCount == 2 ? 0.85f : 0.7f;
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f) / k;
        var hud = canvas.gameObject.AddComponent<PlayerHUD>();
        hud.Bind(p, playerCount, coop || playerCount > 1);
        if (slot != null) slot.Hud = hud;
        return hud;
    }

    void Bind(PlayerHealth p, int playerCount, bool coop)
    {
        player = p;
        slot = PlayerSlot.Of(p);
        shooter = p.GetComponent<PlayerShooter>();
        interactor = p.GetComponent<PlayerInteractor>();
        drinks = p.GetComponent<PlayerDrinks>();
        ability = p.GetComponent<PlayerAbility>();
        carry = p.GetComponent<PlayerCarry>();
        cam = PlayerSlot.CameraOf(p);
        Build(playerCount, coop);

        player.HealthChanged += OnHealthChanged;
        player.Damaged += _ => damageFlashAlpha = 0.45f;
        shooter.WeaponsChanged += RefreshWeapon;
        shooter.AmmoChanged += RefreshWeapon;
        shooter.Fired += (hit, killed) => { if (hit) { SetCrosshair(new Color(1f, 0.25f, 0.2f)); crosshairFlashUntil = Time.time + (killed ? 0.25f : 0.1f); } };
        var melee = p.GetComponent<PlayerMelee>();
        if (melee != null) melee.Hit += killed => { SetCrosshair(new Color(1f, 0.6f, 0.1f)); crosshairFlashUntil = Time.time + (killed ? 0.3f : 0.12f); };
        drinks.Changed += RefreshDrinks;
        OnHealthChanged(player.CurrentHealth, player.MaxHealth);
        RefreshWeapon();
        RefreshDrinks();
    }

    void OnDestroy()
    {
        if (player != null) player.HealthChanged -= OnHealthChanged;
        if (shooter != null) { shooter.WeaponsChanged -= RefreshWeapon; shooter.AmmoChanged -= RefreshWeapon; }
        if (drinks != null) drinks.Changed -= RefreshDrinks;
    }

    // ---------------------------------------------------------------- build
    void Build(int playerCount, bool coop)
    {
        canvasRect = (RectTransform)transform;
        var r = cam != null ? cam.rect : new Rect(0f, 0f, 1f, 1f);
        view = UIKit.Rect("View", transform, r.min, r.max, Vector2.zero, Vector2.zero);

        var flash = UIKit.Stretch("DamageFlash", view);
        damageFlash = UIKit.Image(flash, new Color(0.8f, 0f, 0f, 0f));

        // top-left: health (below the shared round number for J1 / solo)
        bool underRound = playerCount <= 1;   // split-screen: the round is in the middle strip (GameHUD)
        float hy = underRound ? -170f : -40f;
        var bar = UIKit.Anchored("HealthBar", view, new Vector2(0, 1), new Vector2(40, hy), new Vector2(420, 36));
        UIKit.Image(bar, new Color(0, 0, 0, 0.6f));
        var area = UIKit.Rect("Area", bar, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        healthFill = UIKit.Stretch("Fill", area);
        healthFillImage = UIKit.Image(healthFill, new Color(0.3f, 0.85f, 0.35f));
        healthText = UIKit.Text(UIKit.Stretch("Text", bar), "", 24);
        if (coop && slot != null)
        {
            tagText = UIKit.Label("Tag", view, new Vector2(0, 1), new Vector2(470, hy + 4), new Vector2(400, 44), slot.Tag + " · " + slot.Animal.displayName, 30, TextAnchor.MiddleLeft, slot.Color);
            var stripe = UIKit.Anchored("Stripe", view, new Vector2(0, 1), new Vector2(40, hy - 40), new Vector2(420, 6));
            UIKit.Image(stripe, slot.Color);
        }

        // top-right: carried item, key
        carryText = UIKit.Label("Carry", view, new Vector2(1, 1), new Vector2(-40, -140), new Vector2(700, 36), "", 26, TextAnchor.UpperRight, new Color(1f, 0.9f, 0.2f));
        keyText = UIKit.Label("Key", view, new Vector2(1, 1), new Vector2(-40, -180), new Vector2(700, 36), "", 26, TextAnchor.UpperRight, new Color(1f, 0.8f, 0.1f));

        // bottom-right: weapon
        otherWeaponText = UIKit.Label("OtherWeapon", view, new Vector2(1, 0), new Vector2(-40, 150), new Vector2(600, 32), "", 24, TextAnchor.LowerRight, new Color(1, 1, 1, 0.6f));
        weaponText = UIKit.Label("Weapon", view, new Vector2(1, 0), new Vector2(-40, 100), new Vector2(600, 44), "", 36, TextAnchor.LowerRight);
        ammoText = UIKit.Label("Ammo", view, new Vector2(1, 0), new Vector2(-40, 30), new Vector2(600, 70), "", 60, TextAnchor.LowerRight);
        var rb = UIKit.Anchored("ReloadBar", view, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(200, 10));
        UIKit.Image(rb, new Color(0, 0, 0, 0.6f));
        reloadFill = UIKit.Stretch("Fill", rb);
        UIKit.Image(reloadFill, Color.white);
        reloadBar = rb.gameObject;

        // bottom-left: ability + drinks
        var ab = UIKit.Anchored("Ability", view, new Vector2(0, 0), new Vector2(40, 30), new Vector2(380, 36));
        UIKit.Image(ab, new Color(0, 0, 0, 0.6f));
        abilityFill = UIKit.Rect("Fill", ab, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
        UIKit.Image(abilityFill, new Color(0.3f, 0.7f, 1f));
        abilityText = UIKit.Text(UIKit.Stretch("Text", ab), "", 22);
        drinksRoot = UIKit.Anchored("Drinks", view, new Vector2(0, 0), new Vector2(40, 80), new Vector2(600, 50));

        // centre: crosshair, prompt
        var cross = UIKit.Anchored("Crosshair", view, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        crosshair = new[]
        {
            UIKit.Image(UIKit.Anchored("Dot", cross, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6)), Color.white),
            UIKit.Image(UIKit.Anchored("Up", cross, new Vector2(0.5f, 0.5f), new Vector2(0, 15), new Vector2(4, 14)), Color.white),
            UIKit.Image(UIKit.Anchored("Down", cross, new Vector2(0.5f, 0.5f), new Vector2(0, -15), new Vector2(4, 14)), Color.white),
            UIKit.Image(UIKit.Anchored("Left", cross, new Vector2(0.5f, 0.5f), new Vector2(-15, 0), new Vector2(14, 4)), Color.white),
            UIKit.Image(UIKit.Anchored("Right", cross, new Vector2(0.5f, 0.5f), new Vector2(15, 0), new Vector2(14, 4)), Color.white),
        };
        promptText = UIKit.Label("Prompt", view, new Vector2(0.5f, 0f), new Vector2(0, 200), new Vector2(1400, 50), "", 30);

        // co-op: downed overlay and revive bar
        var dp = UIKit.Anchored("Down", view, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1200, 120));
        downText = UIKit.Text(dp, "", 40, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.25f));
        downPanel = dp.gameObject;
        downPanel.SetActive(false);
        var rv = UIKit.Anchored("Revive", view, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(420, 26));
        UIKit.Image(rv, new Color(0, 0, 0, 0.7f));
        reviveFill = UIKit.Rect("Fill", rv, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
        UIKit.Image(reviveFill, new Color(0.4f, 1f, 0.5f));
        reviveText = UIKit.Label("Text", rv, new Vector2(0.5f, 1f), new Vector2(0, 30), new Vector2(800, 36), "", 26);
        reviveBar = rv.gameObject;
        reviveBar.SetActive(false);
    }

    // ---------------------------------------------------------------- refresh
    void Update()
    {
        if (player == null) return;
        var gm = GameManager.Instance;

        if (interactor != null)
        {
            var f = interactor.Focus;
            promptText.text = f != null && !player.IsDowned ? f.Prompt(interactor) : "";
        }

        reloadBar.SetActive(shooter.IsReloading);
        if (shooter.IsReloading) reloadFill.anchorMax = new Vector2(Mathf.Clamp01(shooter.ReloadProgress), 1f);

        if (ability != null && ability.Animal != null)
        {
            float fill = ability.IsActive ? 1f : 1f - ability.CooldownFraction;
            abilityFill.anchorMax = new Vector2(fill, 1f);
            abilityText.text = "L1 " + ability.Animal.abilityName + (ability.IsActive ? "  ¡ACTIVA!" : ability.IsReady ? "  LISTA" : "  " + Mathf.CeilToInt(ability.CooldownRemaining) + "s");
            UpdateEnemyMarkers(ability.IsActive && ability.Animal.ability == AbilityType.EagleEye);
        }

        carryText.text = carry != null && carry.Carried != null ? "Llevas: " + carry.Carried.DisplayName + " (□ soltar)" : "";
        keyText.text = MasterKey.Instance != null && MasterKey.Instance.IsHeldBy(interactor) ? (BoatMission.Instance != null && BoatMission.Instance.HasMission ? "LLAVE MAESTRA: vale por una pieza al llamar al barco" : "LLAVE MAESTRA: abre el muelle gratis") : "";

        UpdateCoop(gm);

        if (crosshairFlashUntil > 0f && Time.time >= crosshairFlashUntil) { crosshairFlashUntil = 0f; SetCrosshair(Color.white); }
        if (damageFlashAlpha > 0f)
        {
            damageFlashAlpha = Mathf.MoveTowards(damageFlashAlpha, 0f, Time.unscaledDeltaTime * 1.5f);
            var c = damageFlash.color; c.a = damageFlashAlpha; damageFlash.color = c;
        }
    }

    void UpdateCoop(GameManager gm)
    {
        // downed: bleed-out timer / bled out: spectating
        if (player.InLastStand)
        {
            downPanel.SetActive(true);
            var by = player.BeingRevivedBy != null ? PlayerSlot.Of(player.BeingRevivedBy) : null;
            downText.text = by != null ? "¡" + by.Tag + " TE ESTÁ LEVANTANDO!" : "CAÍDO\nte desangras en " + Mathf.CeilToInt(player.BleedOutRemaining) + " s";
        }
        else if (player.IsDead && gm != null && gm.IsCoop)
        {
            downPanel.SetActive(true);
            downText.text = "TE HAS DESANGRADO\nvuelves la próxima ronda";
        }
        else downPanel.SetActive(false);

        // revive bar: reviving someone, or being revived
        float progress = 0f;
        string label = "";
        if (interactor != null && interactor.Reviving != null)
        {
            progress = interactor.ReviveProgress;
            var other = PlayerSlot.Of(interactor.Reviving);
            label = "Levantando a " + (other != null ? other.Tag : "tu compañero") + "...";
        }
        else if (player.InLastStand && player.BeingRevivedBy != null)
        {
            progress = player.ReviveProgress;
            label = "Te están levantando...";
        }
        reviveBar.SetActive(label.Length > 0);
        if (label.Length > 0)
        {
            reviveFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            reviveText.text = label;
        }

        // markers over downed teammates (with the distance), clamped to the edge of this view
        var downed = gm != null && gm.IsCoop ? gm.Players.Where(p => p != null && p != player && p.InLastStand).ToList() : new List<PlayerHealth>();
        while (mateMarkers.Count < downed.Count)
        {
            var t = UIKit.Label("MateMarker", canvasRect, Vector2.zero, Vector2.zero, new Vector2(240, 60), "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.3f));
            mateMarkers.Add(t);
        }
        for (int i = 0; i < mateMarkers.Count; i++)
        {
            bool on = i < downed.Count && cam != null;
            mateMarkers[i].gameObject.SetActive(on);
            if (!on) continue;
            var p = downed[i];
            var ps = PlayerSlot.Of(p);
            float dist = Vector3.Distance(p.transform.position, player.transform.position);
            mateMarkers[i].text = "+ " +(ps != null ? ps.Tag : "") + "\n" + Mathf.RoundToInt(dist) + " m";
            if (ps != null) mateMarkers[i].color = Color.Lerp(ps.Color, new Color(1f, 0.3f, 0.25f), Mathf.PingPong(Time.time * 2f, 1f));
            Vector3 sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 1.2f);
            Rect pr = cam.pixelRect;
            if (sp.z < 0f) { sp.x = pr.xMax - (sp.x - pr.xMin); sp.y = pr.yMin; }   // behind: bottom edge
            sp.x = Mathf.Clamp(sp.x, pr.xMin + 60f, pr.xMax - 60f);
            sp.y = Mathf.Clamp(sp.y, pr.yMin + 40f, pr.yMax - 40f);
            float scale = canvasRect.rect.width / Screen.width;
            mateMarkers[i].rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) * scale;
        }
    }

    void OnHealthChanged(int current, int max)
    {
        float t = max > 0 ? (float)current / max : 0f;
        healthFill.anchorMax = new Vector2(t, 1f);
        healthFillImage.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.35f), t);
        healthText.text = current + " / " + max;
    }

    void RefreshWeapon()
    {
        var c = shooter.Current;
        weaponText.text = c.data != null ? c.Name + (shooter.InLastStand ? "  (última resistencia)" : "") : "";
        ammoText.text = c.data != null ? c.magazine + " / " + c.reserve : "";
        ammoText.color = c.data != null && c.magazine == 0 ? new Color(1f, 0.3f, 0.3f) : Color.white;
        otherWeaponText.text = shooter.Other.data != null ? "Triángulo: " + shooter.Other.Name : "";
    }

    void RefreshDrinks()
    {
        foreach (Transform t in drinksRoot) Destroy(t.gameObject);
        int i = 0;
        foreach (var d in drinks.Owned)
        {
            var box = UIKit.Anchored(d.id, drinksRoot, new Vector2(0, 0), new Vector2(i * 145, 0), new Vector2(138, 44));
            UIKit.Image(box, d.color);
            UIKit.Text(UIKit.Stretch("Text", box), d.displayName, 18, TextAnchor.MiddleCenter, Color.black, false);
            i++;
        }
    }

    void SetCrosshair(Color c)
    {
        foreach (var img in crosshair) img.color = c;
    }

    void UpdateEnemyMarkers(bool show)
    {
        var enemies = show ? FindObjectsByType<EnemyHealth>().Where(e => !e.IsDead).ToList() : new List<EnemyHealth>();
        while (enemyMarkers.Count < enemies.Count)
        {
            var m = UIKit.Image(UIKit.Anchored("Marker", canvasRect, Vector2.zero, Vector2.zero, new Vector2(18, 18)), new Color(1f, 0.3f, 0.2f, 0.9f));
            enemyMarkers.Add(m);
        }
        for (int i = 0; i < enemyMarkers.Count; i++)
        {
            bool on = i < enemies.Count && cam != null;
            if (on)
            {
                Vector3 sp = cam.WorldToScreenPoint(enemies[i].transform.position + Vector3.up * 1.6f);
                on = sp.z > 0f && cam.pixelRect.Contains(new Vector2(sp.x, sp.y));
                if (on)
                {
                    float scale = canvasRect.rect.width / Screen.width;
                    enemyMarkers[i].rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) * scale;
                }
            }
            enemyMarkers[i].gameObject.SetActive(on);
        }
    }
}
