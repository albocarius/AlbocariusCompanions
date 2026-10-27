using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 126 (M11 T8): the sample companion shows every delivery and polling kind, with zero warnings.
public class SampleTriggerContentTests
{
    static readonly string Root = Path.Combine(RepoPaths.Root, "module", "ModuleData", "Companions");

    [Fact]
    public void Sample_has_every_required_trigger_item_and_validates_clean()
    {
        var catalog = CompanionCatalog.Load(Root);
        Assert.Empty(catalog.Issues);
        var package = catalog.Registry.All.Single(p => p.Id == "test_companion");
        var engine = new TriggerEngine(new[] { package }, new AlbocariusCompanions.Core.Dialogs.SystemRandomSource(1));

        TriggeredContent One(string trigger, string delivery) =>
            Assert.Single(engine.ContentFor(trigger), c => c.Delivery == delivery);

        One("playerBattleEnded", DeliveryRules.Conversation);
        var daily = One("daily", DeliveryRules.Notification);
        Assert.True(daily.Spec.Chance is > 0 and < 1);
        var inquiry = One("daily", DeliveryRules.Inquiry);
        var options = inquiry.Conversation!.Nodes!.Single(n => n.Id == inquiry.Conversation.Start![0]).Next!;
        Assert.Equal(2, options.Count);
        One("nearSettlement", DeliveryRules.Notification);
        Assert.Equal("mood", (string)One("varCrossed", DeliveryRules.Notification).Spec.Params!["var"]!);

        var morale = package.Conversations.SelectMany(c => c.Nodes!).Select(n => n.Effects?.PartyMorale).Where(m => m != null).ToList();
        Assert.Contains(morale, m => m > 0);
        Assert.Contains(morale, m => m < 0);
    }
}
