using UnityEngine;

public enum AbilityType
{
    RedFury,      // Panda Rojo: faster fire + reload for a few seconds
    BearHug,      // Panda: shockwave that damages and stuns nearby enemies
    SuperJump,    // Rana: huge jump, stun on landing
    Armor,        // Cocodrilo: takes much less damage for a few seconds
    EagleEye,     // Tucán: see enemies through walls + bonus headshot damage
}

// A playable animal: model prefab, passive bonus and active ability (L1).
[CreateAssetMenu(menuName = "Island/Animal")]
public class AnimalData : ScriptableObject
{
    public string id = "RedPanda";
    public string displayName = "Panda Rojo";
    [TextArea] public string tagline;
    public GameObject playerPrefab;
    public Color uiColor = Color.white;

    [Header("Passive")]
    public string passiveName;
    [TextArea] public string passiveDescription;
    public StatMod[] passiveMods;

    [Header("Active ability (L1)")]
    public AbilityType ability;
    public string abilityName;
    [TextArea] public string abilityDescription;
    public float abilityCooldown = 40f;
    public float abilityDuration = 6f;
    public float furyMoveSpeed = 1.25f;   // Furia Roja: move speed while it lasts (feedback 2026-11-17)
}
