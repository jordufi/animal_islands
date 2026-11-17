using System.Collections;
using UnityEngine;

// Karaoke machine of the chiringuito. With the 3 records, sing (stay close for singTime seconds
// while the old men keep coming) to get the Golden Disc.
public class Karaoke : Interactable
{
    [SerializeField] float singTime = 6f;
    [SerializeField] float stayRadius = 3.5f;
    [SerializeField] Carryable goldenDiscPrefab;
    [SerializeField] Transform rewardPoint;
    [SerializeField] TextMesh worldLabel;

    bool singing;

    public float SingProgress { get; private set; }
    public bool IsSinging => singing;

    void Update()
    {
        if (worldLabel == null) return;
        var egg = EasterEgg.Instance;
        worldLabel.text = egg != null && egg.Completed ? "" : singing ? "♪ ♫ ♪  " + Mathf.RoundToInt(SingProgress * 100) + "%" : "KARAOKE";
    }

    public override string Prompt(PlayerInteractor player)
    {
        var egg = EasterEgg.Instance;
        if (egg == null || egg.Completed || singing) return "";
        if (egg.DiscsFound < egg.DiscsNeeded) return "Karaoke: necesitas los discos de Julio (" + egg.DiscsFound + "/" + egg.DiscsNeeded + ")";
        return "□  Cantar \"Soy un truhán, soy un señor\" (no te alejes)";
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        var egg = EasterEgg.Instance;
        return egg != null && !egg.Completed && !singing && egg.DiscsFound >= egg.DiscsNeeded;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (Net.Authority) StartCoroutine(Sing(player.transform));
    }

    // ---------------------------------------------------------------- online client
    public void NetSinging(bool on, float duration)
    {
        StopAllCoroutines();
        singing = on;
        SingProgress = 0f;
        if (on && EasterEgg.Instance != null) EasterEgg.Instance.SongStarted = true;
        if (on) StartCoroutine(NetProgress(duration));
    }

    System.Collections.IEnumerator NetProgress(float duration)
    {
        for (float t = 0f; t < duration && singing; t += Time.deltaTime)
        {
            SingProgress = t / Mathf.Max(0.1f, duration);
            yield return null;
        }
    }

    // The host's karaoke is done: the Golden Disc appears here too.
    public Carryable NetDone(Vector3 at)
    {
        StopAllCoroutines();
        singing = false;
        SingProgress = 1f;
        if (EasterEgg.Instance != null) { EasterEgg.Instance.Completed = true; EasterEgg.Instance.NotifyChanged(); }
        return Instantiate(goldenDiscPrefab, at, Quaternion.identity);
    }

    IEnumerator Sing(Transform player)
    {
        singing = true;
        bool firstSong = !EasterEgg.Instance.SongStarted;
        EasterEgg.Instance.SongStarted = true;
        Game.ShowMessage(firstSong ? "♪ Cantando... ¡Los abuelos han oído a Julio y se vienen arriba!" : "♪ Cantando... ¡aguanta cerca del karaoke!");
        if (NetGame.H != null) NetGame.H.KaraokeChanged(this, true, singTime);
        for (float t = 0f; t < singTime; t += Time.deltaTime)
        {
            SingProgress = t / singTime;
            if (player == null || Vector3.Distance(player.position, transform.position) > stayRadius)
            {
                Game.ShowMessage("Te has ido a mitad de canción. El público abuchea.");
                singing = false;
                SingProgress = 0f;
                if (NetGame.H != null) NetGame.H.KaraokeChanged(this, false, 0f);
                yield break;
            }
            yield return null;
        }
        singing = false;
        SingProgress = 1f;
        EasterEgg.Instance.Completed = true;
        EasterEgg.Instance.NotifyChanged();
        var p = rewardPoint != null ? rewardPoint.position : transform.position + transform.forward;
        var disc = Instantiate(goldenDiscPrefab, p, Quaternion.identity);
        if (NetGame.H != null) NetGame.H.DiscSpawned(this, disc);
        Game.ShowMessage("¡DISCO DE ORO! Llévalo al " + BoatMission.Vehicle + " para multiplicar tu puntuación");
    }
}
