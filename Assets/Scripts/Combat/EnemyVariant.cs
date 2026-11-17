using UnityEngine;

// A flavour of the old man enemy: same model, different stats and clothing colour.
[CreateAssetMenu(menuName = "Island/Enemy Variant")]
public class EnemyVariant : ScriptableObject
{
    public string displayName = "Jubilado";
    public float healthMultiplier = 1f;
    public float moveSpeed = 2f;
    public int damage = 25;
    public float scale = 1f;
    public float pointsMultiplier = 1f;
    [Tooltip("Tint for shirt / vest / shorts. Alpha 0 keeps the original colours.")]
    public Color clothingColor = new Color(0, 0, 0, 0);
    [Tooltip("First round this variant can appear in.")]
    public int firstRound = 1;
    [Tooltip("Spawn chance at firstRound, grows by chancePerRound up to maxChance.")]
    public float baseChance = 0f;
    public float chancePerRound = 0f;
    public float maxChance = 0f;
    [Tooltip("Most of this variant alive at the same time (0 = only the round's global limit). Night rounds use it.")]
    public int maxAliveAtOnce = 0;

    [Header("Abuelo senderista (night rounds)")]
    [Tooltip("Walks over props (cars, tents, crates...) instead of around them: uses the 'Saltador' NavMesh.")]
    public bool jumper;
    public bool throwsChanclas;
    public float throwMinRange = 4f;
    public float throwMaxRange = 14f;
    public float throwCooldown = 3.5f;
    public int throwDamage = 15;
    public float throwSpeed = 13f;

    [Header("Carnival mask (La Serenisima)")]
    [Tooltip("Hits that break the mask (0 = no mask). While he wears it every hit only chips the mask and there are no headshots.")]
    public int maskHits = 0;
    [Tooltip("Part of his health that is the mask.")]
    public float maskShare = 0.3f;
    public GameObject maskPrefab;
    [Tooltip("Plague doctor: a cloud that hurts stays where he dies (0 = none).")]
    public float deathCloudRadius = 0f;
    public float deathCloudSeconds = 5f;
    public float deathCloudDps = 12f;

    [Header("Villajubilada")]
    [Tooltip("El del bombo: old men within this radius walk faster while he lives (0 = no aura).")]
    public float auraRadius = 0f;
    public float auraSpeedFactor = 1.3f;
    public GameObject wornPrefab;        // the drum, hung on the belly
    [Tooltip("La de los petardos: what she throws leaves a burning patch where it lands (0 = a plain chancla).")]
    public float throwCloudRadius = 0f;
    public float throwCloudSeconds = 3f;
    public float throwCloudDps = 15f;

    [Header("Hurt (user feedback 1)")]
    [Tooltip("Below this share of his health he loses his glasses (walks slower) or drops his cane (runs faster), 50/50. 0 = never.")]
    public float hurtShare = 0.25f;
    public float glassesSpeedFactor = 0.6f;
    public float caneSpeedFactor = 1.4f;

    public float ChanceAt(int round) => round < firstRound ? 0f : Mathf.Min(maxChance, baseChance + chancePerRound * (round - firstRound));
}
