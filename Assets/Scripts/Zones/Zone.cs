using System.Collections.Generic;
using UnityEngine;

// An area of the map. Enemies only spawn at the spawn points of open zones, and the mystery box
// only appears at the box spots of open zones. Children named "SpawnPoints" / "BoxSpots" hold the points.
public class Zone : MonoBehaviour
{
    public string displayName = "Zona";
    public bool openAtStart;

    public bool IsOpen { get; private set; }

    public IEnumerable<Transform> SpawnPoints => Children("SpawnPoints");
    public IEnumerable<Transform> BoxSpots => Children("BoxSpots");

    void Awake() => IsOpen = openAtStart;

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;   // Villajubilada: the start zones are lost after the first encierro

    IEnumerable<Transform> Children(string group)
    {
        var g = transform.Find(group);
        if (g == null) yield break;
        foreach (Transform t in g) yield return t;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = openAtStart ? Color.green : Color.yellow;
        foreach (var t in SpawnPoints) Gizmos.DrawWireSphere(t.position, 0.5f);
        Gizmos.color = Color.cyan;
        foreach (var t in BoxSpots) Gizmos.DrawWireCube(t.position + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 0.8f));
    }
}
