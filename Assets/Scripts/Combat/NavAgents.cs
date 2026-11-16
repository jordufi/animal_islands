using UnityEngine;
using UnityEngine.AI;

// NavMesh agent types used by the game. "Saltador" is a second NavMesh baked without the props
// (layer Prop), so the night old men walk over cars, tents and crates; walls, rocks and doors still block.
public static class NavAgents
{
    public const string JumperName = "Saltador";
    public const int PropLayer = 12;
    // La Serenisima: the canal beds are baked as area "Canal". Only walkable during the acqua alta (AcquaAlta sets
    // SwimOpen and the old men's area masks); every other time nothing samples or paths onto it.
    public const int CanalArea = 14;
    public static bool SwimOpen;
    public static int EnemyMask => SwimOpen ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << CanalArea);

    static int jumperId = int.MinValue;

    public static void ResetCache() => jumperId = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() { jumperId = int.MinValue; SwimOpen = false; }   // Enter Play Mode without domain reload keeps statics

    // Agent type id of "Saltador", or 0 (default humanoid) if the project has no such agent type.
    public static int JumperId
    {
        get
        {
            if (jumperId != int.MinValue) return jumperId;
            jumperId = 0;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == JumperName) { jumperId = id; break; }
            }
            return jumperId;
        }
    }

    public static NavMeshQueryFilter Filter(int agentTypeId) => new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = EnemyMask };

    // SamplePosition on the NavMesh of one agent type (the plain overload may snap onto any agent type's mesh).
    public static bool Sample(Vector3 p, float maxDistance, out Vector3 result, int agentTypeId = 0)
    {
        if (NavMesh.SamplePosition(p, out var hit, maxDistance, Filter(agentTypeId))) { result = hit.position; return true; }
        result = p;
        return false;
    }
}
