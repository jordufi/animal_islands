using UnityEngine;

// One playable map for the menu's map selector: the scene to load plus what the card shows.
[CreateAssetMenu(menuName = "Island/Map")]
public class MapData : ScriptableObject
{
    public string id = "map";
    public string displayName = "Mapa";
    public string sceneName = "";
    [TextArea] public string description = "";
    public Texture2D preview;
    [Tooltip("The guided tutorial (UI/Tutorial.cs) on this scene; solo only.")]
    public bool tutorial;
}
