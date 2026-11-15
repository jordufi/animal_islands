using System;
using UnityEngine;
using UnityEngine.AI;

// Llave Maestra: appears at one random spot of the map at the start of the run. Whoever picks it up
// keeps it in the pocket (hands stay free, unlike the Golden Disc) and can open the door that accepts
// it (the Muelle) for free. If the holder dies (or goes down in co-op) the key drops where they fell.
public class MasterKey : Interactable
{
    public static MasterKey Instance { get; private set; }

    [SerializeField] Transform[] spots;
    [SerializeField] Transform visual;
    [SerializeField] TextMesh worldLabel;

    public PlayerInteractor Holder { get; private set; }
    public bool Used { get; private set; }
    public event Action Changed;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (Net.IsClient) { Show(false); return; }   // online client: the host says where it is (NetApply)
        if (spots != null && spots.Length > 0)
        {
            var s = spots[UnityEngine.Random.Range(0, spots.Length)];
            if (s != null) transform.position = s.position;
        }
        Show(true);
        Sync();
    }

    void Sync()
    {
        if (NetGame.H != null) NetGame.H.KeyChanged(this);
    }

    // Online client: who has it / where it lies.
    public void NetApply(PlayerInteractor holder, Vector3 position, bool used)
    {
        Holder = holder;
        Used = used;
        transform.position = position;
        Show(holder == null && !used);
        if (used) gameObject.SetActive(false);
        Changed?.Invoke();
    }

    void Update()
    {
        if (visual != null && visual.gameObject.activeSelf)
        {
            visual.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            visual.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(Time.time * 2.5f) * 0.12f, 0f);
        }
    }

    public bool IsHeldBy(PlayerInteractor player) => player != null && Holder == player;

    public override string Prompt(PlayerInteractor player) => Holder == null && !Used ? "□  Coger la LLAVE MAESTRA" : "";
    public override bool CanInteract(PlayerInteractor player) => Holder == null && !Used;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || !CanInteract(player)) return;
        Holder = player;
        if (player.Health != null) { player.Health.Died += OnHolderDied; player.Health.Downed += OnHolderDied; }
        Show(false);
        Game.ShowMessage("¡" + (Game.IsCoop && PlayerSlot.Of(player) != null ? PlayerSlot.Of(player).Tag + " tiene" : "Tienes") + " la LLAVE MAESTRA! " + (BoatMission.Instance != null && BoatMission.Instance.HasTasks ? "Abre la puerta del vaporetto" : BoatMission.Instance != null && BoatMission.Instance.HasMission ? "Vale por una pieza que falte al llamar al barco" : "Abre el muelle gratis"));
        Changed?.Invoke();
        Sync();
    }

    // Spends the key on a door. Returns false if this player doesn't have it.
    public bool Use(PlayerInteractor player)
    {
        if (!IsHeldBy(player)) return false;
        if (player.Health != null) { player.Health.Died -= OnHolderDied; player.Health.Downed -= OnHolderDied; }
        Holder = null;
        Used = true;
        Changed?.Invoke();
        Sync();
        gameObject.SetActive(false);
        return true;
    }

    void OnHolderDied()
    {
        if (Holder == null || !Net.Authority) return;
        Vector3 p = Holder.transform.position;
        if (Holder.Health != null) { Holder.Health.Died -= OnHolderDied; Holder.Health.Downed -= OnHolderDied; }
        if (NavAgents.Sample(p, 3f, out var hit)) p = hit;
        transform.position = p;
        Holder = null;
        Show(true);
        if (Game != null) Game.ShowMessage("La LLAVE MAESTRA se ha caído");
        Changed?.Invoke();
        Sync();
    }

    void Show(bool on)
    {
        if (visual != null) visual.gameObject.SetActive(on);
        if (worldLabel != null) worldLabel.gameObject.SetActive(on);
    }
}
