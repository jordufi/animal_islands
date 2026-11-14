using UnityEngine;

// Definition of a weapon. All balance numbers live here so they can be tuned without code.
[CreateAssetMenu(menuName = "Island/Weapon")]
public class WeaponData : ScriptableObject
{
    public string id = "pistol";
    public string displayName = "Pistola";
    public string upgradedName = "Pistola Deluxe";
    public GameObject model;

    [Header("Damage")]
    public float damage = 50f;
    public float headshotMultiplier = 3f;
    public int pellets = 1;
    [Tooltip("Enemies a single bullet can go through.")]
    public int penetration = 1;
    public float range = 80f;
    [Tooltip("Damage drops linearly to minDamageFactor between this distance and range.")]
    public float falloffStart = 60f;
    public float minDamageFactor = 0.5f;

    [Header("Handling")]
    public float fireInterval = 0.2f;
    public bool automatic;
    public float hipSpread = 1.5f;      // degrees
    public float aimSpread = 0.3f;
    public float aimFieldOfView = 45f;
    public int magazineSize = 8;
    public int maxReserve = 80;
    public float reloadTime = 1.4f;
    [Tooltip("Shotgun style: shells go in one at a time (reloadTime is ignored); shooting stops the reload.")]
    public bool reloadOneByOne;
    public float reloadStartTime = 0.4f;    // before the first shell
    public float shellLoadTime = 0.6f;      // per shell

    [Header("Mystery box")]
    public float boxWeight = 1f;

    [Header("Wall buy (WallWeapon)")]
    public int wallCost = 500;
    public int wallAmmoCost = 250;   // x2 once Pack-a-Punched

    [Header("Pack-a-Punch")]
    public float upgradeDamageMultiplier = 2f;
    public float upgradeMagazineMultiplier = 1.5f;

    [Header("Feel")]
    [Tooltip("Camera kick per shot in degrees (-1 = filled in by the map setup).")]
    public float recoil = -1f;

    [Header("Look")]
    public Color tracerColor = new Color(1f, 0.85f, 0.3f);
}
