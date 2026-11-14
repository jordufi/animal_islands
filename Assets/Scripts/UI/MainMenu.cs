using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Start screen. A 3D diorama of the island (the five animals lined up on the beach, an old man
// strolling behind them) with a title screen ("press X") and then the menu: start, animal (◄ ►),
// difficulty (◄ ►), settings, quit. The selected animal steps forward and hops. Start opens the map
// selector (one card per MapData); choosing a card loads that map's scene.
// "Pantalla dividida" opens the lobby: each gamepad joins with X (the keyboard with Enter), picks its
// animal with ◄ ► and leaves with O / Backspace; J1 also sets the difficulty and the game mode there
// (▲ ▼ to move between animal / dificultad / modo); START (or Enter on the keyboard) goes on to the maps.
// The joined players are the Party the GameManager spawns (2-4 views).
public partial class MainMenu : MonoBehaviour
{
    [SerializeField] AnimalData[] animals;
    [SerializeField] Transform[] showcase;          // one model per animal, same order
    [SerializeField] Camera menuCamera;
    [SerializeField] Transform oldMan;
    [SerializeField] Vector3 oldManFrom, oldManTo;
    [SerializeField] MapData[] maps;
    [SerializeField] string gameScene = "IslaZombie";   // used only when no maps are set
    [SerializeField] float stepForward = 0.9f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");

    enum Screen { Title, Menu, Settings, Maps, Loading, Lobby, Online, Room }
    Screen screen = Screen.Title;
    public string ScreenName => screen.ToString();   // tests

    Transform canvas;
    RectTransform titleBlock, menuPanel, cardPanel;
    Text pressText, animalName, animalTagline, animalPassive, animalAbility, difficultyInfo;
    Button startButton, splitButton, onlineButton, animalButton, difficultyButton, modeButton, settingsButton, quitButton;
    GameObject lobbyPanel;
    readonly System.Collections.Generic.List<Text> lobbySlots = new System.Collections.Generic.List<Text>();
    readonly System.Collections.Generic.Dictionary<Party.Member, float> lobbyStepAt = new System.Collections.Generic.Dictionary<Party.Member, float>();
    bool fromLobby;
    Text lobbySettings;
    int lobbyRow;                 // J1's cursor: 0 animal, 1 difficulty, 2 mode
    float lobbyRowStepAt;
    public int LobbyRow => lobbyRow;   // tests
    const float Row = 82f;
    const float MenuY = -135f;
    Text modeInfo;
    Image fade;
    GameObject settingsPanel, mapPanel;
    int animalIndex;
    Vector3[] basePositions;
    float[] hopStart;
    Vector3 cameraBase;
    Quaternion cameraBaseRot;
    float stickCooldown;
    bool oldManForward = true;
    float oldManPause;
    Animator oldManAnimator;

    void Start()
    {
        Time.timeScale = 1f;
        UIKit.EnsureEventSystem();
        animalIndex = Mathf.Max(0, System.Array.FindIndex(animals, a => a != null && a.id == GameSettings.Animal));
        basePositions = new Vector3[showcase.Length];
        hopStart = new float[showcase.Length];
        for (int i = 0; i < showcase.Length; i++)
        {
            basePositions[i] = showcase[i] != null ? showcase[i].position : Vector3.zero;
            hopStart[i] = -10f;
        }
        if (menuCamera != null) { cameraBase = menuCamera.transform.position; cameraBaseRot = menuCamera.transform.rotation; }
        if (oldMan != null) oldManAnimator = oldMan.GetComponentInChildren<Animator>();
        Build();
        RefreshTexts();
        StartCoroutine(Fade(1f, 0f, 0.8f));
        // back from an online match (or kicked out of one): straight to the room / the online screen
        if (NetSession.Instance != null && NetSession.Instance.Active) SkipTitle(OpenRoom);
        else if (NetSession.LastEndReason.Length > 0) SkipTitle(OpenOnline);
        else if (NetSession.HasArg("-nethost") || NetSession.Arg("-netjoin") != null) SkipTitle(AutoOnline);   // tests / shortcuts
    }

    // ---------------------------------------------------------------- UI
    void Build()
    {
        canvas = UIKit.CreateCanvas("MainMenuCanvas", 0).transform;

        // soft gradient on the left so the menu reads over the scene
        var shade = UIKit.Rect("Shade", canvas, new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        var shadeImg = UIKit.Image(shade, new Color(0.02f, 0.05f, 0.1f, 1f));
        shadeImg.sprite = UIKit.GradientSprite(0.75f, 0f);

        titleBlock = UIKit.Anchored("Title", canvas, new Vector2(0, 1), new Vector2(80, -28), new Vector2(900, 300));
        var logo = UIKit.Text(UIKit.Anchored("Logo", titleBlock, new Vector2(0, 1), Vector2.zero, new Vector2(900, 230)), "ANIMAL\nISLAND", 118, TextAnchor.UpperLeft, UIKit.Accent);
        logo.lineSpacing = 0.8f;
        var outline = logo.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.12f, 0.02f, 1f);
        outline.effectDistance = new Vector2(4, -4);
        UIKit.Text(UIKit.Anchored("Sub", titleBlock, new Vector2(0, 1), new Vector2(4, -238), new Vector2(900, 40)),
            "Los jubilados han tomado el resort. Tú solo querías un mojito.", 30, TextAnchor.UpperLeft, new Color(1f, 0.95f, 0.85f));

        pressText = UIKit.Label("Press", canvas, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1200, 60), "PULSA  X  PARA EMPEZAR", 44, TextAnchor.MiddleCenter, Color.white);
        pressText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

        // menu panel (slides in from the left)
        menuPanel = UIKit.Anchored("Menu", canvas, new Vector2(0, 0.5f), new Vector2(-700, MenuY), new Vector2(620, 740));
        startButton = MenuButton("Start", 0, "EMPEZAR PARTIDA", StartGame, 40);
        splitButton = MenuButton("Split", 1, "PANTALLA DIVIDIDA (2-4)", OpenLobby, 34);
        onlineButton = MenuButton("Online", 2, "JUGAR ONLINE (2-4)", OpenOnline, 34);
        animalButton = MenuButton("Animal", 3, "", NextAnimal, 32);
        difficultyButton = MenuButton("Difficulty", 4, "", NextDifficulty, 32);
        difficultyInfo = UIKit.Label("DifficultyInfo", menuPanel, new Vector2(0, 1), new Vector2(20, -4.9f * Row), new Vector2(600, 34), "", 22, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.75f));
        modeButton = MenuButton("Mode", 5.3f, "", ToggleMode, 32);
        modeInfo = UIKit.Label("ModeInfo", menuPanel, new Vector2(0, 1), new Vector2(20, -6.2f * Row), new Vector2(600, 34), "", 22, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.75f));
        settingsButton = MenuButton("Settings", 6.6f, "Ajustes", OpenSettings, 32);
        quitButton = MenuButton("Quit", 7.5f, "Salir", Quit, 32);
        UIKit.ChainVertical(startButton, splitButton, onlineButton, animalButton, difficultyButton, modeButton, settingsButton, quitButton);
        menuPanel.gameObject.SetActive(false);

        // selected animal card (bottom right)
        cardPanel = UIKit.Anchored("AnimalCard", canvas, new Vector2(1, 1), new Vector2(-60, -60), new Vector2(620, 260));
        UIKit.Image(cardPanel, new Color(0.03f, 0.06f, 0.1f, 0.8f));
        animalName = UIKit.Label("Name", cardPanel, new Vector2(0, 1), new Vector2(24, -14), new Vector2(580, 60), "", 48, TextAnchor.UpperLeft);
        animalTagline = UIKit.Label("Tagline", cardPanel, new Vector2(0, 1), new Vector2(24, -74), new Vector2(580, 36), "", 24, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.8f));
        animalPassive = UIKit.Label("Passive", cardPanel, new Vector2(0, 1), new Vector2(24, -118), new Vector2(580, 60), "", 24, TextAnchor.UpperLeft, new Color(0.6f, 1f, 0.65f));
        animalAbility = UIKit.Label("Ability", cardPanel, new Vector2(0, 1), new Vector2(24, -180), new Vector2(580, 70), "", 24, TextAnchor.UpperLeft, new Color(0.6f, 0.85f, 1f));
        cardPanel.gameObject.SetActive(false);

        UIKit.Label("Controls", canvas, new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(1800, 34),
            "R2 disparar · L2 apuntar · □ usar/recargar · Triángulo cambiar arma · L1 habilidad · X saltar · L3 correr · OPTIONS pausa", 22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f));
        UIKit.Label("Version", canvas, new Vector2(1, 0), new Vector2(-30, 56), new Vector2(500, 30), "Isla Zombie · v0.3", 20, TextAnchor.LowerRight, new Color(1, 1, 1, 0.6f));

        fade = UIKit.Image(UIKit.Stretch("Fade", canvas), Color.black);
        fade.raycastTarget = false;
    }

    Button MenuButton(string name, float row, string label, UnityEngine.Events.UnityAction action, int size)
    {
        var b = UIKit.Button(name, menuPanel, new Vector2(0, 1), new Vector2(0, -row * Row), new Vector2(600, 72), label, action, size);
        var colors = b.colors;
        colors.normalColor = new Color(0.06f, 0.1f, 0.16f, 0.88f);
        b.colors = colors;
        var stripe = UIKit.Rect("Stripe", b.transform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(8, 0));
        UIKit.Image(stripe, UIKit.Accent);
        var txt = b.GetComponentInChildren<Text>();
        txt.alignment = TextAnchor.MiddleLeft;
        ((RectTransform)txt.transform).offsetMin = new Vector2(30, 0);
        return b;
    }

    void RefreshTexts()
    {
        var a = animals[animalIndex];
        animalButton.GetComponentInChildren<Text>().text = "Animal:  ◄  " + a.displayName + "  ►";
        var p = GameSettings.Preset(GameSettings.Difficulty);
        difficultyButton.GetComponentInChildren<Text>().text = "Dificultad:  ◄  " + p.label + "  ►";
        difficultyInfo.text = p.description;
        bool cagon = GameSettings.AnselmoCagon;
        modeButton.GetComponentInChildren<Text>().text = "Modo:  ◄  " + (cagon ? "Anselmo cagón" : "Clásico") + "  ►";
        modeInfo.text = cagon ? "La mitad de los abuelos atajan por los WC y las entradas." : "Los abuelos te persiguen andando, como siempre.";
        animalName.text = a.displayName.ToUpper();
        animalName.color = a.uiColor;
        animalTagline.text = a.tagline;
        animalPassive.text = "Pasiva · " + a.passiveName + ": " + a.passiveDescription;
        animalAbility.text = "L1 · " + a.abilityName + ": " + a.abilityDescription;
    }

    // ---------------------------------------------------------------- input
    bool SubmitPressed()
    {
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        return (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) ||
               (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
               (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }

    bool CancelPressed()
    {
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        return (pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame));
    }

    int HorizontalPressed()
    {
        int dir = 0;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) dir = -1;
            if (pad.dpad.right.wasPressedThisFrame) dir = 1;
            float x = pad.leftStick.x.ReadValue();
            if (dir == 0 && Mathf.Abs(x) > 0.6f && Time.unscaledTime >= stickCooldown) { dir = x > 0 ? 1 : -1; stickCooldown = Time.unscaledTime + 0.3f; }
            if (Mathf.Abs(x) < 0.3f) stickCooldown = 0f;
        }
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dir = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dir = 1;
        }
        return dir;
    }

    void Update()
    {
        if (screen == Screen.Title)
        {
            pressText.color = new Color(1, 1, 1, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f));
            if (SubmitPressed() && fade.color.a < 0.3f) StartCoroutine(ShowMenu());
        }
        else if (screen == Screen.Maps)
        {
            if (CancelPressed()) CloseMaps();
        }
        else if (screen == Screen.Lobby) UpdateLobby();
        else if (screen == Screen.Online) UpdateOnline();
        else if (screen == Screen.Room) UpdateRoom();
        else if (screen == Screen.Menu)
        {
            var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            int h = HorizontalPressed();
            if (h != 0 && selected == animalButton.gameObject) ChangeAnimal(h);
            else if (h != 0 && selected == difficultyButton.gameObject) ChangeDifficulty(h);
            else if (h != 0 && selected == modeButton.gameObject) ToggleMode();
        }
        AnimateScene();
    }

    // ---------------------------------------------------------------- screens
    IEnumerator ShowMenu()
    {
        screen = Screen.Loading;   // block input during the transition
        pressText.gameObject.SetActive(false);
        menuPanel.gameObject.SetActive(true);
        cardPanel.gameObject.SetActive(true);
        Vector2 from = new Vector2(-700, MenuY), to = new Vector2(80, MenuY);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
        {
            menuPanel.anchoredPosition = Vector2.Lerp(from, to, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        menuPanel.anchoredPosition = to;
        hopStart[animalIndex] = Time.time;
        yield return new WaitForSecondsRealtime(0.1f);
        screen = Screen.Menu;
        UIKit.Select(startButton);
    }

    void NextAnimal() => ChangeAnimal(1);
    void NextDifficulty() => ChangeDifficulty(1);

    void ChangeAnimal(int dir)
    {
        animalIndex = (animalIndex + dir + animals.Length) % animals.Length;
        GameSettings.Animal = animals[animalIndex].id;
        hopStart[animalIndex] = Time.time;
        RefreshTexts();
    }

    void ChangeDifficulty(int dir)
    {
        GameSettings.Difficulty = (Difficulty)(((int)GameSettings.Difficulty + dir + 4) % 4);
        RefreshTexts();
    }

    // Game mode: classic / "Anselmo cagón" (old men take shortcuts through the spawn entrances)
    void ToggleMode()
    {
        GameSettings.AnselmoCagon = !GameSettings.AnselmoCagon;
        RefreshTexts();
    }

    void OpenSettings()
    {
        screen = Screen.Settings;
        menuPanel.gameObject.SetActive(false);
        cardPanel.gameObject.SetActive(false);
        settingsPanel = SettingsPanel.Build(canvas, CloseSettings, out var first);
        fade.transform.SetAsLastSibling();
        UIKit.Select(first);
    }

    void CloseSettings()
    {
        Destroy(settingsPanel);
        menuPanel.gameObject.SetActive(true);
        cardPanel.gameObject.SetActive(true);
        screen = Screen.Menu;
        UIKit.Select(settingsButton);
    }

    void StartGame()
    {
        if (screen != Screen.Menu) return;
        Party.Clear();   // solo: one player, every device
        if (NetSession.Instance != null) NetSession.Instance.Leave();
        fromLobby = false;
        if (MapCount == 0)
        {
            screen = Screen.Loading;
            GameSettings.Save();
            StartCoroutine(LoadGame(gameScene, "la isla"));
            return;
        }
        OpenMapSelector();
    }

    // ---------------------------------------------------------------- split-screen lobby
    public void OpenLobby()
    {
        if (screen != Screen.Menu && screen != Screen.Maps) return;
        screen = Screen.Lobby;
        fromLobby = true;
        Party.Prune();
        menuPanel.gameObject.SetActive(false);
        cardPanel.gameObject.SetActive(false);
        titleBlock.gameObject.SetActive(false);
        if (lobbyPanel != null) Destroy(lobbyPanel);
        var panel = UIKit.Stretch("Lobby", canvas);
        UIKit.Image(panel, new Color(0.02f, 0.05f, 0.1f, 0.82f), true);
        UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1400, 90), "PANTALLA DIVIDIDA", 72, TextAnchor.MiddleCenter, UIKit.Accent)
            .gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.12f, 0.02f, 1f);
        UIKit.Label("Help", panel, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(1700, 40),
            "X (Enter en el teclado) para unirte  ·  ◄ ► animal  ·  J1: ▲ ▼ dificultad y modo  ·  O / Retroceso para salir  ·  START (Enter) para elegir isla", 24);
        lobbyRow = 0;
        lobbySlots.Clear();
        for (int i = 0; i < Party.MaxPlayers; i++)
        {
            float x = (i - (Party.MaxPlayers - 1) / 2f) * 430f;
            var box = UIKit.Anchored("Slot" + i, panel, new Vector2(0.5f, 0.5f), new Vector2(x, -20), new Vector2(400, 420));
            UIKit.Image(box, new Color(0.06f, 0.1f, 0.16f, 0.95f));
            UIKit.Image(UIKit.Rect("Stripe", box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -12), Vector2.zero), PlayerSlot.Colors[i]);
            lobbySlots.Add(UIKit.Text(UIKit.Rect("Text", box, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -24)), "", 30, TextAnchor.MiddleCenter, Color.white));
        }
        // difficulty + mode for the whole run (J1 changes them)
        var settingsBox = UIKit.Anchored("Settings", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(1100, 150));
        UIKit.Image(settingsBox, new Color(0.06f, 0.1f, 0.16f, 0.95f));
        lobbySettings = UIKit.Text(UIKit.Rect("Text", settingsBox, Vector2.zero, Vector2.one, new Vector2(20, 8), new Vector2(-20, -8)), "", 30, TextAnchor.MiddleCenter, Color.white);
        UIKit.Label("Back", panel, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1400, 40), "Sin jugadores: O / Esc vuelve al menú", 24, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
        lobbyPanel = panel.gameObject;
        fade.transform.SetAsLastSibling();
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        RefreshLobby();
    }

    void RefreshLobby()
    {
        for (int i = 0; i < lobbySlots.Count; i++)
        {
            var t = lobbySlots[i];
            if (i < Party.Count)
            {
                var m = Party.Members[i];
                var a = System.Array.Find(animals, x => x != null && x.id == m.animal);
                string name = (a != null ? a.displayName.ToUpper() : m.animal);
                if (i == 0 && lobbyRow == 0) name = "<color=#FFFFFF>" + name + "</color>";   // J1's cursor is on the animal
                t.text = "J" + (i + 1) + "\n<size=22>" + m.Label + "</size>\n\n◄  " + name + "  ►\n\n<size=22>" + (a != null ? a.tagline : "") + "</size>";
                t.color = PlayerSlot.Colors[i];
            }
            else
            {
                t.text = "J" + (i + 1) + "\n\nPulsa X\npara unirte";
                t.color = new Color(1, 1, 1, 0.45f);
            }
        }
        if (lobbySettings != null)
        {
            var p = GameSettings.Preset(GameSettings.Difficulty);
            string Row(int row, string text) => lobbyRow == row && Party.Count > 0 ? "<size=34><color=#FFB833>" + text + "</color></size>" : "<color=#FFFFFF99>" + text + "</color>";
            lobbySettings.text = Row(1, "Dificultad:  ◄  " + p.label + "  ►") + "\n" +
                                 Row(2, "Modo:  ◄  " + (GameSettings.AnselmoCagon ? "Anselmo cagón" : "Clásico") + "  ►") +
                                 "\n<size=20><color=#FFFFFF99>" + (lobbyRow == 2 ? (GameSettings.AnselmoCagon ? "La mitad de los abuelos atajan por los WC y las entradas." : "Los abuelos os persiguen andando, como siempre.") : p.description) + "</color></size>";
        }
    }

    // J1 moves between animal / difficulty / mode; ◄ ► changes the row under the cursor.
    void LobbyRowStep(int dir)
    {
        lobbyRow = Mathf.Clamp(lobbyRow + dir, 0, 2);
        RefreshLobby();
    }

    void LobbyChange(Party.Member m, int dir)
    {
        bool first = Party.Count > 0 && Party.Members[0] == m;
        if (!first || lobbyRow == 0) { ChangeMemberAnimal(m, dir); return; }
        if (lobbyRow == 1) ChangeDifficulty(dir);
        else ToggleMode();
        RefreshLobby();
    }

    void UpdateLobby()
    {
        var kb = Keyboard.current;
        // join / start / leave per device
        foreach (var pad in Gamepad.all)
        {
            var m = Party.WithDevice(pad);
            if (m == null)
            {
                if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) Join(pad);
                else if (pad.buttonEast.wasPressedThisFrame && Party.Count == 0) { CloseLobby(); return; }
                continue;
            }
            if (pad.buttonEast.wasPressedThisFrame) { Leave(m); continue; }
            if (pad.startButton.wasPressedThisFrame) { LobbyDone(); return; }
            float x = pad.leftStick.x.ReadValue();
            int dir = pad.dpad.left.wasPressedThisFrame ? -1 : pad.dpad.right.wasPressedThisFrame ? 1 : 0;
            if (dir == 0 && Mathf.Abs(x) > 0.6f && Time.unscaledTime >= (lobbyStepAt.TryGetValue(m, out var at) ? at : 0f)) { dir = x > 0 ? 1 : -1; lobbyStepAt[m] = Time.unscaledTime + 0.3f; }
            if (Mathf.Abs(x) < 0.3f) lobbyStepAt[m] = 0f;
            if (dir != 0) LobbyChange(m, dir);
            if (Party.Members[0] == m)   // J1: up / down between animal, difficulty, mode
            {
                float y = pad.leftStick.y.ReadValue();
                int v = pad.dpad.up.wasPressedThisFrame ? -1 : pad.dpad.down.wasPressedThisFrame ? 1 : 0;
                if (v == 0 && Mathf.Abs(y) > 0.6f && Time.unscaledTime >= lobbyRowStepAt) { v = y > 0 ? -1 : 1; lobbyRowStepAt = Time.unscaledTime + 0.3f; }
                if (Mathf.Abs(y) < 0.3f) lobbyRowStepAt = 0f;
                if (v != 0) LobbyRowStep(v);
            }
        }
        if (kb != null)
        {
            var m = Party.WithDevice(kb);
            if (m == null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Join(kb, Mouse.current);
                else if ((kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) && Party.Count == 0) { CloseLobby(); return; }
            }
            else
            {
                if (kb.backspaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) Leave(m);
                else if (kb.enterKey.wasPressedThisFrame) { LobbyDone(); return; }
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) LobbyChange(m, -1);
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) LobbyChange(m, 1);
                else if (Party.Members[0] == m && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) LobbyRowStep(-1);
                else if (Party.Members[0] == m && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) LobbyRowStep(1);
            }
        }
    }

    // Tests can drive the lobby with these too.
    public Party.Member Join(params InputDevice[] devices)
    {
        if (screen != Screen.Lobby || Party.Count >= Party.MaxPlayers) return null;
        var list = System.Array.FindAll(devices, d => d != null);
        if (list.Length == 0 || Party.WithDevice(list[0]) != null) return null;
        // a different animal for each new player by default (repeating is allowed)
        var a = animals[(System.Array.FindIndex(animals, x => x != null && x.id == GameSettings.Animal) + Party.Count + animals.Length) % animals.Length];
        var m = Party.Add(a.id, list);
        RefreshLobby();
        return m;
    }

    void Leave(Party.Member m)
    {
        Party.Remove(m);
        if (Party.Count == 0) lobbyRow = 0;
        RefreshLobby();
    }

    void ChangeMemberAnimal(Party.Member m, int dir)
    {
        int i = System.Array.FindIndex(animals, x => x != null && x.id == m.animal);
        m.animal = animals[(i + dir + animals.Length) % animals.Length].id;
        RefreshLobby();
    }

    public void LobbyDone()
    {
        if (screen != Screen.Lobby || Party.Count == 0) return;
        if (lobbyPanel != null) Destroy(lobbyPanel);
        lobbyPanel = null;
        titleBlock.gameObject.SetActive(true);
        screen = Screen.Menu;
        if (MapCount == 0)
        {
            screen = Screen.Loading;
            GameSettings.Save();
            StartCoroutine(LoadGame(gameScene, "la isla"));
            return;
        }
        OpenMapSelector();
    }

    void CloseLobby()
    {
        if (lobbyPanel != null) Destroy(lobbyPanel);
        lobbyPanel = null;
        titleBlock.gameObject.SetActive(true);
        fromLobby = false;
        Party.Clear();
        menuPanel.gameObject.SetActive(true);
        cardPanel.gameObject.SetActive(true);
        screen = Screen.Menu;
        UIKit.Select(splitButton);
    }

    // ---------------------------------------------------------------- map selector
    public int MapCount => maps != null ? System.Array.FindAll(maps, m => m != null).Length : 0;

    public void OpenMapSelector()
    {
        screen = Screen.Maps;
        menuPanel.gameObject.SetActive(false);
        cardPanel.gameObject.SetActive(false);
        if (mapPanel != null) Destroy(mapPanel);
        var panel = UIKit.Stretch("Maps", canvas);
        UIKit.Image(panel, new Color(0.02f, 0.05f, 0.1f, 0.78f), true);
        UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1400, 90), "ELIGE ISLA", 72, TextAnchor.MiddleCenter, UIKit.Accent)
            .gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.12f, 0.02f, 1f);
        var list = System.Array.FindAll(maps, m => m != null);
        var buttons = new System.Collections.Generic.List<Selectable>();
        Button preselect = null;
        for (int i = 0; i < list.Length; i++)
        {
            var map = list[i];
            int index = i;
            float x = (i - (list.Length - 1) / 2f) * 580f;
            var b = UIKit.Button("Map_" + map.id, panel, new Vector2(0.5f, 0.5f), new Vector2(x, -10), new Vector2(540, 640), "", () => PlayMap(index));
            var rt = (RectTransform)b.transform;
            UIKit.Image(UIKit.Rect("Stripe", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -10), Vector2.zero), UIKit.Accent);
            var pic = UIKit.Anchored("Preview", rt, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(460, 440));   // anchored = pivot: top edge
            if (map.preview != null) pic.gameObject.AddComponent<RawImage>().texture = map.preview;
            else UIKit.Image(pic, new Color(0.1f, 0.4f, 0.55f));
            UIKit.Label("Name", rt, new Vector2(0.5f, 0f), new Vector2(0, 108), new Vector2(500, 56), map.displayName.ToUpper(), 44, TextAnchor.MiddleCenter, UIKit.Accent);
            UIKit.Label("Desc", rt, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(500, 90), map.description, 22, TextAnchor.UpperCenter, new Color(1, 1, 1, 0.85f));
            buttons.Add(b);
            if (preselect == null || map.id == GameSettings.Map) preselect = b;   // the last map played, else the first
        }
        var back = UIKit.Button("Back", panel, new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(360, 70), "Volver", CloseMaps, 30);
        UIKit.ChainHorizontal(buttons.ToArray());
        foreach (var b in buttons)
        {
            var nav = b.navigation;
            nav.selectOnDown = back;
            b.navigation = nav;
        }
        var backNav = back.navigation;
        backNav.mode = Navigation.Mode.Explicit;
        backNav.selectOnUp = preselect;
        back.navigation = backNav;
        mapPanel = panel.gameObject;
        fade.transform.SetAsLastSibling();
        UIKit.Select(preselect);
    }

    void CloseMaps()
    {
        if (mapPanel != null) Destroy(mapPanel);
        if (fromLobby) { mapPanel = null; OpenLobby(); return; }
        menuPanel.gameObject.SetActive(true);
        cardPanel.gameObject.SetActive(true);
        screen = Screen.Menu;
        UIKit.Select(startButton);
    }

    public void PlayMap(int index)
    {
        if (screen != Screen.Maps) return;
        var list = System.Array.FindAll(maps, m => m != null);
        if (index < 0 || index >= list.Length) return;
        screen = Screen.Loading;
        GameSettings.Map = list[index].id;
        GameSettings.Save();
        StartCoroutine(LoadGame(list[index].sceneName, list[index].displayName));
    }

    IEnumerator LoadGame(string scene, string mapName)
    {
        hopStart[animalIndex] = Time.time;
        yield return Fade(0f, 1f, 0.6f);
        var label = UIKit.Label("Loading", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 80), "Cargando " + mapName + "...", 48);
        label.transform.SetAsLastSibling();
        yield return null;
        SceneManager.LoadScene(scene);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t));
            yield return null;
        }
        fade.color = new Color(0, 0, 0, to);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- 3D scene
    void AnimateScene()
    {
        // selected animal steps towards the camera and hops; the rest idle in the line-up
        Vector3 toCamera = menuCamera != null ? menuCamera.transform.position : Vector3.zero;
        for (int i = 0; i < showcase.Length; i++)
        {
            var s = showcase[i];
            if (s == null) continue;
            bool selected = i == animalIndex && screen != Screen.Title;
            Vector3 dir = toCamera - basePositions[i];
            dir.y = 0f;
            Vector3 target = basePositions[i] + (selected ? dir.normalized * stepForward : Vector3.zero);
            float hopT = Time.time - hopStart[i];
            float hop = hopT < 0.5f ? Mathf.Sin(hopT / 0.5f * Mathf.PI) * 0.45f : 0f;
            Vector3 pos = Vector3.Lerp(new Vector3(s.position.x, target.y, s.position.z), target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            s.position = pos + Vector3.up * hop;
            var anim = s.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetFloat(SpeedHash, 0f);
                anim.SetBool(GroundedHash, hopT >= 0.45f);
            }
        }

        // old man strolls along the shore, stops, turns around
        if (oldMan != null)
        {
            Vector3 goal = oldManForward ? oldManTo : oldManFrom;
            Vector3 d = goal - oldMan.position;
            d.y = 0f;
            float speed = 0f;
            if (oldManPause > 0f) oldManPause -= Time.deltaTime;
            else if (d.magnitude < 0.2f) { oldManForward = !oldManForward; oldManPause = 2.5f; }
            else
            {
                speed = 1.7f;
                oldMan.position += d.normalized * speed * Time.deltaTime;
                oldMan.rotation = Quaternion.RotateTowards(oldMan.rotation, Quaternion.LookRotation(d), 240f * Time.deltaTime);
            }
            if (oldManAnimator != null) oldManAnimator.SetFloat(SpeedHash, speed, 0.15f, Time.deltaTime);
        }

        // gentle camera drift
        if (menuCamera != null)
        {
            float t = Time.time;
            menuCamera.transform.position = cameraBase + new Vector3(0f, Mathf.Sin(t * 0.4f) * 0.08f, Mathf.Sin(t * 0.25f) * 0.25f);
            menuCamera.transform.rotation = cameraBaseRot * Quaternion.Euler(Mathf.Sin(t * 0.3f) * 0.6f, Mathf.Sin(t * 0.2f) * 1.2f, 0f);
        }
    }
}
