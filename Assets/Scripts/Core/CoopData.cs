using UnityEngine;

// Co-op numbers (Assets/Data/Coop.asset): going down / reviving like Black Ops Zombies, scaling with the
// number of players, the door opener's card choice.
[CreateAssetMenu(menuName = "Island/Coop Data")]
public class CoopData : ScriptableObject
{
    [Header("Down / revive")]
    [Tooltip("Seconds a downed player lasts before bleeding out (then back next round with the pistol).")]
    public float bleedOutTime = 30f;
    [Tooltip("Seconds holding □ next to a downed teammate.")]
    public float reviveTime = 4f;
    [Tooltip("Revive time when the REVIVER has the Tinto de Verano (Quick Revive).")]
    public float reviveTimeFast = 2f;
    public float reviveRadius = 2f;
    [Tooltip("Fraction of the shared conchas lost when someone goes down.")]
    [Range(0f, 1f)] public float downPenalty = 0.05f;
    [Tooltip("Health after being revived (fraction of max).")]
    [Range(0.1f, 1f)] public float reviveHealth = 0.5f;
    [Tooltip("Spare magazines of the last-stand pistol.")]
    public int lastStandMagazines = 2;

    [Header("Scaling per extra player")]
    [Tooltip("Old men per round x (1 + this x (players - 1)).")]
    public float enemiesPerExtraPlayer = 0.5f;
    [Tooltip("Boss health x (1 + this x (players - 1)).")]
    public float bossHealthPerExtraPlayer = 0.5f;

    [Header("Cards")]
    [Tooltip("The game pauses for everyone while the player who opened the door chooses (full screen). Off = no pause, timer.")]
    public bool pauseForCards = true;
    [Tooltip("Only without the pause (online later): seconds to choose before a random card is taken.")]
    public float cardChoiceTime = 15f;

    [Header("Extraction")]
    [Tooltip("Every standing player must be this close to the boat.")]
    public float extractionGatherRadius = 10f;
}
