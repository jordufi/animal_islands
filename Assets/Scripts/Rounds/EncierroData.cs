using UnityEngine;

// Numbers of the encierro (Villajubilada): Assets/Data/Encierro.asset.
[CreateAssetMenu(menuName = "Island/Encierro")]
public class EncierroData : ScriptableObject
{
    [Tooltip("An encierro every this many rounds (instead of the night round).")]
    public int every = 5;
    [Tooltip("Whole ring open: laps round it. Ring half open: runs out and back along each open arm.")]
    public int laps = 3;
    public int partialPasses = 2;
    public int bulls = 6;
    [Tooltip("m/s. The player sprints at 5: you can't outrun them, you step out of the street.")]
    public float speed = 9f;
    [Tooltip("m/s on the way up the Calle del Encierro the first time (the run out of the start zones).")]
    public float firstRunSpeed = 6.5f;
    [Tooltip("Seconds between the rocket and the lorry gate opening.")]
    public float warning = 6f;
    public int playerDamage = 60;
    [Tooltip("Seconds a player can't be hit again by the herd.")]
    public float playerImmunity = 1f;
    public float hitRadius = 1.1f;
    [Tooltip("The Porton shuts this long after the last bull went through it (first encierro).")]
    public float gateCloseDelay = 1.5f;
    [Tooltip("Seconds for the sunset to turn into night when the first encierro starts.")]
    public float nightFall = 10f;

    [Header("Old ladies at the windows (first run only)")]
    public float throwRange = 12f;
    public float throwInterval = 2.4f;
    [Tooltip("m/s: slow on purpose, you dodge it.")]
    public float throwSpeed = 6f;
    public int throwDamage = 15;
    public float throwRadius = 1.1f;
}
