using UnityEngine;

// Numbers of the "Anselmo cagón" mode (Assets/Data/AnselmoCagon.asset). In that mode every old man gets,
// when he spawns, a fixed coin flip (chance): if true he may take a shortcut through the spawn entrances
// (into a WC / crack / bus in one zone, out of another entrance in another zone) whenever that is faster
// than walking to the player; if false he just chases like in the classic mode.
// Only one old man at a time can use an entrance (in or out): the others wait their turn.
[CreateAssetMenu(menuName = "Island/Tunnel Data (Anselmo cagón)")]
public class TunnelData : ScriptableObject
{
    [Range(0f, 1f)] public float chance = 0.5f;
    [Tooltip("Seconds inside the 'pipes' between going in and the door of the exit starting to rattle.")]
    public float travelTime = 3f;
    [Tooltip("Only take the shortcut if it saves at least this many metres of walking.")]
    public float minSaving = 12f;
    [Tooltip("Seconds between route re-checks (each old man, staggered).")]
    public float replanInterval = 1.5f;
    [Tooltip("Candidate entrances checked by walking distance: the nearest N to the old man and to the player.")]
    public int candidates = 4;
    [Tooltip("Seconds to go in (climb the barrier back, walk inside, door closes).")]
    public float enterClimbTime = 0.6f;
    public float enterWalkTime = 0.45f;
}
