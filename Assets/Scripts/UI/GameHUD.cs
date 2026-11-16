using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// In-game HUD, built from code. Shared part (this): round, the shared conchas + floating "+10" / "-750"
// next to the counter, toasts, boss bar, easter egg progress, card choice, pause and end-of-run screens,
// and in 3-player split-screen the island map in the empty quarter. Each player's own part (health,
// weapon, prompt, drinks, downed / revive...) is a PlayerHUD inside their camera's view.
public class GameHUD : MonoBehaviour
{
    static readonly Color RoundColor = new Color(0.85f, 0.12f, 0.1f);

    GameManager gm;
    PlayerHealth player;   // J1
    readonly List<PlayerHUD> playerHuds = new List<PlayerHUD>();

    Transform root;
    Text objectiveText, buffText;
    Text roundText, remainingText, pointsText, eggText, bossName, cardTimer;
    RectTransform bossFill, toastRoot;
    GameObject bossBar, cardPanel, pausePanel, endPanel, settingsPanel, spareMap;
    readonly List<(RectTransform rt, Image frame)> cardViews = new List<(RectTransform, Image)>();
    readonly List<RectTransform> spareDots = new List<RectTransform>();
    RectTransform spareMapRect;
    readonly Queue<string> toasts = new Queue<string>();
    Text toastText;
    Image toastBack;
    bool toastSplit;
    float toastUntil;
    int lastRound = -1;

    public IReadOnlyList<PlayerHUD> PlayerHuds => playerHuds;

    class Popup { public RectTransform rt; public Text text; public float born; public int amount; public PointSource source; public Vector2 velocity; }
    readonly List<Popup> popups = new List<Popup>();
    const float PopupLife = 1.1f;

    // La Serenisima is a city, not an island
    static string MapTitle => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Venecia" ? "MAPA DE LA SERENÍSIMA" : "MAPA DE LA ISLA";

    void Start()
    {
        gm = GameManager.Instance;
        UIKit.EnsureEventSystem();
        Build();
        gm.PointsChanged += p => pointsText.text = p + " conchas";
        gm.PointsDelta += ShowPointsPopup;
        gm.Message += Toast;
        gm.StateChanged += OnStateChanged;
        foreach (var p in gm.Players) Bind(p);
        gm.PlayerSpawned += Bind;
        var cm = CardManager.Instance;
        if (cm != null)
        {
            cm.OfferShown += ShowCards;
            cm.SelectionChanged += HighlightCard;
            cm.CardChosen += _ => CloseCards();
        }
        if (BossEncounter.Instance != null) BossEncounter.Instance.BossSpawned += b => bossBar.SetActive(true);
        if (RoundManager.Instance != null) RoundManager.Instance.RoundStarted += r => Toast("RONDA " + r);
        pointsText.text = gm.Points + " conchas";
    }

    void Bind(PlayerHealth p)
    {
        if (p == null || playerHuds.Exists(h => h.Player == p)) return;
        if (PlayerSlot.IsProxy(p)) return;   // online: only this machine's player has a HUD here
        if (player == null) player = p;
        int n = Net.Online ? 1 : Mathf.Max(gm.ExpectedPlayerCount, gm.PlayerCount);
        playerHuds.Add(PlayerHUD.Create(p, n, gm.ExpectedPlayerCount > 1));
        if (n == 3 && spareMap == null) BuildSpareMap();
        if (n > 1 && !toastSplit) LayoutToast();
    }

    // Split-screen: toasts and the round in the middle of the screen on dark strips, so they don't cover
    // anyone's HUD.
    void LayoutToast()
    {
        toastSplit = true;
        // the round goes into the same strip (shown whenever there is no toast)
        roundText.gameObject.SetActive(false);
        remainingText.gameObject.SetActive(false);
        objectiveText.gameObject.SetActive(false);
        toastRoot.anchorMin = toastRoot.anchorMax = toastRoot.pivot = new Vector2(0.5f, 0.5f);
        toastRoot.anchoredPosition = Vector2.zero;
        toastRoot.sizeDelta = new Vector2(1500, 56);
        toastText.fontSize = 30;
    }

