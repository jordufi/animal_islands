using UnityEngine;

// La Serenisima: the tall iron fence along the water edge of a barrio that is still closed (you can see you can't
// get in). Its collider is taller than the model on purpose: the Frog's super jump goes 4 m up. The fence goes away
// the moment its zone opens; on clients the zone opens through ZoneDoor.NetOpened, so nothing is sent for it.
public class ZoneFence : MonoBehaviour
{
    [SerializeField] Zone zone;

    public Zone Zone => zone;
    public bool Up => transform.childCount > 0 && transform.GetChild(0).gameObject.activeSelf;

    public void Configure(Zone z) => zone = z;

    void Update()
    {
        if (zone == null || !zone.IsOpen || !Up) return;
        foreach (Transform t in transform) t.gameObject.SetActive(false);
    }
}
