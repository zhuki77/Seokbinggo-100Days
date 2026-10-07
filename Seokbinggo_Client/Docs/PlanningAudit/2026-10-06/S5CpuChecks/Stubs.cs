namespace Nyangbingo.Data
{
    public class ItemDefinition { public string Id; }
    public class ModuleDefinition { public string Id; public ItemDefinition Item; }
    public class GlobalDefinition
    {
        public string Value;
        public bool TryGetInt(out int value) => int.TryParse(Value, out value);
    }
    public class GameDataCatalog
    {
        public IReadOnlyList<ModuleDefinition> Modules;
        public Dictionary<string, GlobalDefinition> Globals = new();
        public GlobalDefinition FindGlobal(string id) => Globals.GetValueOrDefault(id);
    }
}
namespace Nyangbingo.Save
{
    public struct BossRecord { public string bossId; public int count; }
    public class GoalProgressRecord { public List<string> completedGoalIds = new(); }
    public class SaveGame
    {
        public List<string> modulesDone = new();
        public List<BossRecord> bossRecords = new();
        public GoalProgressRecord goalProgress = new();
        public bool storageSuccess, demoComplete;
    }
}
namespace Nyangbingo.World
{
    public static class UtilityTurretRules
    {
        public const string ScarecrowId = "scarecrow", IceTrapId = "ice_trap";
        public static bool IsUtilityTurretId(string id) => false;
    }
    public static class DamageTurretRules { public static bool IsDamageTurretId(string id) => false; }
}
