using UnityEngine;

// Villajubilada: the Porton del encierro between the start zones and the village. Shut from the start; the first
// encierro opens it and shuts it behind the herd for good. In later encierros the leaves swing open for the bulls
// but the way stays blocked (nobody goes back).
public class EncierroGate : MonoBehaviour
{
    [SerializeField] GameObject blocker;      // collider + carving NavMeshObstacle
    [SerializeField] Transform leafLeft, leafRight;
    [SerializeField] TextMesh worldLabel;

    public bool Blocked => blocker != null && blocker.activeSelf;
    public bool LeavesOpen { get; private set; }
    float swing;

    public void Configure(GameObject wall, Transform left, Transform right, TextMesh label)
    {
        blocker = wall; leafLeft = left; leafRight = right; worldLabel = label;
    }

    public void SetBlocked(bool on) { if (blocker != null) blocker.SetActive(on); }
    public void SetLeaves(bool open) => LeavesOpen = open;

    void Update()
    {
        swing = Mathf.MoveTowards(swing, LeavesOpen ? 1f : 0f, Time.deltaTime * 1.5f);
        if (leafLeft != null) leafLeft.localRotation = Quaternion.Euler(0f, -105f * swing, 0f);
        if (leafRight != null) leafRight.localRotation = Quaternion.Euler(0f, 105f * swing, 0f);
        if (worldLabel == null) return;
        var e = Encierro.Instance;
        worldLabel.text = e == null ? "" : e.ActOne ? (e.Warning || e.Running ? "¡CORRE!" : "PORTÓN DEL ENCIERRO\nse abre en la ronda " + (e.Data != null ? e.Data.every : 5)) : "CERRADO\n(no hay vuelta atrás)";
    }
}
