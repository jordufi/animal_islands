using UnityEngine;

// Where the conchas come from. All economy numbers live here (Assets/Data/Economy.asset) so they can be
// tuned without code; prices stay on the doors / box / drinks / Pack-a-Punch themselves.
[CreateAssetMenu(menuName = "Island/Economy")]
public class EconomyData : ScriptableObject
{
    public int startingPoints = 500;
    [Tooltip("Per shot that damages an enemy without killing it (once per enemy per shot, not per pellet).")]
    public int hitPoints = 10;
    public int killPoints = 60;
    public int headshotKillPoints = 65;
    public int meleeKillPoints = 130;
    public int abilityKillPoints = 60;
    public int bossKillPoints = 2000;
    [Tooltip("Old men summoned by the boss: do their non-lethal hits pay hit points? (off = the boss fight is not a farm)")]
    public bool bossMinionsPayHits = false;
    [Tooltip("Boat mission: full rounds to survive after calling the boat.")]
    public int missionDefenseRounds = 1;
}

// What a points change was for (floating "+10" colour on the HUD, per-run log).
public enum PointSource { Hit, Kill, Headshot, Melee, Ability, Bonus, Spend, Debug }
