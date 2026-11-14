using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Online screens of the main menu. "JUGAR ONLINE": create a room (Unity Relay gives a 6-letter code to pass
// to the friends) or join one with a code (or an IP for the local network). The room: everyone picks their
// animal (◄ ►); the host also sets difficulty, mode and island (▲ ▼ to move between them) and starts the
// match (START / Enter / the button). Each friend plays on their own PC at home; the host's PC runs it.
public partial class MainMenu
{
    GameObject onlinePanel, roomPanel;
    InputField codeField;
    Text onlineStatus, roomCode, roomStatus, roomSettings, roomHelp;
    readonly List<Text> roomSlots = new List<Text>();
    Button createButton, joinButton, roomStart, roomCopy;
    int roomRow;            // host's cursor: 0 animal, 1 difficulty, 2 mode, 3 island
    float roomRowStepAt;
    bool roomSubscribed;
    public int RoomRow => roomRow;   // tests

    void SkipTitle(System.Action open)
    {
        pressText.gameObject.SetActive(false);
        menuPanel.anchoredPosition = new Vector2(80, MenuY);
        screen = Screen.Menu;
        open();
    }

    void HideMenu()
    {
        pressText.gameObject.SetActive(false);
        menuPanel.gameObject.SetActive(false);
        cardPanel.gameObject.SetActive(false);
        titleBlock.gameObject.SetActive(false);
    }

    void ShowMenuAgain(Selectable select)
    {
        titleBlock.gameObject.SetActive(true);
        menuPanel.gameObject.SetActive(true);
        cardPanel.gameObject.SetActive(true);
        screen = Screen.Menu;
        UIKit.Select(select);
    }

    // Tests / shortcuts: -nethost (direct IP host) or -netjoin <code or ip>, optional -netanimal <id>.
    void AutoOnline()
    {
        string animal = NetSession.Arg("-netanimal");
        if (!string.IsNullOrEmpty(animal)) GameSettings.Animal = animal;
        OpenOnline();
        var ns = NetSession.Ensure();
        if (NetSession.HasArg("-nethost")) ns.HostDirect();
        else ns.Join(NetSession.Arg("-netjoin"));
    }

    // ---------------------------------------------------------------- create / join
    public void OpenOnline()
    {
        if (screen != Screen.Menu && screen != Screen.Room && screen != Screen.Online) return;
        CloseRoomPanel();
        screen = Screen.Online;
        HideMenu();
        if (onlinePanel != null) Destroy(onlinePanel);
        var panel = UIKit.Stretch("Online", canvas);
        UIKit.Image(panel, new Color(0.02f, 0.05f, 0.1f, 0.85f), true);
        UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1400, 90), "JUGAR ONLINE", 72, TextAnchor.MiddleCenter, UIKit.Accent)
            .gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.12f, 0.02f, 1f);
        UIKit.Label("Help", panel, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(1700, 40),
            "Cada uno juega en su casa. Uno crea la partida y pasa el código; los demás lo escriben aquí (hasta 4).", 26);

        createButton = UIKit.Button("Create", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(700, 90), "CREAR PARTIDA", CreateRoom, 40);
        UIKit.Label("CodeLabel", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1000, 40), "¿Te han pasado un código? Escríbelo o pégalo:", 28);
        codeField = MakeInput(panel, new Vector2(-120, 50), new Vector2(460, 84));
        var paste = UIKit.Button("Paste", panel, new Vector2(0.5f, 0.5f), new Vector2(240, 50), new Vector2(220, 84), "Pegar", Paste, 30);
        joinButton = UIKit.Button("Join", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(700, 90), "UNIRSE", JoinRoom, 40);
        var lan = UIKit.Button("Lan", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(700, 64), "Crear partida por IP (red local, sin Relay)", HostLan, 24);
        onlineStatus = UIKit.Label("Status", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(1500, 150), NetSession.LastEndReason, 28, TextAnchor.UpperCenter, new Color(1f, 0.75f, 0.55f));
        NetSession.LastEndReason = "";
        var back = UIKit.Button("Back", panel, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(360, 70), "Volver", CloseOnline, 30);
        UIKit.ChainVertical(createButton, codeField, joinButton, lan, back);
        var nav = paste.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnLeft = codeField;
        nav.selectOnUp = createButton;
        nav.selectOnDown = joinButton;
        paste.navigation = nav;
        var fieldNav = codeField.navigation;
        fieldNav.selectOnRight = paste;
        codeField.navigation = fieldNav;
        onlinePanel = panel.gameObject;
        fade.transform.SetAsLastSibling();
        UIKit.Select(createButton);
    }

    InputField MakeInput(Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = UIKit.Anchored("Code", parent, new Vector2(0.5f, 0.5f), pos, size);
        var img = UIKit.Image(rt, new Color(1f, 1f, 1f, 0.95f), true);
        var field = rt.gameObject.AddComponent<InputField>();
        var text = UIKit.Text(UIKit.Rect("Text", rt, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-16, -6)), "", 40, TextAnchor.MiddleCenter, Color.black, false);
        text.supportRichText = false;
        var placeholder = UIKit.Text(UIKit.Rect("Placeholder", rt, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-16, -6)), "Código (ej. K7Q2XM)", 30, TextAnchor.MiddleCenter, new Color(0f, 0f, 0f, 0.4f), false);
        field.textComponent = text;
        field.placeholder = placeholder;
        field.targetGraphic = img;
        field.characterLimit = 40;
        field.onValidateInput += (t, i, c) => char.ToUpperInvariant(c);
        return field;
    }

    void CreateRoom() => NetSession.Ensure().HostRelay();
    void HostLan() => NetSession.Ensure().HostDirect();
    void JoinRoom() { if (codeField != null) NetSession.Ensure().Join(codeField.text); }
    void Paste() { if (codeField != null) codeField.text = (GUIUtility.systemCopyBuffer ?? "").Trim().ToUpperInvariant(); }

    void CloseOnline()
    {
        var ns = NetSession.Instance;
        if (ns != null && ns.Busy) return;   // wait for the connection attempt to finish
        if (ns != null) ns.Leave();
        if (onlinePanel != null) Destroy(onlinePanel);
        onlinePanel = null;
        ShowMenuAgain(onlineButton);
    }

    void UpdateOnline()
    {
        var ns = NetSession.Instance;
        bool busy = ns != null && ns.Busy;
        if (ns != null)
        {
            if (ns.Status.Length > 0) onlineStatus.text = ns.Status;
            if (ns.Active && ns.Me != null && ns.Roster.Count > 0) { OpenRoom(); return; }
        }
        createButton.interactable = joinButton.interactable = !busy;
        bool typing = codeField != null && codeField.isFocused;
        var kb = Keyboard.current;
        if (typing && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) JoinRoom();
        if (!typing && CancelPressed()) CloseOnline();
    }

    // ---------------------------------------------------------------- room
    public void OpenRoom()
    {
        var ns = NetSession.Instance;
        if (ns == null) return;
        if (onlinePanel != null) Destroy(onlinePanel);
        onlinePanel = null;
        if (roomPanel != null) Destroy(roomPanel);
        screen = Screen.Room;
        HideMenu();
        roomRow = 0;
        var panel = UIKit.Stretch("Room", canvas);
        UIKit.Image(panel, new Color(0.02f, 0.05f, 0.1f, 0.85f), true);
        UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1400, 80), "SALA ONLINE", 64, TextAnchor.MiddleCenter, UIKit.Accent)
            .gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.12f, 0.02f, 1f);
        roomCode = UIKit.Label("Code", panel, new Vector2(0.5f, 1f), new Vector2(-150, -150), new Vector2(1000, 70), "", 52, TextAnchor.MiddleCenter, Color.white);
        roomCopy = UIKit.Button("Copy", panel, new Vector2(0.5f, 1f), new Vector2(500, -150), new Vector2(300, 64), "Copiar código", CopyCode, 28);
        roomSlots.Clear();
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            float x = (i - (NetSession.MaxPlayers - 1) / 2f) * 430f;
            var box = UIKit.Anchored("Slot" + i, panel, new Vector2(0.5f, 0.5f), new Vector2(x, 40), new Vector2(400, 340));
            UIKit.Image(box, new Color(0.06f, 0.1f, 0.16f, 0.95f));
            UIKit.Image(UIKit.Rect("Stripe", box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -12), Vector2.zero), PlayerSlot.Colors[i]);
            roomSlots.Add(UIKit.Text(UIKit.Rect("Text", box, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -24)), "", 30, TextAnchor.MiddleCenter, Color.white));
        }
        var settingsBox = UIKit.Anchored("Settings", panel, new Vector2(0.5f, 0.5f), new Vector2(0, -222), new Vector2(1100, 160));
        UIKit.Image(settingsBox, new Color(0.06f, 0.1f, 0.16f, 0.95f));
        roomSettings = UIKit.Text(UIKit.Rect("Text", settingsBox, Vector2.zero, Vector2.one, new Vector2(20, 8), new Vector2(-20, -8)), "", 30, TextAnchor.MiddleCenter, Color.white);
        roomHelp = UIKit.Label("Help", panel, new Vector2(0.5f, 0f), new Vector2(0, 182), new Vector2(1700, 40), "", 24);
        roomStart = UIKit.Button("Start", panel, new Vector2(0.5f, 0f), new Vector2(-220, 110), new Vector2(400, 80), "¡EMPEZAR!", StartOnlineMatch, 36);
        var leave = UIKit.Button("Leave", panel, new Vector2(0.5f, 0f), new Vector2(220, 110), new Vector2(400, 80), ns.IsHost ? "Cerrar la sala" : "Salir de la sala", LeaveRoom, 32);
        roomStatus = UIKit.Label("Status", panel, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1600, 40), "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.55f));
        roomStart.gameObject.SetActive(ns.IsHost);
        roomPanel = panel.gameObject;
        fade.transform.SetAsLastSibling();
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        if (!roomSubscribed) { ns.Changed += RefreshRoom; roomSubscribed = true; }
        // the menu diorama shows my animal
        int a = System.Array.FindIndex(animals, x => x != null && ns.Me != null && x.id == ns.Me.animal);
        if (a >= 0) { animalIndex = a; hopStart[a] = Time.time; }
        RefreshRoom();
    }

    void CloseRoomPanel()
    {
        if (roomSubscribed && NetSession.Instance != null) NetSession.Instance.Changed -= RefreshRoom;
        roomSubscribed = false;
        if (roomPanel != null) Destroy(roomPanel);
        roomPanel = null;
    }

    void OnDestroy() => CloseRoomPanel();

    MapData[] MapList => maps != null ? System.Array.FindAll(maps, m => m != null) : new MapData[0];

    MapData RoomMap()
    {
        var list = MapList;
        if (list.Length == 0) return null;
        var ns = NetSession.Instance;
        var m = ns != null ? System.Array.Find(list, x => x.id == ns.MapId) : null;
        return m != null ? m : list[0];
    }

    void RefreshRoom()
    {
        var ns = NetSession.Instance;
        if (ns == null || roomPanel == null) return;
        roomCode.text = ns.JoinCode.Length > 0 ? (ns.UsingRelay ? "CÓDIGO:  " : "IP:  ") + ns.JoinCode : "";
        int me = ns.LocalIndex;
        for (int i = 0; i < roomSlots.Count; i++)
        {
            var t = roomSlots[i];
            var m = ns.Roster.Find(x => x.index == i && !x.gone);
            if (m != null)
            {
                var a = System.Array.Find(animals, x => x != null && x.id == m.animal);
                string name = a != null ? a.displayName.ToUpper() : m.animal;
                bool mine = m.index == me;
                if (mine && roomRow == 0) name = "<color=#FFFFFF>" + name + "</color>";
                t.text = "J" + (i + 1) + (i == 0 ? " · anfitrión" : "") + "\n<size=22>" + (mine ? "(tú)" : "") + "</size>\n\n" +
                         (mine ? "◄  " + name + "  ►" : name) + "\n\n<size=22>" + (a != null ? a.tagline : "") + "</size>";
                t.color = PlayerSlot.Colors[i];
            }
            else
            {
                t.text = "J" + (i + 1) + "\n\nEsperando\njugador...";
                t.color = new Color(1, 1, 1, 0.4f);
            }
        }
        var p = GameSettings.Preset(ns.Difficulty);
        var map = RoomMap();
        bool host = ns.IsHost;
        string Row(int row, string text) => host && roomRow == row ? "<size=34><color=#FFB833>" + text + "</color></size>" : "<color=#FFFFFFCC>" + text + "</color>";
        string arrows(string s) => host ? "◄  " + s + "  ►" : s;
        roomSettings.text = Row(1, "Dificultad:  " + arrows(p.label)) + "\n" +
                            Row(2, "Modo:  " + arrows(ns.AnselmoCagon ? "Anselmo cagón" : "Clásico")) + "\n" +
                            Row(3, "Isla:  " + arrows(map != null ? map.displayName : "?"));
        roomHelp.text = host
            ? "◄ ► cambia tu animal · ▲ ▼ dificultad, modo e isla · START / Enter: ¡a jugar! (" + ns.PlayerCount + "/" + NetSession.MaxPlayers + ")"
            : "◄ ► cambia tu animal · el anfitrión elige dificultad, modo e isla y empieza la partida";
        if (ns.Status.Length > 0) roomStatus.text = ns.Status;
    }

    void UpdateRoom()
    {
        var ns = NetSession.Instance;
        if (ns == null || !ns.Active)
        {
            CloseRoomPanel();
            screen = Screen.Menu;
            OpenOnline();
            return;
        }
        if (ns.Status.Length > 0 && roomStatus != null) roomStatus.text = ns.Status;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if (CancelPressed()) { LeaveRoom(); return; }
        if (ns.IsHost && ((pad != null && pad.startButton.wasPressedThisFrame) || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))))
        {
            StartOnlineMatch();
            return;
        }
        if (ns.IsHost)
        {
            int v = 0;
            if (pad != null)
            {
                v = pad.dpad.up.wasPressedThisFrame ? -1 : pad.dpad.down.wasPressedThisFrame ? 1 : 0;
                float y = pad.leftStick.y.ReadValue();
                if (v == 0 && Mathf.Abs(y) > 0.6f && Time.unscaledTime >= roomRowStepAt) { v = y > 0 ? -1 : 1; roomRowStepAt = Time.unscaledTime + 0.3f; }
                if (Mathf.Abs(y) < 0.3f) roomRowStepAt = 0f;
            }
            if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) v = -1;
            if (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) v = 1;
            if (v != 0) { roomRow = Mathf.Clamp(roomRow + v, 0, 3); RefreshRoom(); }
        }
        int h = HorizontalPressed();
        if (h != 0) RoomChange(h);
    }

    // ◄ ►: my animal, or (host) the setting under the cursor.
    public void RoomChange(int dir)
    {
        var ns = NetSession.Instance;
        if (ns == null || ns.Me == null) return;
        if (!ns.IsHost || roomRow == 0)
        {
            int i = System.Array.FindIndex(animals, x => x != null && x.id == ns.Me.animal);
            i = (i + dir + animals.Length) % animals.Length;
            ns.SetMyAnimal(animals[i].id);
            animalIndex = i;
            hopStart[i] = Time.time;
            RefreshTexts();
        }
        else if (roomRow == 1)
        {
            var d = (Difficulty)(((int)ns.Difficulty + dir + 4) % 4);
            GameSettings.Difficulty = d;
            ns.SetSettings(d, ns.AnselmoCagon, ns.MapId);
        }
        else if (roomRow == 2)
        {
            GameSettings.AnselmoCagon = !ns.AnselmoCagon;
            ns.SetSettings(ns.Difficulty, !ns.AnselmoCagon, ns.MapId);
        }
        else
        {
            var list = MapList;
            if (list.Length == 0) return;
            int i = System.Array.IndexOf(list, RoomMap());
            var m = list[(i + dir + list.Length) % list.Length];
            GameSettings.Map = m.id;
            ns.SetSettings(ns.Difficulty, ns.AnselmoCagon, m.id);
        }
        RefreshRoom();
    }

    public void StartOnlineMatch()
    {
        var ns = NetSession.Instance;
        if (ns == null || !ns.IsHost || screen != Screen.Room) return;
        var map = RoomMap();
        string scene = map != null ? map.sceneName : gameScene;
        if (map != null) GameSettings.Map = map.id;
        GameSettings.Save();
        screen = Screen.Loading;
        CloseRoomPanel();
        var label = UIKit.Label("Loading", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 80), "Cargando " + (map != null ? map.displayName : "la isla") + "...", 48);
        label.transform.SetAsLastSibling();
        ns.StartMatch(scene);
    }

    void LeaveRoom()
    {
        var ns = NetSession.Instance;
        CloseRoomPanel();
        if (ns != null) ns.Leave();
        NetSession.LastEndReason = "";
        screen = Screen.Menu;
        OpenOnline();
    }

    void CopyCode()
    {
        var ns = NetSession.Instance;
        if (ns == null || ns.JoinCode.Length == 0) return;
        GUIUtility.systemCopyBuffer = ns.JoinCode;
        if (roomStatus != null) roomStatus.text = "Código copiado: pégalo en WhatsApp / Discord a tus amigos";
    }
}
