using UnityEngine;

// Villajubilada: Don Anselmo comes as the pregonero of the fiestas: a red sash across the chest and a red
// neckerchief. Only a look (no rule changes), so it is the same on the host and on the clients' puppet.
public class PregoneroLook : MonoBehaviour
{
    [SerializeField] Material red;

    BossAI dressed;
    public bool Wearing { get; private set; }

    public void Configure(Material cloth) => red = cloth;

    void Update()
    {
        var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;
        if (boss == null || boss == dressed) return;
        dressed = boss;
        Transform spine = null, head = null;
        foreach (var t in boss.GetComponentsInChildren<Transform>())
        {
            if (t.name == "Spine" && spine == null) spine = t;
            if (t.name == "Head" && head == null) head = t;
        }
        if (spine == null) spine = boss.transform;
        float k = boss.transform.lossyScale.y;
        Part(spine, spine.position + Vector3.up * 0.16f * k, boss.transform.rotation * Quaternion.Euler(0f, 0f, 40f), new Vector3(0.62f, 0.09f, 0.44f) * k);   // the sash
        if (head != null) Part(head, head.position - Vector3.up * 0.12f * k, boss.transform.rotation, new Vector3(0.5f, 0.14f, 0.5f) * k);                  // the neckerchief
        Wearing = true;
        if (Net.Authority && GameManager.Instance != null) GameManager.Instance.ShowMessage("¡DON ANSELMO, EL PREGONERO! Viene a dar el pregón... a bastonazos");
    }

    void Part(Transform bone, Vector3 pos, Quaternion rot, Vector3 size)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Pregonero_Tela";
        Destroy(g.GetComponent<Collider>());
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = size;
        g.transform.SetParent(bone, true);
        if (red != null) g.GetComponent<Renderer>().sharedMaterial = red;
    }
}