    // ---------------------------------------------------------------- build
    void Build()
    {
        root = UIKit.CreateCanvas("HUD", 10).transform;

        // top-left: round
        roundText = UIKit.Label("Round", root, new Vector2(0, 1), new Vector2(40, -20), new Vector2(400, 110), "", 96, TextAnchor.UpperLeft, RoundColor);
        remainingText = UIKit.Label("Remaining", root, new Vector2(0, 1), new Vector2(44, -125), new Vector2(500, 36), "", 24, TextAnchor.UpperLeft);

        // what to do next (boat mission) under the round; active power-ups under the conchas
        objectiveText = UIKit.Label("Objective", root, new Vector2(0, 1), new Vector2(44, -200), new Vector2(900, 34), "", 24, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.5f));
        buffText = UIKit.Label("Buffs", root, new Vector2(1, 1), new Vector2(-40, -138), new Vector2(900, 36), "", 26, TextAnchor.UpperRight, new Color(1f, 0.45f, 0.35f));

        // top-right: shared conchas, easter egg
        pointsText = UIKit.Label("Points", root, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(600, 70), "", 52, TextAnchor.UpperRight, UIKit.Accent);
        eggText = UIKit.Label("Egg", root, new Vector2(1, 1), new Vector2(-40, -100), new Vector2(600, 36), "", 26, TextAnchor.UpperRight, new Color(1f, 0.85f, 0.4f));

        // toasts (split-screen: moved to the middle of the screen on a dark strip by LayoutToast)
        toastRoot = UIKit.Anchored("Toast", root, new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(1400, 60));
        toastBack = UIKit.Image(toastRoot, new Color(0f, 0f, 0f, 0f));
        toastText = UIKit.Text(UIKit.Stretch("Text", toastRoot), "", 34, TextAnchor.MiddleCenter, Color.white);

        // boss bar
        var bb = UIKit.Anchored("BossBar", root, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(900, 34));
        UIKit.Image(bb, new Color(0, 0, 0, 0.7f));
        bossFill = UIKit.Rect("Fill", bb, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        UIKit.Image(bossFill, new Color(0.75f, 0.1f, 0.6f));
        bossName = UIKit.Label("Name", bb, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(900, 36), "DON ANSELMO, PRESIDENTE DE LA COMUNIDAD", 28);
        bossBar = bb.gameObject;
        bossBar.SetActive(false);
    }

    // ---------------------------------------------------------------- refresh
    void Update()
    {
        if (gm == null) return;
        var rm = RoundManager.Instance;
        if (rm != null)
        {
            if (rm.Round != lastRound) { lastRound = rm.Round; roundText.text = rm.Round > 0 ? rm.Round.ToString() : ""; }
            remainingText.text = rm.WaitingForPlayers ? "Esperando a que carguen los demás jugadores..." : rm.SpawningPaused ? "¡JEFE FINAL!" : rm.InIntermission ? (rm.Round == 0 ? "Prepárate..." : rm.NextRoundNight ? "Se está haciendo de noche..." : "Siguiente ronda en camino...")
                : rm.IsNight ? "NOCHE · Senderistas restantes: " + rm.RemainingInRound : "Abuelos restantes: " + rm.RemainingInRound;
            remainingText.color = rm.IsNight ? new Color(0.55f, 1f, 0.55f) : Color.white;
        }

        var mission = BoatMission.Instance;
        string objective = mission != null ? mission.Objective() : "";
        objectiveText.text = objective.Length > 0 ? "Objetivo: " + objective : "";
        // task-list maps (La Serenisima), one screen: the whole list with its checks, top left under the health
        if (mission != null && mission.HasTasks && !toastSplit && !(GameManager.Instance != null && GameManager.Instance.BossDefeated))
        {
            objectiveText.supportRichText = true;
            objectiveText.verticalOverflow = VerticalWrapMode.Overflow;
            objectiveText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sb = new System.Text.StringBuilder("<b>PARA QUE VENGA EL VAPORETTO</b>");
            foreach (var (text, done) in mission.TaskList())
                sb.Append(done ? "\n<color=#7CFC7C>[X] " + text + "</color>" : "\n[  ] " + text);
            if (mission.Called || mission.Complete) sb.Append("\n<color=#FFE680>> " + objective + "</color>");
            objectiveText.text = sb.ToString();
        }
        buffText.text = PowerUpManager.Instance != null ? PowerUpManager.Instance.Status() : "";

        var egg = EasterEgg.Instance;
        eggText.text = egg != null && egg.DiscsFound > 0 && !egg.Completed ? "Discos de Julio: " + egg.DiscsFound + "/" + egg.DiscsNeeded : "";

        var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;
        if (boss != null && bossBar.activeSelf)
        {
            bossFill.anchorMax = new Vector2(boss.Health.Current / boss.Health.MaxHealth, 1f);
            if (boss.Health.IsDead) bossBar.SetActive(false);
        }

        UpdateCards();
        UpdateSpareMap();
        UpdatePopups();

        if (Time.unscaledTime >= toastUntil)
        {
            if (toasts.Count > 0) { toastText.text = toasts.Dequeue(); toastText.color = Color.white; toastUntil = Time.unscaledTime + 2.8f; }
            else if (toastSplit)
            {
                var rm2 = RoundManager.Instance;
                toastText.text = (rm2 != null && rm2.Round > 0 ? "RONDA " + rm2.Round + "  ·  " : "") + remainingText.text + (objectiveText.text.Length > 0 ? "  ·  " + objectiveText.text : "");
                toastText.color = remainingText.color;
            }
            else toastText.text = "";
            if (toastBack != null) toastBack.color = new Color(0f, 0f, 0f, toastSplit && toastText.text.Length > 0 ? 0.65f : 0f);
        }
    }

