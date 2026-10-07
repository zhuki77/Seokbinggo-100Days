using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.World;
using System.Text.Json;

var root = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
var moduleRows = File.ReadAllLines(Path.Combine(root, "Assets/Data/CSV/modules.csv"))
    .Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split(',')).ToArray();
var catalog = new GameDataCatalog
{
    Modules = moduleRows.Select(row => new ModuleDefinition
    { Id = row[0], Item = string.IsNullOrEmpty(row[2]) ? null : new ItemDefinition { Id = row[2] } }).ToArray()
};
foreach (var id in new[] { "win_modules", "win_gate_demo", "demo_complete_rule" })
{
    var row = File.ReadAllLines(Path.Combine(root, "Assets/Data/CSV/globals.csv"))
        .First(line => line.StartsWith(id + ",", StringComparison.Ordinal)).Split(',');
    catalog.Globals[id] = new GlobalDefinition { Value = row[1] };
}
int checks = 0;
void Check(bool condition, string message)
{ checks++; if (!condition) throw new Exception(message); }
var core = DemoAchievementRules.CoreModules(catalog).Select(m => m.Id).ToArray();
Check(core.Length == 5 && catalog.Modules.Count == 11 &&
    DemoAchievementRules.RequiredCoreModuleCount(catalog) == 5, "Five core rows out of eleven");
foreach (var defeated in new[] { false, true })
foreach (var n in Enumerable.Range(0, 6))
{
    var save = new SaveGame { modulesDone = core.Take(n).ToList() };
    save.modulesDone.AddRange(new[] { "seokbinggo_s3", "unknown", "seokbinggo_s6" });
    if (n > 0) save.modulesDone.Add(core[0]);
    if (defeated) save.bossRecords.Add(new BossRecord { bossId = "imugi_boss", count = 1 });
    DemoAchievementRules.UpdateSavedAchievements(save, catalog);
    Check(DemoAchievementRules.InstalledCoreModuleIds(save.modulesDone, catalog).Count == n,
        $"Current core count excludes duplicates, stages, unknown IDs ({n})");
    Check(save.demoComplete == (defeated && n == 5), $"Completion boss={defeated}, modules={n}");
}
var later = new SaveGame
{
    modulesDone = core.Take(3).ToList(),
    bossRecords = new() { new() { bossId = "imugi_boss", count = 1 } }
};
DemoAchievementRules.UpdateSavedAchievements(later, catalog);
Check(!later.demoComplete && !later.storageSuccess, "Boss-only result stays incomplete");
later.modulesDone = core.ToList();
later.goalProgress.completedGoalIds.Add("g09");
DemoAchievementRules.UpdateSavedAchievements(later, catalog);
Check(later.demoComplete && later.storageSuccess, "Later build and acknowledged storage update achievements");
var options = new JsonSerializerOptions { IncludeFields = true };
var restored = JsonSerializer.Deserialize<SaveGame>(JsonSerializer.Serialize(later, options), options)!;
restored.modulesDone.Clear();
DemoAchievementRules.UpdateSavedAchievements(restored, catalog);
Check(restored.demoComplete && restored.storageSuccess &&
    DemoAchievementRules.InstalledCoreModuleIds(restored.modulesDone, catalog).Count == 0,
    "Saved achievements persist separately from current construction");
var oldSave = new SaveGame { modulesDone = core.ToList(), bossRecords = later.bossRecords };
oldSave.goalProgress.completedGoalIds.Add("g09");
DemoAchievementRules.UpdateSavedAchievements(oldSave, catalog);
Check(oldSave.demoComplete && oldSave.storageSuccess, "Old goal/boss records derive optional new achievements");
catalog.Globals["win_modules"].Value = "11";
var rejected = false;
try { DemoAchievementRules.RequiredCoreModuleCount(catalog); } catch (InvalidOperationException) { rejected = true; }
Check(rejected, "Data mismatch must never display eleven as core denominator");
Console.WriteLine($"S5 CPU policy checks: {checks}/{checks} passed. No Unity runtime executed.");
