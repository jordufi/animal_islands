using UnityEngine;

// The ukulele every animal carries on its back (R3 / V / middle mouse). Balance numbers for the melee hit.
[CreateAssetMenu(menuName = "Island/Melee")]
public class MeleeData : ScriptableObject
{
    public string displayName = "El Ukelele";
    [Tooltip("350 kills in one hit up to round 3 (150 / 250 / 350 HP at Normal).")]
    public float damage = 350f;
    public float range = 1.9f;            // hit reach from the player
    public float arc = 70f;               // half-angle in front of the player that the swing covers
    public float lungeRange = 3.5f;       // lunge at an old man this close in front of the camera
    public float lungeSpeed = 10f;
    public float lungeMaxTime = 0.22f;
    public float hitDelay = 0.16f;        // from button to impact
    public float swingTime = 0.42f;       // whole swing (gun hidden meanwhile)
    public float cooldown = 0.6f;         // between swings
    public float knockback = 1.2f;
    public float stun = 0.35f;
    public float bossDamageFactor = 0.25f;
}
