using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Keeps track of which zones are open.
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    public IReadOnlyList<Zone> Zones => zones;
    public event Action<Zone> ZoneOpened;

    List<Zone> zones = new List<Zone>();

    void Awake()
    {
        Instance = this;
        zones = FindObjectsByType<Zone>().ToList();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public IEnumerable<Transform> OpenSpawnPoints() => zones.Where(z => z.IsOpen).SelectMany(z => z.SpawnPoints);
    public IEnumerable<Transform> OpenBoxSpots() => zones.Where(z => z.IsOpen).SelectMany(z => z.BoxSpots);
    public int OpenCount => zones.Count(z => z.IsOpen);

    // Returns true if the zone was closed and is now open.
    public bool Open(Zone zone)
    {
        if (zone == null || zone.IsOpen) return false;
        zone.Open();
        ZoneOpened?.Invoke(zone);
        return true;
    }
}
