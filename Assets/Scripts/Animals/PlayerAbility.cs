using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Active ability of the chosen animal (L1 / E). Cooldown starts when the ability is used.
public class PlayerAbility : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;

    InputAction abilityAction;
    PlayerStats stats;
    PlayerController controller;
    PlayerHealth health;
    AnimalData animal;
    float readyAt;
    float activeUntil;

    public AnimalData Animal => animal;
    public bool IsReady => animal != null && Time.time >= readyAt;
    public bool IsActive => Time.time < activeUntil;
    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
    public float CooldownFraction => animal == null || animal.abilityCooldown <= 0f ? 0f : CooldownRemaining / animal.abilityCooldown;

    void Awake()
    {
        actions = PlayerSlot.ActionsFor(this, actions);   // co-op: this player's own devices
        abilityAction = actions.FindActionMap("Player", true).FindAction("Ability", true);
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
    }

    void OnEnable() => abilityAction.Enable();

    public void Setup(AnimalData data)
    {
        animal = data;
        readyAt = Time.time + 5f;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (animal == null || (gm != null && !gm.IsPlaying) || health.IsDead || health.IsDowned) return;
        if (abilityAction.WasPressedThisFrame() && IsReady) Activate();
    }

    void Activate()
    {
        readyAt = Time.time + animal.abilityCooldown;
        activeUntil = Time.time + animal.abilityDuration;
        GameManager.Instance?.ShowMessage(animal.abilityName + "!");
        switch (animal.ability)
        {
            case AbilityType.RedFury:
                StartCoroutine(TimedMods(new[] { new StatMod(Stat.FireRate, 1.5f), new StatMod(Stat.ReloadSpeed, 2f), new StatMod(Stat.MoveSpeed, animal.furyMoveSpeed) }, animal.abilityDuration));
                break;
            case AbilityType.Armor:
                StartCoroutine(TimedMods(new[] { new StatMod(Stat.DamageTaken, 0.3f), new StatMod(Stat.MoveSpeed, 0.9f) }, animal.abilityDuration));
                break;
            case AbilityType.EagleEye:
                StartCoroutine(TimedMods(new[] { new StatMod(Stat.HeadshotDamage, 1.5f) }, animal.abilityDuration));
                break;
            case AbilityType.BearHug:
                Shockwave(4.5f, 0.6f, 2f, 2.5f);
                break;
            case AbilityType.SuperJump:
                StartCoroutine(SuperJump());
                break;
        }
    }

    IEnumerator TimedMods(StatMod[] mods, float duration)
    {
        stats.Apply(mods);
        yield return new WaitForSeconds(duration);
        stats.Remove(mods);
    }

    IEnumerator SuperJump()
    {
        controller.Launch(4f);
        yield return new WaitForSeconds(0.2f);
        while (!controller.IsGrounded) yield return null;
        Shockwave(3.5f, 0.35f, 2.5f, 0f);
    }

    // Damages (a fraction of a normal enemy's health this round), stuns and pushes enemies around the player.
    void Shockwave(float radius, float healthFraction, float stun, float push)
    {
        var rm = RoundManager.Instance;
        float damage = (rm != null ? rm.HealthForRound(Mathf.Max(1, rm.Round)) : 150f) * healthFraction * stats.Get(Stat.PlayerDamage);
        foreach (var c in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var enemy = c.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead) continue;
            // stunned, pushed, damaged, ability conchas (online client: the host applies it)
            EnemyHits.Apply(enemy, enemy.GetComponent<BossAI>() != null ? damage * 0.25f : damage, false, HitKind.Ability, this, true, stun, push);
        }
    }
}
