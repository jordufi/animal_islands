using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Settings panel shared by the main menu and the pause menu (camera sensitivity, invert Y, FOV, sprint mode, FPS counter, full
// screen, the "brújula de mierdecillas"; the compass shows from the next match).
public static class SettingsPanel
{
    public static GameObject Build(Transform parent, UnityAction onBack, out Selectable first)
    {
        var panel = UIKit.Stretch("SettingsPanel", parent);
        UIKit.Image(panel, new Color(0, 0, 0, 0.75f), true);
        var box = UIKit.Anchored("Box", panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 900));
        UIKit.Image(box, UIKit.PanelColor, true);
        UIKit.Label("Title", box, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(800, 70), "AJUSTES", 52, TextAnchor.MiddleCenter, UIKit.Accent);

        Text sensLabel = UIKit.Label("SensLabel", box, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(800, 40), "", 30);
        var sens = UIKit.Slider("Sensitivity", box, new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(640, 36), 0.2f, 3f, GameSettings.Sensitivity, v =>
        {
            GameSettings.Sensitivity = v;
            sensLabel.text = "Sensibilidad de cámara: " + v.ToString("0.0");
        });
        sensLabel.text = "Sensibilidad de cámara: " + GameSettings.Sensitivity.ToString("0.0");

        Text fovLabel = UIKit.Label("FovLabel", box, new Vector2(0.5f, 1f), new Vector2(0, -240), new Vector2(800, 40), "", 30);
        var fov = UIKit.Slider("Fov", box, new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(640, 36), 50f, 90f, GameSettings.FieldOfView, v =>
        {
            GameSettings.FieldOfView = Mathf.Round(v);
            fovLabel.text = "Campo de visión: " + Mathf.RoundToInt(v);
        });
        fovLabel.text = "Campo de visión: " + Mathf.RoundToInt(GameSettings.FieldOfView);

        Button invert = null;
        invert = UIKit.Button("InvertY", box, new Vector2(0.5f, 1f), new Vector2(0, -370), new Vector2(640, 70), InvertText(), () =>
        {
            GameSettings.InvertY = !GameSettings.InvertY;
            invert.GetComponentInChildren<Text>().text = InvertText();
        });

        Button sprint = null;
        sprint = UIKit.Button("Sprint", box, new Vector2(0.5f, 1f), new Vector2(0, -460), new Vector2(640, 70), SprintText(), () =>
        {
            GameSettings.SprintToggle = !GameSettings.SprintToggle;
            sprint.GetComponentInChildren<Text>().text = SprintText();
        });

        Button fps = null;
        fps = UIKit.Button("Fps", box, new Vector2(0.5f, 1f), new Vector2(0, -550), new Vector2(640, 70), FpsText(), () =>
        {
            GameSettings.ShowFps = !GameSettings.ShowFps;
            fps.GetComponentInChildren<Text>().text = FpsText();
        });

        Button full = null;
        full = UIKit.Button("Fullscreen", box, new Vector2(0.5f, 1f), new Vector2(0, -640), new Vector2(640, 70), FullscreenText(), () =>
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            full.GetComponentInChildren<Text>().text = FullscreenText();
        });

        Button compass = null;
        compass = UIKit.Button("Compass", box, new Vector2(0.5f, 1f), new Vector2(0, -730), new Vector2(640, 70), CompassText(), () =>
        {
            GameSettings.Compass = !GameSettings.Compass;
            compass.GetComponentInChildren<Text>().text = CompassText();
        });

        var back = UIKit.Button("Back", box, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(400, 80), "Volver", () =>
        {
            GameSettings.Save();
            onBack?.Invoke();
        }, 36);

        UIKit.ChainVertical(sens, fov, invert, sprint, fps, full, compass, back);
        first = sens;
        return panel.gameObject;
    }

    static string SprintText() => "Sprint con L3: " + (GameSettings.SprintToggle ? "PULSAR (se mantiene)" : "MANTENER");

    static string FpsText() => "Contador de FPS: " + (GameSettings.ShowFps ? "SÍ" : "NO");

    static string FullscreenText() => "Pantalla completa: " + (GameSettings.Fullscreen ? "SÍ" : "NO (ventana)");

    static string CompassText() => "Brújula de mierdecillas: " + (GameSettings.Compass ? "SÍ" : "NO");

    static string InvertText() => "Invertir eje Y: " + (GameSettings.InvertY ? "SÍ" : "NO");
}
