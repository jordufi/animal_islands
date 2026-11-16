// Tools > Island > Quick Checks: design rules on the numbers in Assets/Data, no Play Mode, under 1 s.
// Run it after touching any ScriptableObject or balance code (the play tests are for behaviour).
// Rule: every new number in Assets/Data that has a design rule behind it gets a line here.
// Results in EditorPrefs "QuickChecks.Log" / ".Status" ("done" / "failed"), `QuickChecks.Report` has the text.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class QuickChecks
{
    const string Key = "QuickChecks.";
    const string Data = "Assets/Data/";
    public static string Report { get; private set; } = "";
    public static int Failures { get; private set; }

    static StringBuilder sb;
    static int ok;

    [MenuItem("Tools/Island/Quick Checks")]
    public static void Run()
    {
        sb = new StringBuilder();
        ok = 0; Failures = 0;
        var eco = Load<EconomyData>("Economy.asset");
        var pistol = Load<WeaponData>("Weapons/Pistol.asset");
        var shotgun = Load<WeaponData>("Weapons/Shotgun.asset");
        var sniper = Load<WeaponData>("Weapons/Sniper.asset");
        var smg = Load<WeaponData>("Weapons/SMG.asset");
        var uku = Load<MeleeData>("Weapons/Ukulele.asset");
        var old = Load<EnemyVariant>("Enemies/Jubilado.asset");
        var track = Load<EnemyVariant>("Enemies/JubiladoChandal.asset");
        var gym = Load<EnemyVariant>("Enemies/AbueloCulturista.asset");
        var pina = Load<DrinkData>("Drinks/PinaColapso.asset");
        if (eco == null || pistol == null || shotgun == null || sniper == null || smg == null || uku == null || old == null || pina == null)
        {
            Fail("missing data assets (run Tools > Island > Setup Test Map)");
            Finish();
            return;
        }

        // ---- economy: aiming must pay, bullet hoses must not print conchas
        Check(eco.headshotKillPoints > eco.killPoints, $"headshot kill pays more than a body kill ({eco.headshotKillPoints} > {eco.killPoints})");
        Check(eco.fastHitPoints > 0 && eco.fastHitPoints < eco.hitPoints, $"fast guns pay less per hit ({eco.fastHitPoints} < {eco.hitPoints})");
        Check(eco.HitPointsFor(smg.fireInterval) == eco.fastHitPoints, $"the Thermomix ({1f / smg.fireInterval:F1} shots/s) earns the fast-gun hit pay");
        Check(eco.HitPointsFor(pistol.fireInterval) == eco.hitPoints && eco.HitPointsFor(shotgun.fireInterval) == eco.hitPoints, "pistol and shotgun earn the normal hit pay");

        // ---- rounds: old man health
        Check(Mathf.Approximately(RoundManager.BaseHealth(1), 150f), $"round 1 old man has 150 health ({RoundManager.BaseHealth(1)})");
        Check(Mathf.Approximately(RoundManager.BaseHealth(9), 950f), $"round 9 old man has 950 health ({RoundManager.BaseHealth(9)})");
        Check(Mathf.Abs(RoundManager.BaseHealth(10) - 1045f) < 1f, $"round 10 old man has ~1045 health ({RoundManager.BaseHealth(10):F0})");

        // ---- boss: never rushable, grows with the round
        float Boss(int r) => Mathf.Max(eco.bossMinHealth, eco.bossHealthInEnemies * RoundManager.BaseHealth(r));
        Check(Boss(1) >= 30000f, $"boss in round 1 has at least 30000 health ({Boss(1):F0}): no pistol rush");
        Check(Boss(14) > Boss(7) && Boss(7) >= Boss(1), $"boss health grows with the round (r1 {Boss(1):F0}, r7 {Boss(7):F0}, r14 {Boss(14):F0})");
        float ammoDamage = smg.damage * smg.upgradeDamageMultiplier * (smg.magazineSize + smg.maxReserve) * smg.upgradeMagazineMultiplier;
        Check(ammoDamage >= Boss(14) * 0.5f,
            $"an upgraded Thermomix carries at least half the damage a round-14 boss needs ({ammoDamage:F0} vs {Boss(14):F0})");

        // ---- weapons
        foreach (var w in AllWeapons())
        {
            if (w.wallCost > 0)
                Check(w.wallAmmoCost * 2 == w.wallCost, $"{w.displayName}: wall ammo costs half the weapon ({w.wallAmmoCost} x2 = {w.wallCost})");
            if (w.boxWeight > 0f)
                Check(w.model != null, $"{w.displayName}: box weapon has a model");
        }
        Check(shotgun.wallCost > 500 && smg.wallCost > shotgun.wallCost, $"wall prices climb with power (chancla {shotgun.wallCost}, Thermomix {smg.wallCost})");
        Check(sniper.damage * sniper.upgradeDamageMultiplier >= RoundManager.BaseHealth(14),
            $"an upgraded Catalejo one-shots a round-14 old man ({sniper.damage * sniper.upgradeDamageMultiplier:F0} vs {RoundManager.BaseHealth(14):F0})");
        Check(eco.packAPunchCost > 0, $"Pack-a-Punch cost comes from Economy ({eco.packAPunchCost})");

        // ---- ukulele: one hit in round 1 only
        Check(uku.damage >= RoundManager.BaseHealth(1), $"ukulele one-shots a round-1 old man ({uku.damage} >= {RoundManager.BaseHealth(1)})");
        Check(uku.damage < RoundManager.BaseHealth(2), $"ukulele no longer one-shots in round 2 ({uku.damage} < {RoundManager.BaseHealth(2)})");
        Check(uku.lungeRange <= 3f, $"ukulele lunge is short ({uku.lungeRange} m)");
        Check(eco.meleeKillPoints <= eco.headshotKillPoints, "a melee kill never pays more than a headshot kill");

        // ---- old men: hits to put the player down
        int baseHp = PlayerBaseHealth();
        float pinaMult = pina.mods != null ? pina.mods.Where(m => m.stat == Stat.MaxHealth).Select(m => m.multiplier).DefaultIfEmpty(1f).Aggregate(1f, (a, b) => a * b) : 1f;
        int hits = Mathf.CeilToInt(baseHp / (float)old.damage);
        int hitsPina = Mathf.CeilToInt(baseHp * pinaMult / (float)old.damage);
        Check(hits == 3, $"an old man needs 3 hits to down the player without Piña ({baseHp} health / {old.damage} damage = {hits})");
        Check(hitsPina == 5, $"... and 5 with Piña ({Mathf.RoundToInt(baseHp * pinaMult)} health = {hitsPina})");
        if (track != null) Check(track.healthMultiplier < 1f && track.moveSpeed > old.moveSpeed, "tracksuit runner is fast and fragile");
        if (gym != null) Check(gym.healthMultiplier > 1f && gym.damage >= old.damage, "culturista is a tank that hits harder");

        // ---- co-op / power-ups sanity
        var pu = Load<PowerUpData>("PowerUps.asset");
        if (pu != null)
        {
            Check(pu.maxPerRound >= 1 && pu.maxActive >= 1, $"power-up caps are set (per round {pu.maxPerRound}, active {pu.maxActive})");
            Check(pu.ammoDropChance + pu.otherDropChance < 0.2f, "power-ups stay rare (under 20 % of kills)");
        }

        // ---- Villajubilada: the encierro, the traca, the fiesta old men
        var enc = Load<EncierroData>("Encierro.asset");
        if (enc != null)
        {
            const float sprint = IslaZombieSetup.SprintSpeed;
            Check(enc.speed > sprint * 1.3f, $"the herd can't be outrun ({enc.speed} m/s vs sprint {sprint})");
            Check(enc.firstRunSpeed > sprint && enc.firstRunSpeed < enc.speed, $"the first run is slower but still faster than a sprint ({enc.firstRunSpeed} m/s)");
            Check(enc.warning >= 4f, $"the rocket warns at least 4 s before the lorry opens ({enc.warning} s)");
            Check(enc.playerDamage >= 35 && enc.playerDamage < 100, $"a bull hurts more than an old man and never kills from full health ({enc.playerDamage})");
            Check(enc.playerImmunity >= 0.5f, $"the same pass can't hit twice in a row ({enc.playerImmunity} s)");
            Check(enc.throwSpeed <= 8f && enc.throwDamage <= 25, $"flowerpots are slow and light: you dodge them ({enc.throwSpeed} m/s, {enc.throwDamage})");
            Check(enc.every == 5, $"encierro every 5 rounds ({enc.every})");
            Check(eco.tracaCost >= eco.killPoints * 6, $"a traca costs more than six kills pay ({eco.tracaCost}): it is for surviving, not for farming");
            Check(eco.tracaCooldown >= 30f && eco.tracaPlayerDamage < enc.playerDamage, $"traca: cooldown {eco.tracaCooldown} s, burns the player {eco.tracaPlayerDamage}");
        }
        var bombo = Load<EnemyVariant>("Enemies/AbueloBombo.asset");
        var petardos = Load<EnemyVariant>("Enemies/AbuelaPetardos.asset");
        if (bombo != null) Check(bombo.auraRadius > 0f && bombo.auraSpeedFactor > 1f && bombo.auraSpeedFactor <= 1.5f && bombo.moveSpeed < old.moveSpeed,
            $"el del bombo is slow himself and speeds the others up a little (x{bombo.auraSpeedFactor} within {bombo.auraRadius} m)");
        if (petardos != null) Check(petardos.throwsChanclas && petardos.throwCloudRadius > 0f && petardos.throwCloudDps * petardos.throwCloudSeconds < 60f,
            $"la de los petardos: her patch denies a spot, it doesn't kill ({petardos.throwCloudDps * petardos.throwCloudSeconds:F0} damage if you stand in it)");
        Finish();
    }

    static int PlayerBaseHealth()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Player_Frog.prefab");
        var ph = prefab != null ? prefab.GetComponent<PlayerHealth>() : null;
        if (ph == null) return 100;
        var p = new SerializedObject(ph).FindProperty("baseMaxHealth");
        return p != null ? p.intValue : 100;
    }

    static WeaponData[] AllWeapons() =>
        AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/Data/Weapons" })
            .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g))).Where(w => w != null).ToArray();

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Data + path);

    static void Check(bool pass, string text)
    {
        if (pass) { ok++; sb.AppendLine("OK   " + text); }
        else Fail(text);
    }

    static void Fail(string text) { Failures++; sb.AppendLine("FAIL " + text); }

    static void Finish()
    {
        sb.AppendLine($"{ok} OK / {Failures} FAIL");
        Report = sb.ToString();
        EditorPrefs.SetString(Key + "Log", Report);
        EditorPrefs.SetString(Key + "Status", Failures == 0 ? "done" : "failed");
        Debug.Log("[QuickChecks]\n" + Report);
    }
}
