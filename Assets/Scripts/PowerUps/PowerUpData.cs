using UnityEngine;

// Numbers of the power-ups old men drop (Assets/Data/PowerUps.asset).
[CreateAssetMenu(menuName = "Island/Power-ups")]
public class PowerUpData : ScriptableObject
{
    [Header("Drops")]
    [Tooltip("Chance per kill of a Nevera de munición (x the AmmoDropChance stat of cards).")]
    public float ammoDropChance = 0.025f;
    [Tooltip("Chance per kill of one of the other power-ups (picked by weight below).")]
    public float otherDropChance = 0.025f;
    public float instaKillWeight = 1f;
    public float doublePointsWeight = 1f;
    public float nukeWeight = 0.6f;
    [Tooltip("Drops per round from kills (the dawn cooler doesn't count).")]
    public int maxPerRound = 4;
    public int maxActive = 3;
    public float lifetime = 25f;

    [Header("Effects")]
    public float instaKillSeconds = 30f;
    public float doublePointsSeconds = 30f;
    public int nukePoints = 400;
}
