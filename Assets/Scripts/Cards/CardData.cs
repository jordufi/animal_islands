using UnityEngine;

// Blessing + curse offered when a zone is opened. Both apply for the rest of the run.
[CreateAssetMenu(menuName = "Island/Card")]
public class CardData : ScriptableObject
{
    public string title = "Carta";
    [TextArea] public string blessingText;
    public StatMod[] blessing;
    [TextArea] public string curseText;
    public StatMod[] curse;
    public Color color = new Color(0.9f, 0.6f, 0.2f);
}
