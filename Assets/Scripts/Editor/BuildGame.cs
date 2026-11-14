// Builds the game for Windows (64-bit) and zips it, ready to copy to another PC.
//   From the editor:        Tools > Island > Build Windows (.zip)
//   From the command line:  Tools\Build-Game.ps1 (Unity must be closed: a project can't be open twice), i.e.
//     Unity.exe -batchmode -quit -projectPath <project> -executeMethod BuildGame.CommandLine -logFile Builds\build.log
// Output (project root, outside Assets):
//   Builds\AnimalIsland_<date>_<time>\AnimalIsland.exe (+ data folders, LEEME.txt)
//   Builds\AnimalIsland_<date>_<time>.zip            <- the file to send
// The scenes are the ones in File > Build Profiles (MainMenu first); nothing in the project settings is changed.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildGame
{
    public const string BuildsFolder = "Builds";
    const string ExeName = "AnimalIsland.exe";
    const string Key = "BuildGame.";

    public static string Status => EditorPrefs.GetString(Key + "Status", "");   // running / done / failed
    public static string Result => EditorPrefs.GetString(Key + "Result", "");   // zip path or error

    [MenuItem("Tools/Island/Build Windows (.zip)")]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Build", "Sal del modo Play primero.", "OK"); return; }
        string zip = BuildWindowsZip();
        if (zip != null)
        {
            EditorUtility.RevealInFinder(zip);
            EditorUtility.DisplayDialog("Build terminada", "Juego exportado a:\n" + zip + "\n\nPásale ese .zip a tu colega: que lo descomprima y abra AnimalIsland.exe.", "OK");
        }
        else EditorUtility.DisplayDialog("Build fallida", Result, "OK");
    }

    // -executeMethod BuildGame.CommandLine (exit code 0 = ok, 1 = failed)
    public static void CommandLine()
    {
        string zip = BuildWindowsZip();
        Console.WriteLine(zip != null ? "BUILD OK: " + zip : "BUILD FAILED: " + Result);
        EditorApplication.Exit(zip != null ? 0 : 1);
    }

    // Returns the path of the .zip, or null (the reason is in Result).
    public static string BuildWindowsZip()
    {
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetString(Key + "Result", "");
        try
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0) return Fail("No hay escenas en File > Build Profiles (ejecuta Tools > Island > Setup Test Map).");

            string project = Path.GetDirectoryName(Application.dataPath);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            string name = "AnimalIsland_" + stamp;
            string folder = Path.Combine(project, BuildsFolder, name);
            Directory.CreateDirectory(folder);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            Debug.Log("[BuildGame] building " + scenes.Length + " scenes (" + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)) + ") to " + folder);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                return Fail("Unity no pudo compilar el juego (" + report.summary.result + ", " + report.summary.totalErrors + " errores). Mira la consola / Builds\\build.log.");

            File.WriteAllText(Path.Combine(folder, "LEEME.txt"), Readme(stamp), System.Text.Encoding.UTF8);

            // zip everything except the debug folders Unity marks "DoNotShip" / "ButDontShipItWithYourGame"
            string zip = Path.Combine(project, BuildsFolder, name + ".zip");
            if (File.Exists(zip)) File.Delete(zip);
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(folder.Length + 1);
                    if (rel.Contains("DoNotShip") || rel.Contains("DontShip")) continue;
                    archive.CreateEntryFromFile(file, Path.Combine(name, rel).Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                }
            long mb = new FileInfo(zip).Length / (1024 * 1024);
            Debug.Log("[BuildGame] OK: " + zip + " (" + mb + " MB)");
            EditorPrefs.SetString(Key + "Status", "done");
            EditorPrefs.SetString(Key + "Result", zip);
            return zip;
        }
        catch (Exception e)
        {
            return Fail(e.ToString());
        }
    }

    static string Fail(string why)
    {
        Debug.LogError("[BuildGame] " + why);
        EditorPrefs.SetString(Key + "Status", "failed");
        EditorPrefs.SetString(Key + "Result", why);
        return null;
    }

    static string Readme(string stamp) =>
        "ANIMAL ISLAND  (versión " + stamp + ")\r\n" +
        "==================================\r\n\r\n" +
        "Cómo jugar:\r\n" +
        "  1. Descomprime el .zip entero en una carpeta (no lo abras desde dentro del zip).\r\n" +
        "  2. Abre AnimalIsland.exe.\r\n" +
        "  3. Si Windows dice \"Windows protegió su PC\": pulsa \"Más información\" y luego \"Ejecutar de todas formas\"\r\n" +
        "     (sale porque el juego no está firmado, no es un virus).\r\n\r\n" +
        "Requisitos: Windows 10/11 de 64 bits. No hace falta instalar nada.\r\n" +
        "Mando: DualShock 4 (PS4) por USB o Bluetooth, o un mando de Xbox. También teclado y ratón.\r\n" +
        "Pantalla dividida: en el menú, PANTALLA DIVIDIDA (2-4): cada mando se une con X.\r\n\r\n" +
        "ONLINE (cada uno en su casa, hasta 4):\r\n" +
        "  - Todos tenéis que usar el MISMO .zip (la misma versión).\r\n" +
        "  - Uno elige JUGAR ONLINE > CREAR PARTIDA. Le sale un código de 6 letras (botón \"Copiar código\").\r\n" +
        "    Pásalo por WhatsApp / Discord.\r\n" +
        "  - Los demás: JUGAR ONLINE, escriben (o pegan con \"Pegar\") el código y pulsan UNIRSE.\r\n" +
        "  - En la sala cada uno elige su animal (◄ ►). El que creó la partida elige dificultad, modo e isla\r\n" +
        "    y pulsa ¡EMPEZAR! (o START / Enter).\r\n" +
        "  - Hace falta internet. No hay que abrir puertos ni instalar nada más. El PC del que crea la partida\r\n" +
        "    hace de servidor: que lo cree el que tenga mejor PC / conexión.\r\n" +
        "  - En online la pausa no para el juego (solo abre el menú en tu pantalla). Si el anfitrión se va,\r\n" +
        "    se acaba la partida para todos.\r\n" +
        "  - Red local / sin Relay: \"Crear partida por IP\" y los demás escriben esa IP en el campo del código.\r\n\r\n" +
        "Controles (mando): R2 disparar · L2 apuntar · □ usar / recargar · Triángulo cambiar arma · L1 habilidad\r\n" +
        "                   X saltar · L3 correr · R3 ukelele · OPTIONS pausa\r\n" +
        "Controles (teclado): WASD mover · ratón apuntar · clic izq. disparar · clic der. apuntar · F/R usar\r\n" +
        "                     Q cambiar arma · E habilidad · Espacio saltar · Shift correr · V ukelele · Esc pausa\r\n\r\n" +
        "Para quitarlo: borra la carpeta. Las partidas guardan un registro en\r\n" +
        "  %USERPROFILE%\\AppData\\LocalLow\\DefaultCompany\\animal_island1\\runs\r\n";
}
