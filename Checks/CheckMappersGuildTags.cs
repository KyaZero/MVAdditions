using MapsetParser.objects;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MVAdditions.Checks
{
    [Check]
    public class CheckMappersGuildTags : GeneralCheck
    {
        public override CheckMetadata GetMetadata() => new CheckMetadata()
        {
            Category = "Metadata",
            Message = "Mappers' Guild tags are present.",
            Author = "Zer0-",
            Documentation = new Dictionary<string, string>()
            {
                { "Purpose", "Make sure to remind you to check if bg is free to use." },
                {
                    "Reasoning",
                    "Sometimes you can get DQ'd for not having artist's permission, this is just to remind the modder to check this before qualification."
                }
            }
        };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>()
            {
                {
                    "MPG Tags",
                    new IssueTemplate(Issue.Level.Warning,
                        "Make sure the background is free to use, and that the set is on the Mappers' Guild website.")
                }
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            bool hasMpgTags = false;

            foreach (Beatmap map in beatmapSet.beatmaps)
            {
                if (map.metadataSettings.IsCoveredByTags("mpg") ||
                    map.metadataSettings.IsCoveredByTags("mappers guild") ||
                    map.metadataSettings.IsCoveredByTags("mappers' guild"))
                    hasMpgTags = true;
            }

            if (hasMpgTags)
                yield return new Issue(GetTemplate("MPG Tags"), null);
        }
    }
}