    // ---------------------------------------------------------------- floating points
    static Color PopupColor(PointSource s)
    {
        switch (s)
        {
            case PointSource.Hit: return Color.white;
            case PointSource.Headshot: return new Color(1f, 0.55f, 0.15f);
            case PointSource.Spend: return new Color(1f, 0.3f, 0.25f);
            case PointSource.Bonus: return new Color(0.5f, 1f, 0.55f);
            case PointSource.Debug: return new Color(0.4f, 0.9f, 1f);
            default: return UIKit.Accent;   // kills, melee, ability
        }
    }

    void ShowPointsPopup(int amount, PointSource source)
    {
        // fast streams (SMG, shotgun) add up into the last number instead of flooding the screen
        var last = popups.Count > 0 ? popups[popups.Count - 1] : null;
        if (last != null && Time.unscaledTime - last.born < 0.3f && (last.amount > 0) == (amount > 0) && (last.source == PointSource.Hit) == (source == PointSource.Hit))
        {
            last.amount += amount;
            if (source != PointSource.Hit && source != last.source && (source == PointSource.Headshot || source == PointSource.Melee)) last.source = source;
            last.born = Time.unscaledTime - 0.05f;
            last.text.text = (last.amount > 0 ? "+" : "") + last.amount;
            last.text.color = PopupColor(last.source);
            return;
        }
        float x = -40f - pointsText.preferredWidth - 16f;
        bool big = source == PointSource.Headshot || source == PointSource.Melee || Mathf.Abs(amount) >= 500;
        var rt = UIKit.Anchored("PointsPopup", root, new Vector2(1, 1), new Vector2(x - 110f, -60f + Random.Range(-14f, 14f)), new Vector2(220, 56));
        var text = UIKit.Text(rt, (amount > 0 ? "+" : "") + amount, big ? 46 : 36, TextAnchor.MiddleRight, PopupColor(source));
        popups.Add(new Popup
        {
            rt = rt, text = text, born = Time.unscaledTime, amount = amount, source = source,
            velocity = amount > 0 ? new Vector2(Random.Range(-90f, -50f), Random.Range(-30f, 30f)) : new Vector2(-30f, -45f),
        });
    }

