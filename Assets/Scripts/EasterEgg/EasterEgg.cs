using System;
using System.Collections;
using UnityEngine;

// Julio Iglesias easter egg: find the 3 (fake) records around the island, sing at the karaoke
// of the chiringuito and get the Golden Disc. Carry it to the boat for a score multiplier.
public class EasterEgg : MonoBehaviour
{
    public static EasterEgg Instance { get; private set; }

    [SerializeField] int discsNeeded = 3;

    public int DiscsFound { get; private set; }
    public int DiscsNeeded => discsNeeded;
    public bool Completed { get; set; }
    // The records went into the karaoke (a song started): the old men get fired up for the rest of the run (feedback 1).
    public bool SongStarted { get; set; }
    public static bool Motivated => Instance != null && (Instance.SongStarted || Instance.Completed);
    public event Action Changed;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void AddDisc()
    {
        DiscsFound++;
        Changed?.Invoke();
        GameManager.Instance?.ShowMessage(DiscsFound >= discsNeeded
            ? "¡Tienes los " + discsNeeded + " discos! Al karaoke del chiringuito..."
            : "Disco de Julio Iglesias (falso) " + DiscsFound + "/" + discsNeeded);
    }

    public void NotifyChanged() => Changed?.Invoke();

    // Online client: the host's count.
    public void NetSetDiscs(int found)
    {
        DiscsFound = found;
        Changed?.Invoke();
    }
}
