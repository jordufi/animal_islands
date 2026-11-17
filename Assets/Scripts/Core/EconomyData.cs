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
