using UnityEngine;

// La Serenisima: Don Anselmo comes as the Dux, with a golden carnival mask. The mask is the top share of his health
// bar (EnemyHealth.SetMask): every hit takes one segment and none is a headshot until it breaks and flies off.
// Lives in the scene; works the same on the host and on the clients' puppet (the mask is derived from the health).
public class DuxMask : MonoBehaviour
{
    [SerializeField] GameObject maskPrefab;
    [SerializeField] Material gold;
    [SerializeField] int hits = 10;
    [SerializeField] float share = 0.3f;

    BossAI masked;
    GameObject mask;

    public bool Wearing => mask != null;

    public void Configure(GameObject prefab, Material goldMaterial)
    {
        maskPrefab = prefab; gold = goldMaterial;
    }

    void Update()
    {
        var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;
        if (boss == null || boss == masked || boss.Health == null || boss.Health.IsDead) return;
        masked = boss;
        boss.Health.SetMask(hits, share);
        boss.Health.MaskBroken += OnBroken;
        Transform head = null;
        foreach (var t in boss.GetComponentsInChildren<Transform>()) if (t.name == "Head") { head = t; break; }
        if (maskPrefab == null || head == null) return;
        float k = boss.transform.lossyScale.y;
        mask = Instantiate(maskPrefab, head.position + boss.transform.forward * 0.3f * k + Vector3.up * 0.26f * k, boss.transform.rotation, head);
        mask.transform.localScale *= 2.1f;
        foreach (var c in mask.GetComponentsInChildren<Collider>()) Destroy(c);
        if (gold != null)
            foreach (var r in mask.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) if (ms[i] != null && (ms[i].name.StartsWith("VN_White") || ms[i].name.StartsWith("VN_Gold"))) ms[i] = gold;
                r.sharedMaterials = ms;
            }
        if (Net.Authority && GameManager.Instance != null) GameManager.Instance.ShowMessage("¡EL DUX ANSELMO! Rompedle la máscara dorada: hasta entonces no hay tiros a la cabeza");
    }

    void OnBroken(EnemyHealth h)
    {
        h.MaskBroken -= OnBroken;
        if (Net.Authority && GameManager.Instance != null) GameManager.Instance.ShowMessage("¡La máscara del Dux se ha roto!");
        if (mask == null) return;
        var m = mask;
        mask = null;
        MaskLook.Shatter(m, (masked != null ? masked.transform.forward : Vector3.forward) * 3f);
    }
}
