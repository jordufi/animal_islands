using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Offers 3 random cards (not taken yet) every time a door is bought. The player who opened the door
// chooses (left / right on their stick or D-pad, X / □ / R2 to take it, or a mouse click); only their
// devices count. The blessing and the curse apply to everyone for the rest of the run.
// The game pauses for everyone until the card is chosen (CoopData.pauseForCards). Without the pause (kept for
// online later) the game goes on, the chooser is left alone and has cardChoiceTime seconds, then random.
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [SerializeField] CardData[] pool;
    [SerializeField] int choices = 3;
    [SerializeField] float inputDelay = 0.35f;   // the □ that opened the door must not pick a card

    readonly List<CardData> taken = new List<CardData>();
    readonly Queue<PlayerInteractor> pending = new Queue<PlayerInteractor>();
    InputAction moveAction, jumpAction, useAction, fireAction;
    float shownAt, nextStep;

    public IReadOnlyList<CardData> Taken => taken;
    public CardData[] CurrentOffer { get; private set; }
    public PlayerInteractor Chooser { get; private set; }
    public int Selected { get; private set; }
    public bool PausesGame { get; private set; }
    public float TimeLeft => CurrentOffer == null || PausesGame ? 0f : Mathf.Max(0f, deadline - Time.time);
    float deadline;

    public event Action<CardData[]> OfferShown;
    public event Action<int> SelectionChanged;
    public event Action<CardData> CardChosen;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Offer(PlayerInteractor chooser = null)
    {
        var gm = GameManager.Instance;
        if (chooser == null && gm != null && gm.Player != null) chooser = gm.Player.GetComponent<PlayerInteractor>();
        if (CurrentOffer != null) { pending.Enqueue(chooser); return; }
        var available = pool.Where(c => c != null && !taken.Contains(c)).OrderBy(_ => UnityEngine.Random.value).Take(choices).ToArray();
        if (available.Length == 0) return;
        CurrentOffer = available;
        Chooser = chooser;
        Selected = 0;
        shownAt = Time.unscaledTime;
        // online never pauses (each player only stops on their own screen)
        PausesGame = !Net.Online && (gm == null || gm.PlayerCount <= 1 || gm.Coop == null || gm.Coop.pauseForCards);
        BindChooserInput();
        if (PausesGame) gm.SetState(GameState.ChoosingCard);
        else
        {
            float time = gm.Coop != null ? gm.Coop.cardChoiceTime : 15f;
            deadline = Time.time + time;
            SetChooserBusy(true, time);
            var slot = PlayerSlot.Of(chooser);
            gm.ShowMessage((slot != null ? slot.Tag : "Alguien") + " elige carta para todo el equipo");
        }
        OfferShown?.Invoke(available);
        SelectionChanged?.Invoke(Selected);
        if (NetGame.H != null) NetGame.H.CardOffered(this);
    }

    // ---------------------------------------------------------------- online client
    bool ChooserIsLocal => Chooser != null && !PlayerSlot.IsProxy(Chooser);

    public void NetOffer(CardData[] cards, PlayerInteractor chooser, float time)
    {
        CurrentOffer = cards;
        Chooser = chooser;
        Selected = 0;
        PausesGame = false;
        shownAt = Time.unscaledTime;
        deadline = Time.time + time;
        if (ChooserIsLocal) BindChooserInput();
        else moveAction = jumpAction = useAction = fireAction = null;
        OfferShown?.Invoke(cards);
        SelectionChanged?.Invoke(Selected);
    }

    public void NetSelect(int index)
    {
        if (CurrentOffer == null) return;
        Selected = Mathf.Clamp(index, 0, CurrentOffer.Length - 1);
        SelectionChanged?.Invoke(Selected);
    }

    public void NetChosen(CardData card)
    {
        if (card == null) return;
        if (!taken.Contains(card))
        {
            taken.Add(card);
            RunModifiers.Apply(card.blessing);
            RunModifiers.Apply(card.curse);
            var gm = GameManager.Instance;
            if (gm != null) gm.CardsTaken++;
        }
        CurrentOffer = null;
        Chooser = null;
        CardChosen?.Invoke(card);
    }

    // Online: the host tells the chooser's machine to freeze them while they choose (and to let go).
    public void NetBusy(PlayerInteractor chooser, bool busy, float seconds)
    {
        var keep = Chooser;
        Chooser = chooser;
        SetChooserBusy(busy, seconds);
        Chooser = keep;
    }

    System.Collections.Generic.IEnumerable<InputDevice> ChooserDevices()
    {
        var asset = moveAction != null ? moveAction.actionMap.asset : null;
        if (asset != null && asset.devices.HasValue) return asset.devices.Value;   // co-op: this player's devices
        var all = new List<InputDevice>(Gamepad.all);                              // solo: every device
        if (Keyboard.current != null) all.Add(Keyboard.current);
        return all;
    }

    void BindChooserInput()
    {
        var gm = GameManager.Instance;
        var asset = Chooser != null ? PlayerSlot.ActionsFor(Chooser, gm != null ? gm.Actions : null) : (gm != null ? gm.Actions : null);
        if (asset == null) { moveAction = jumpAction = useAction = fireAction = null; return; }
        var map = asset.FindActionMap("Player", true);
        moveAction = map.FindAction("Move", true);
        jumpAction = map.FindAction("Jump", true);
        useAction = map.FindAction("Use", true);
        fireAction = map.FindAction("Fire", true);
        foreach (var a in new[] { moveAction, jumpAction, useAction, fireAction }) a.Enable();
    }

    // co-op: the chooser stands still and the old men leave them alone while the cards are up
    void SetChooserBusy(bool busy, float seconds = 0f)
    {
        if (Chooser == null) return;
        if (NetGame.H != null && PlayerSlot.IsProxy(Chooser)) NetGame.H.Forward(Chooser, busy ? NetGame.Fwd.CardBusy : NetGame.Fwd.CardFree, "", seconds);
        var controller = Chooser.GetComponent<PlayerController>();
        var shooter = Chooser.GetComponent<PlayerShooter>();
        var health = Chooser.Health;
        bool usable = health == null || (!health.IsDead && !health.IsDowned);
        if (controller != null && usable) controller.ControlsEnabled = !busy;
        if (shooter != null && usable) shooter.enabled = !busy;
        if (health != null)
        {
            if (busy) health.SetIgnored(seconds + 0.5f);
            else { health.ClearIgnored(); health.SetIgnored(1f); }
        }
    }

    void Update()
    {
        if (CurrentOffer == null) return;
        if (!PausesGame && Time.time >= deadline && Net.Authority) { Choose(CurrentOffer[UnityEngine.Random.Range(0, CurrentOffer.Length)]); return; }
        if (moveAction == null || Time.unscaledTime - shownAt < inputDelay) return;

        // D-pad / arrow keys of the chooser's own devices (the Move action is only the stick / WASD)
        int step = 0;
        foreach (var d in ChooserDevices())
        {
            if (d is Gamepad pad) step += pad.dpad.right.wasPressedThisFrame ? 1 : pad.dpad.left.wasPressedThisFrame ? -1 : 0;
            else if (d is Keyboard kb) step += kb.rightArrowKey.wasPressedThisFrame ? 1 : kb.leftArrowKey.wasPressedThisFrame ? -1 : 0;
        }
        if (step != 0) Select(Selected + (step > 0 ? 1 : -1));

        float x = moveAction.ReadValue<Vector2>().x;
        if (Mathf.Abs(x) < 0.3f) nextStep = 0f;
        else if (Time.unscaledTime >= nextStep)
        {
            Select(Selected + (x > 0f ? 1 : -1));
            nextStep = Time.unscaledTime + 0.3f;
        }
        if (jumpAction.WasPressedThisFrame() || useAction.WasPressedThisFrame() || fireAction.WasPressedThisFrame())
            Choose(CurrentOffer[Selected]);
    }

    public void Select(int index)
    {
        if (CurrentOffer == null) return;
        int i = Mathf.Clamp(index, 0, CurrentOffer.Length - 1);
        if (i == Selected) return;
        Selected = i;
        SelectionChanged?.Invoke(i);
        if (NetGame.H != null) NetGame.H.CardSelected(i);
        else if (NetGame.C != null && ChooserIsLocal) NetGame.C.SendCardSelect(i);
    }

    public void Choose(CardData card)
    {
        if (CurrentOffer == null || !CurrentOffer.Contains(card)) return;
        if (Net.IsClient)
        {
            // online client: only the chooser picks, and the host applies it for everyone
            if (ChooserIsLocal && NetGame.C != null) NetGame.C.SendCardPick(System.Array.IndexOf(CurrentOffer, card));
            return;
        }
        taken.Add(card);
        RunModifiers.Apply(card.blessing);
        RunModifiers.Apply(card.curse);
        CurrentOffer = null;
        var gm = GameManager.Instance;
        gm.CardsTaken++;
        if (PausesGame) gm.SetState(GameState.Playing);
        else SetChooserBusy(false);
        var slot = PlayerSlot.Of(Chooser);
        gm.ShowMessage((gm.IsCoop && slot != null ? slot.Tag + " eligió " : "") + card.title + ": " + card.blessingText + " / " + card.curseText);
        Chooser = null;
        CardChosen?.Invoke(card);
        if (NetGame.H != null) NetGame.H.CardChosen(card);
        if (pending.Count > 0) Offer(pending.Dequeue());
    }
}
