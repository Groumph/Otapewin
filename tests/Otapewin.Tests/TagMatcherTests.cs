using Otapewin;
using Otapewin.Helpers;
using Xunit;

namespace SecondBrain.Tests;

public class TagMatcherTests
{
    [Fact]
    public void Hashtags_ReturnsLowercaseHash()
    {
        var tag = new TagConfig { Name = "Sleep" };
        var hashtags = TagMatcher.Hashtags(tag).ToList();

        Assert.Single(hashtags);
        Assert.Equal("#sleep", hashtags[0]);
    }

    [Fact]
    public void ExtractTaggedSections_AssignsLinesToMatchingTag()
    {
        var tags = new Dictionary<string, List<string>>
        {
            { "sleep", new List<string>() },
            { "task", new List<string>() }
        };

        var lines = new List<string>
        {
            "Went to bed early #sleep",
            "- [ ] Do laundry #task",
            "No tag here"
        };

        var result = TagMatcher.ExtractTaggedSections(tags, lines);

        Assert.Single(result["sleep"]);
        Assert.Contains("Went to bed early #sleep".Trim(), result["sleep"]);
        Assert.Single(result["task"]);
        Assert.Contains("- [ ] Do laundry #task".Trim(), result["task"]);
    }
}