    void UpdatePopups()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            float age = Time.unscaledTime - p.born;
            if (age >= PopupLife || p.rt == null) { if (p.rt != null) Destroy(p.rt.gameObject); popups.RemoveAt(i); continue; }
            p.rt.anchoredPosition += p.velocity * Time.unscaledDeltaTime;
            float pop = age < 0.1f ? 1f + 0.35f * (1f - age / 0.1f) : 1f;   // little punch when it appears
            p.rt.localScale = Vector3.one * pop;
            var c = p.text.color;
            c.a = Mathf.Clamp01((PopupLife - age) / 0.4f);
            p.text.color = c;
        }
    }

    void Toast(string s)
    {
        if (toasts.Count > 4) toasts.Dequeue();
        toasts.Enqueue(s);
    }

    // ---------------------------------------------------------------- cards
    // The door opener picks with their stick + X / □ / R2 (CardManager reads their input), or a mouse click.
    // Solo: the game is paused behind the cards. Co-op: everyone sees them, the game goes on, with a timer.
    void ShowCards(CardData[] cards)
    {
        CloseCards();
        var cm = CardManager.Instance;
        bool live = cm != null && !cm.PausesGame;   // no pause (online later): see-through
        bool coop = gm.IsCoop;
        var panel = UIKit.Stretch("Cards", root);
        panel.SetAsLastSibling();
        UIKit.Image(panel, new Color(0, 0, 0, live ? 0.55f : 0.88f), !live);
        var chooser = cm != null ? PlayerSlot.Of(cm.Chooser) : null;
        bool mine = chooser == null || !chooser.IsRemote;   // online: only the chooser's own machine can pick
        string who = coop && chooser != null ? "ELIGE " + chooser.Tag + " (" + chooser.Animal.displayName.ToUpper() + ")" : "ELIGE TU DESTINO";
        UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1600, 80), who, 64, TextAnchor.MiddleCenter, coop && chooser != null ? chooser.Color : UIKit.Accent);
        UIKit.Label("Sub", panel, new Vector2(0.5f, 1f), new Vector2(0, -165), new Vector2(1600, 40),
            (coop && chooser != null ? (Net.Online ? (mine ? "Eliges tú" : "Elige " + chooser.Tag) : "Solo elige " + chooser.Tag + " con su mando") + " · la bendición y la maldición son para todo el equipo" : "Cada carta trae una bendición y una maldición para el resto de la partida") + (mine ? "   ·   ◄ ► elegir · X / □ coger" : ""), 28);
        cardTimer = UIKit.Label("Timer", panel, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(800, 50), "", 36, TextAnchor.MiddleCenter, Color.white);
        cardViews.Clear();
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            float x = (i - (cards.Length - 1) / 2f) * 520f;
            var frame = UIKit.Image(UIKit.Anchored("Frame" + i, panel, new Vector2(0.5f, 0.5f), new Vector2(x, -20), new Vector2(504, 584)), UIKit.Accent);   // bright border of the selected card
            var b = UIKit.Button("Card" + i, panel, new Vector2(0.5f, 0.5f), new Vector2(x, -20), new Vector2(480, 560), "", () => { if (mine) CardManager.Instance.Choose(card); });
            b.navigation = new Navigation { mode = Navigation.Mode.None };   // the chooser's own stick moves the selection
            var rt = (RectTransform)b.transform;
            UIKit.Image(UIKit.Rect("Stripe", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -16), Vector2.zero), card.color);
            UIKit.Label("Name", rt, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(440, 90), card.title.ToUpper(), 38, TextAnchor.MiddleCenter, UIKit.Accent);
            UIKit.Label("Bless", rt, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(420, 150), "BENDICIÓN\n" + card.blessingText, 28, TextAnchor.UpperCenter, new Color(0.5f, 1f, 0.55f));
            UIKit.Label("Curse", rt, new Vector2(0.5f, 1f), new Vector2(0, -340), new Vector2(420, 150), "MALDICIÓN\n" + card.curseText, 28, TextAnchor.UpperCenter, new Color(1f, 0.45f, 0.4f));
            cardViews.Add((rt, frame));
        }
        cardPanel = panel.gameObject;
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        HighlightCard(cm != null ? cm.Selected : 0);
    }

    void HighlightCard(int index)
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            bool on = i == index;
            cardViews[i].rt.localScale = Vector3.one * (on ? 1.06f : 0.92f);
            cardViews[i].frame.rectTransform.localScale = cardViews[i].rt.localScale;
            cardViews[i].frame.enabled = on;
        }
    }

    void UpdateCards()
    {
        var cm = CardManager.Instance;
        if (cardPanel == null || cm == null) return;
        if (cm.CurrentOffer == null) { CloseCards(); return; }
        if (cardTimer != null) cardTimer.text = cm.PausesGame ? "" : "Tiempo: " + Mathf.CeilToInt(cm.TimeLeft) + " s (si no, una al azar)";
    }

    void CloseCards()
    {
        if (cardPanel != null) Destroy(cardPanel);
        cardPanel = null;
        cardTimer = null;
        cardViews.Clear();
    }

    // ---------------------------------------------------------------- 3 players: map in the 4th quarter
    void BuildSpareMap()
    {
        var info = MapInfo.Instance;
        var quarter = UIKit.Rect("SpareView", root, new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        quarter.SetAsFirstSibling();
        UIKit.Image(quarter, new Color(0.03f, 0.05f, 0.08f, 1f));
        spareMap = quarter.gameObject;
        if (info == null || info.Texture == null)
        {
            UIKit.Label("Title", quarter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 60), "ANIMAL ISLAND", 48, TextAnchor.MiddleCenter, UIKit.Accent);
            return;
        }
        var map = UIKit.Anchored("Map", quarter, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(440, 440));
        map.gameObject.AddComponent<RawImage>().texture = info.Texture;
        UIKit.Label("Title", quarter, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(600, 40), MapTitle, 30, TextAnchor.MiddleCenter, UIKit.Accent);
        spareMapRect = map;
    }

    void UpdateSpareMap()
    {
        var info = MapInfo.Instance;
        if (spareMap == null || info == null || spareMapRect == null) return;
        while (spareDots.Count < gm.PlayerCount)   // one dot per player (they spawn after the HUD is built)
        {
            var slot = PlayerSlot.Of(gm.Players[spareDots.Count]);
            var color = slot != null ? slot.Color : Color.red;
            var dot = UIKit.Anchored("Dot", spareMapRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            UIKit.Image(dot, color);
            UIKit.Label("Tag", dot, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(60, 26), slot != null ? slot.Tag : "", 20, TextAnchor.MiddleCenter, color);
            spareDots.Add(dot);
        }
        for (int i = 0; i < spareDots.Count && i < gm.PlayerCount; i++)
        {
            var p = gm.Players[i];
            if (p == null) continue;
            var uv = info.ToUV(p.transform.position);
            spareDots[i].anchoredPosition = new Vector2((uv.x - 0.5f) * 440f, (uv.y - 0.5f) * 440f);
            spareDots[i].gameObject.SetActive(!p.IsDead);
        }
    }

    // ---------------------------------------------------------------- state screens
    void OnStateChanged(GameState s)
    {
        if ((s == GameState.GameOver || s == GameState.Victory) && cardPanel != null) CloseCards();
        if (s == GameState.Paused) ShowPause();
        else if (pausePanel != null) { Destroy(pausePanel); pausePanel = null; if (settingsPanel != null) Destroy(settingsPanel); }
        if (s == GameState.GameOver || s == GameState.Victory) ShowEnd(s == GameState.Victory);
    }

    void ShowPause()
    {
        var panel = UIKit.Stretch("Pause", root);
        UIKit.Image(panel, new Color(0, 0, 0, 0.7f), true);
        UIKit.Label("Title", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1000, 100), "PAUSA", 80, TextAnchor.MiddleCenter, UIKit.Accent);
        if (Net.Online)
            UIKit.Label("Online", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 205), new Vector2(1200, 40),
                "Partida online: el juego sigue mientras tanto" + (Net.IsHost && NetSession.Instance.JoinCode.Length > 0 ? "  ·  Código: " + NetSession.Instance.JoinCode : ""), 26, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.5f));
        var cards = CardManager.Instance != null ? string.Join("\n", CardManager.Instance.Taken.Select(c => c.title + ": " + c.blessingText + " / " + c.curseText)) : "";
        UIKit.Label("Cards", panel, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1600, 260), cards.Length > 0 ? "Cartas activas:\n" + cards : "", 22);
        var resume = UIKit.Button("Resume", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(520, 80), "Continuar", () => gm.SetPaused(false), 36);
        var settings = UIKit.Button("Settings", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(520, 80), "Ajustes", null, 36);
        string restartLabel = Net.Online ? "Reiniciar (todos)" : "Reiniciar partida";
        string menuLabel = Net.IsHost ? "Volver a la sala (todos)" : Net.IsClient ? "Abandonar la partida" : "Salir al menú";
        var restart = UIKit.Button("Restart", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(520, 80), restartLabel, gm.Restart, 36);
        var menu = UIKit.Button("Menu", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(520, 80), menuLabel, gm.ReturnToMenu, 36);
        if (Net.IsClient) restart.gameObject.SetActive(false);   // only the host restarts
        settings.onClick.AddListener(() =>
        {
            panel.gameObject.SetActive(false);
            settingsPanel = SettingsPanel.Build(root, () => { Destroy(settingsPanel); panel.gameObject.SetActive(true); UIKit.Select(settings); }, out var first);
            UIKit.Select(first);
        });
        if (Net.IsClient) UIKit.ChainVertical(resume, settings, menu);
        else UIKit.ChainVertical(resume, settings, restart, menu);
        BuildPauseMap(panel);
        pausePanel = panel.gameObject;
        UIKit.Select(resume);
    }

    // Map of the island with the players' positions (so nobody gets lost).
    void BuildPauseMap(RectTransform panel)
    {
        var info = MapInfo.Instance;
        if (info == null || info.Texture == null) return;
        const float size = 560f;
        var frame = UIKit.Anchored("Map", panel, new Vector2(0f, 0.5f), new Vector2(60f + size / 2f, 30f), new Vector2(size + 12f, size + 12f));
        UIKit.Image(frame, new Color(0f, 0f, 0f, 0.8f));
        var map = UIKit.Rect("Image", frame, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
        map.gameObject.AddComponent<RawImage>().texture = info.Texture;
        UIKit.Label("MapTitle", frame, new Vector2(0.5f, 1f), new Vector2(0, 30), new Vector2(size, 40), MapTitle, 30, TextAnchor.MiddleCenter, UIKit.Accent);
        foreach (var p in gm.Players)
        {
            if (p == null || p.IsDead) continue;
            var slot = PlayerSlot.Of(p);
            var uv = info.ToUV(p.transform.position);
            var pos = new Vector2((uv.x - 0.5f) * size, (uv.y - 0.5f) * size);
            var color = gm.IsCoop && slot != null ? slot.Color : new Color(1f, 0.15f, 0.1f);
            UIKit.Image(UIKit.Anchored("Me", map, new Vector2(0.5f, 0.5f), pos, new Vector2(22, 22)), color);
            UIKit.Label("MeLabel", map, new Vector2(0.5f, 0.5f), pos + new Vector2(0, 26), new Vector2(240, 30), gm.IsCoop && slot != null ? slot.Tag : "ESTÁS AQUÍ", 22, TextAnchor.MiddleCenter, color);
        }
    }

    void ShowEnd(bool victory)
    {
        if (endPanel != null) return;
        var s = gm.ComputeScore();
        var panel = UIKit.Stretch("End", root);
        UIKit.Image(panel, new Color(0, 0, 0, 0.8f), true);
        string title = victory ? (gm.IsCoop ? "¡HABÉIS ESCAPADO DE LA ISLA!" : "¡HAS ESCAPADO DE LA ISLA!") : (gm.IsCoop ? "¡HABÉIS CAÍDO TODOS!" : "¡HAS MUERTO!");
        UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1600, 110), title, 90, TextAnchor.MiddleCenter, victory ? UIKit.Accent : RoundColor);
        int round = RoundManager.Instance != null ? RoundManager.Instance.Round : 0;
        float t = gm.RunTime;
        string stats =
            "Ronda alcanzada: " + round +
            "\nAbuelos abatidos: " + gm.Kills + "  (a la cabeza: " + gm.Headshots + ")" +
            "\nTiempo: " + Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00") +
            "\nZonas abiertas: " + gm.DoorsOpened + "   Cartas: " + gm.CardsTaken +
            "\n\nConchas ganadas: " + s.points +
            "\nSupervivencia: " + s.rounds +
            (s.boss > 0 ? "\nJefe derrotado: " + s.boss : "") +
            (s.extracted > 0 ? "\nExtracción: " + s.extracted : "") +
            (s.itemMultiplier > 1f ? "\nObjeto sacado de la isla: ×" + s.itemMultiplier : "") +
            "\nMaldiciones asumidas: ×" + s.curseMultiplier.ToString("0.0") +
            "\n\nPUNTUACIÓN: " + s.total;
        if (gm.IsCoop)
            stats += "\n\n" + string.Join("\n", gm.Players.Select(p =>
            {
                var slot = PlayerSlot.Of(p);
                return slot == null ? "" : slot.Tag + " " + slot.Animal.displayName + ": " + slot.Kills + " bajas · " + slot.Downs + " caídas · " + slot.Revives + " rescates";
            }));
        UIKit.Label("Stats", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 700), stats, gm.IsCoop ? 28 : 32);
        string menuLabel = Net.IsHost ? "Volver a la sala" : Net.IsClient ? "Abandonar" : "Menú principal";
        var retry = UIKit.Button("Retry", panel, new Vector2(0.5f, 0f), new Vector2(-280, 70), new Vector2(500, 90), Net.IsHost ? "Reintentar (todos)" : "Reintentar", gm.Restart, 38);
        var menu = UIKit.Button("Menu", panel, new Vector2(0.5f, 0f), new Vector2(280, 70), new Vector2(500, 90), menuLabel, gm.ReturnToMenu, 38);
        endPanel = panel.gameObject;
        if (Net.IsClient)
        {
            // online client: the host decides whether to retry or go back to the room
            retry.gameObject.SetActive(false);
            UIKit.Label("Wait", panel, new Vector2(0.5f, 0f), new Vector2(-280, 70), new Vector2(560, 90), "El anfitrión decide si repetís...", 30, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.75f));
            UIKit.Select(menu);
            return;
        }
        UIKit.ChainHorizontal(retry, menu);
        UIKit.Select(retry);
    }
}
