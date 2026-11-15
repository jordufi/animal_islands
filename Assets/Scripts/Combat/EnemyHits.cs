using UnityEngine;

public enum HitKind : byte { Gun, Melee, Ability }

// Every player hit on an old man goes through here: damage, stun / knockback, conchas and the kill count.
// Online, a client only predicts the hit (health bar, crosshair) and sends it to the host, who applies it
// to the real old man and pays the shared pot.
public static class EnemyHits
{
    // payHit: this hit earns the "hit" conchas if it doesn't kill (the shotgun pays once per shot).
    public static DamageInfo Apply(EnemyHealth enemy, float amount, bool head, HitKind kind, Component by, bool payHit,
                                   float stun = 0f, float knockback = 0f)
    {
        if (enemy == null) return default;
        if (Net.IsClient)
        {
            if (NetGame.Instance != null) NetGame.Instance.SendHit(enemy, amount, head, kind, payHit, stun, knockback);
            return enemy.Predict(amount, head);
        }
        return ApplyLocal(enemy, amount, head, kind, by, payHit, stun, knockback, by != null ? by.transform.position : enemy.transform.position);
    }

    // The real hit (offline / host). `from` = where the attacker stands (knockback direction).
    public static DamageInfo ApplyLocal(EnemyHealth enemy, float amount, bool head, HitKind kind, Component by, bool payHit,
                                        float stun, float knockback, Vector3 from)
    {
        if (enemy == null || enemy.IsDead) return default;   // e.g. a client's hit arriving after someone else killed him
        var ai = enemy.GetComponent<EnemyAI>();
        if (ai != null && kind == HitKind.Ability)  // shockwave: stunned and pushed even if it kills
        {
            if (stun > 0f) ai.Stun(stun);
            if (knockback > 0f) ai.Knockback(from, knockback);
        }
        // Siesta fulminante (insta-kill power-up): anything but the boss dies from one hit
        if (PowerUpManager.InstaKill && enemy.GetComponent<BossAI>() == null) amount = Mathf.Max(amount, enemy.Current + 1f);
        var info = enemy.TakeDamage(amount, head);
        if (ai != null && kind == HitKind.Melee && !info.killed)
        {
            if (stun > 0f) ai.Stun(stun);
            if (knockback > 0f) ai.Knockback(from, knockback);
        }
        Award(enemy, info, head, kind, by, payHit);
        return info;
    }

    static void Award(EnemyHealth enemy, DamageInfo info, bool head, HitKind kind, Component by, bool payHit)
    {
        var gm = GameManager.Instance;
        if (gm == null || info.amount <= 0f) return;
        var eco = gm.Economy;
        if (info.killed)
        {
            int pts;
            PointSource source;
            switch (kind)
            {
                case HitKind.Melee: pts = eco != null ? eco.meleeKillPoints : 130; source = PointSource.Melee; head = false; break;
                case HitKind.Ability: pts = eco != null ? eco.abilityKillPoints : 60; source = PointSource.Ability; head = false; break;
                default:
                    pts = head ? (eco != null ? eco.headshotKillPoints : 65) : (eco != null ? eco.killPoints : 60);
                    source = head ? PointSource.Headshot : PointSource.Kill;
                    break;
            }
            gm.AddPoints(Mathf.RoundToInt(pts * enemy.PointsMultiplier), source);
            gm.RegisterKill(head, by);
        }
        else if (payHit && enemy.PaysHits) gm.AddPoints(eco != null ? eco.hitPoints : 10, PointSource.Hit);
    }
}
