using UnityEngine;

// Where the conchas come from. All economy numbers live here (Assets/Data/Economy.asset) so they can be
// tuned without code; prices stay on the doors / box / drinks / Pack-a-Punch themselves.
[CreateAssetMenu(menuName = "Island/Economy")]
public class EconomyData : ScriptableObject
{
    public int startingPoints = 500;
    [Tooltip("Per shot that damages an enemy without killing it (once per enemy per shot, not per pellet).")]
    public int hitPoints = 10;
    [Tooltip("Hit pay of fast weapons (more than fastFireRate shots per second): bullet hoses must not print conchas.")]
    public int fastHitPoints = 5;
    public float fastFireRate = 8f;
    public int killPoints = 50;
    public int headshotKillPoints = 100;
    public int meleeKillPoints = 100;
    public int abilityKillPoints = 60;
    public int bossKillPoints = 2000;
    [Tooltip("Old men summoned by the boss: do their non-lethal hits pay hit points? (off = the boss fight is not a farm)")]
    public bool bossMinionsPayHits = false;
    [Tooltip("Boat mission: full rounds to survive after calling the boat.")]
    public int missionDefenseRounds = 1;
    [Header("Rules (user feedback 1)")]
    [Tooltip("Doors the team can open per round: every paid, key or mission door counts, the dock too. 0 = no limit.")]
    public int doorsPerRound = 1;
    [Tooltip("Old men's speed once the Julio Iglesias records go into the karaoke (the song starts), for the rest of the run.")]
    public float julioSpeedFactor = 1.25f;
    [Header("Dentadura postiza (R1 / G): the funny grenade (user feedback 1)")]
    public int dentaduraCount = 2;          // per player; the Nevera de munición fills them up
    public float dentaduraDamage = 400f;    // one bite kills an old man up to round 3
    public float dentaduraRadius = 3.5f;
    public float dentaduraStun = 2.5f;
    public float dentaduraFuse = 1.5f;      // bites when it lands, or after this long
    public float dentaduraThrowSpeed = 13f;
    [Header("Weapon stars 0-3 (user feedback 1, phase 5). The weapon .asset numbers are 1 star")]
    [Tooltip("Damage x by stars 0..3 (the Microondas keeps WeaponData.upgradeDamageMultiplier). To be tuned with real games.")]
    public float[] starDamage = { 0.83f, 1f, 1.2f, 1.44f };
    [Tooltip("Fire rate x by stars 0..3.")]
    public float[] starFireRate = { 0.91f, 1f, 1.1f, 1.21f };
    [Tooltip("Reload speed x by stars 0..3.")]
    public float[] starReload = { 0.87f, 1f, 1.15f, 1.32f };
    [Tooltip("Share of the damage a bullet keeps for EACH old man it goes through, by stars 0..3 (2nd old man x f, 3rd x f^2...).")]
    public float[] starPierceKeep = { 0.25f, 0.4f, 0.6f, 0.8f };
    [Tooltip("Old men a bullet goes through after the first one, by stars 0..3 (user: 0, 1, 2, 3; the Microondas 4). Every weapon, WeaponData.penetration is no longer used.")]
    public int[] starPierceCount = { 0, 1, 2, 3 };
    [Tooltip("Mystery box: chance of 1, 2 and 3 stars.")]
    public float[] boxStarOdds = { 0.6f, 0.3f, 0.1f };
    [Tooltip("Stars of the starting pistol (it is neither a wall nor a box weapon).")]
    public int startingWeaponStars = 1;
    [Tooltip("El Microondas: a category of its own, better than 3 stars in everything.")]
    public float microwaveFireRate = 1.25f;
    public float microwaveReload = 1.4f;
    public float microwavePierceKeep = 1f;
    public int microwavePierceCount = 4;
    [Tooltip("La Lijadora del Taller: +1 star to the weapon in hand (not Microondas weapons, max 3).")]
    public int lijadoraCost = 1000;

    public const int MaxStars = 3;
    static float At(float[] a, int i, float fallback) => a != null && a.Length > 0 ? a[Mathf.Clamp(i, 0, a.Length - 1)] : fallback;
    public float StarDamage(int s) => At(starDamage, s, 1f);
    public float StarFireRate(int s) => At(starFireRate, s, 1f);
    public float StarReload(int s) => At(starReload, s, 1f);
    public float StarPierceKeep(int s) => At(starPierceKeep, s, 0.4f);
    public int StarPierceCount(int s) => starPierceCount != null && starPierceCount.Length > 0 ? starPierceCount[Mathf.Clamp(s, 0, starPierceCount.Length - 1)] : s;

    // 1..3 with boxStarOdds
    public int RollBoxStars(float r01)
    {
        float total = 0f;
        for (int i = 0; i < 3; i++) total += boxStarOdds != null && i < boxStarOdds.Length ? Mathf.Max(0f, boxStarOdds[i]) : 0f;
        if (total <= 0f) return 1;
        float r = r01 * total;
        for (int i = 0; i < 3; i++)
        {
            r -= boxStarOdds != null && i < boxStarOdds.Length ? Mathf.Max(0f, boxStarOdds[i]) : 0f;
            if (r < 0f) return i + 1;
        }
        return 3;
    }

    [Header("Boss")]
    [Tooltip("Boss health = this x the health of a normal old man of the round he appears in...")]
    public float bossHealthInEnemies = 45f;
    [Tooltip("...but never less than this (rushing the boss early with the pistol must not work).")]
    public float bossMinHealth = 30000f;
    [Header("Prices")]
    public int packAPunchCost = 5000;
    public int shovelCost = 500;         // La Serenisima: the shovel to dig up Giuseppe
    public int gondolaCost = 250;        // La Serenisima: a gondola ride to the next open stop
    [Header("Villajubilada: the traca (buyable trap)")]
    public int tracaCost = 750;
    public float tracaBurnSeconds = 3f;
    public float tracaCooldown = 90f;
    public int tracaPlayerDamage = 25;

    // Conchas for a non-lethal gun hit with a weapon that fires every `fireInterval` seconds.
    public int HitPointsFor(float fireInterval) =>
        fireInterval > 0f && 1f / fireInterval > fastFireRate ? fastHitPoints : hitPoints;
}

// What a points change was for (floating "+10" colour on the HUD, per-run log).
public enum PointSource { Hit, Kill, Headshot, Melee, Ability, Bonus, Spend, Debug }
