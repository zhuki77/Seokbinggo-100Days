using System;
using Nyangbingo.World;
using Nyangbingo.Data;

// CPU-only policy tests: use the actual queue source, with a data-only stand-in for ScriptableObject.
namespace Nyangbingo.Data
{
    public sealed class GuideMessageDefinition
    {
        public string Id { get; set; }
        public int Tier { get; set; }
        public string Repeat { get; set; }
    }
}
class Program
{
    static int checks;
    static GuideMessageDefinition Def(string id, int tier, string repeat = "while_active") =>
        new GuideMessageDefinition { Id = id, Tier = tier, Repeat = repeat };
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    static void Main()
    {
        var q = new GuidePriorityQueue(); q.Reset(6);
        var goal = Def("goal", 4); var door = Def("door", 3);
        var boss = Def("boss", 2); var hypo = Def("hypo", 1);
        q.SetState(goal, true, "goal"); q.SetState(door, true, "door");
        q.SetState(boss, true, "boss"); q.SetState(hypo, true, "hypo");
        Check(q.Select(false).Definition.Id == "hypo", "risk beats battle, building and goal");
        q.SetState(hypo, false, "");
        Check(q.Select(false).Definition.Id == "boss", "risk ending restores active battle");
        q.SetState(boss, false, "");
        Check(q.Select(false).Definition.Id == "door", "battle ending restores building");
        Check(q.Select(true) == null, "menu hides tiers 3 and 4");
        var placed = Def("placed", 3);
        q.SetState(placed, true, "stone");
        Check(q.Select(false).Definition.Id == "placed", "same tier selects most recent onset");
        q.SetState(door, true, "updated door text");
        Check(q.Select(false).Definition.Id == "placed", "polling does not change onset order");
        var success = Def("success", 3, "once_per_event");
        q.Fire(success, "success"); q.SetState(hypo, true, "cold");
        q.Advance(q.Select(false), 20);
        q.SetState(hypo, false, "");
        Check(q.Select(false).Definition.Id == "success" && q.Select(false).Remaining == 3,
            "suppressed event keeps its full display budget");
        var remaining = q.Select(false).Remaining;
        Check(q.Select(true) == null && q.Select(false).Remaining == remaining, "menu preserves pending event");
        q.Advance(q.Select(false), 3);
        Check(q.Select(false).Definition.Id == "placed", "shown event expires and restores prior state");
        q.Fire(success, "second event");
        Check(q.Select(false).Text == "second event", "new event can display again");
        var announcement = Def("announcement", 2, "once_per_day");
        q.SetState(announcement, true, "tonight"); q.Advance(q.Select(false), 3);
        GuidePriorityQueue.Entry badge = null;
        foreach (var entry in q.Entries) if (entry.Definition.Id == announcement.Id) badge = entry;
        Check(badge.Active && badge.Remaining == 0, "announcement remains active as compact badge");
        q.SetState(announcement, false, ""); q.SetState(announcement, true, "tonight");
        Check(q.Select(false).Definition.Id != announcement.Id, "same-day announcement is not replayed");
        var saved = q.CaptureConsumed();
        q.RestoreConsumed(6, saved); q.SetState(announcement, true, "tonight");
        Check(q.Select(false) == null, "continue preserves consumed daily announcement");
        q.BeginDay(7); q.SetState(announcement, true, "new day");
        Check(q.Select(false).Definition.Id == announcement.Id, "next day permits a new announcement");
        var dawn = Def("dawn", 3, "once_per_dawn");
        q.Reset(8); q.Fire(dawn, "kept"); q.Advance(q.Select(false), 3); q.Fire(dawn, "duplicate");
        Check(q.Select(false) == null, "same dawn does not replay a shown result");
        var bed = Def("bed", 3, "on_each"); q.Fire(bed, "cold bed"); q.Advance(q.Select(false), 3);
        q.Fire(bed, "retry"); Check(q.Select(false).Text == "retry", "each denied interaction can warn");
        q.Clear("bed"); Check(q.Select(false) == null, "successful action clears pending warning");
        q.Reset(9); q.SetState(goal, true, "goal"); q.SetState(hypo, true, "cold");
        Check(q.Select(true) == null && q.Select(false).Definition.Id == "hypo", "menu hides tier 1 status and closing restores it");
        q.Reset(9); Check(q.Select(false) == null, "restore clears transient messages");
        Console.WriteLine($"{checks} CPU policy checks passed.");
    }
}
