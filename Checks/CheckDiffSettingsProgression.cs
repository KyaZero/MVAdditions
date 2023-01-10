using System;
using MapsetParser.objects;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.metadata;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks.Sources;
using MapsetVerifierFramework.objects.attributes;

namespace MVAdditions.Checks
{
    [Check]
    public class CheckDiffSettingsProgression : BeatmapSetCheck
    {
        /// <summary> Determines which modes the check shows for, in which category the check appears, the message for the check, etc. </summary>
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata
        {
            Category = "Settings",
            Message = "Odd difficulty setting progression.",
            Author = "Zer0-",
            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    "Mentions when settings progression doesn't make sense within the spread, allowing you to check whether it's intentional or not."
                },
                {
                    "Reasoning",
                    @"Many times maps have been disqualified due to lower diffs having copied diff settings of the top diffs, and forgot to change them.
                    This aims to avoid this pitfall, by mentioning when diff setting deviate too much from the regular progression."
                }
            }
        };

        /// <summary> Returns a dictionary of issue templates, which determine how each sub-issue is formatted, the issue level, etc. </summary>
        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                {
                    "Warning",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0}: {1} -> {2} ({3} -> this). That's {4}, ensure this makes sense.",
                        "setting", "value", "value", "difficulty", "higher")
                },
                {
                    "Minor",
                    new IssueTemplate(Issue.Level.Minor,
                        "{0}: {1} -> {2} ({3} -> this). That's {4}, ensure this makes sense.",
                        "setting", "value", "value", "difficulty", "higher")
                }
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            var sortedList = new List<Beatmap>(beatmapSet.beatmaps);
            sortedList.Sort(delegate(Beatmap x, Beatmap y)
            {
                if (x.starRating > y.starRating)
                    return 1;
                else if (x.starRating < y.starRating)
                    return -1;
                return 0;
            });

            Beatmap previousDiff = null;
            foreach (Beatmap beatmap in sortedList)
            {
                if (previousDiff != null)
                {
                    var prevSettings = previousDiff.difficultySettings;
                    var nextSettings = beatmap.difficultySettings;

                    Issue issue = CompareSetting(prevSettings.approachRate, nextSettings.approachRate, "AR",
                        previousDiff, beatmap);
                    if (issue != null)
                        yield return issue;

                    issue = CompareSetting(prevSettings.hpDrain, nextSettings.hpDrain, "HP", previousDiff,
                        beatmap);
                    if (issue != null)
                        yield return issue;

                    issue = CompareSetting(prevSettings.overallDifficulty, nextSettings.overallDifficulty,
                        "OD",
                        previousDiff, beatmap);
                    if (issue != null)
                        yield return issue;
                }

                previousDiff = beatmap;
            }
        }

        private Issue CompareSetting(double prevValue, double currentValue, string settingName, Beatmap prevMap,
            Beatmap currentMap)
        {
            // Round to 1 decimal
            prevValue = Math.Round(prevValue, 1);
            currentValue = Math.Round(currentValue, 1);

            double difference = currentValue - prevValue;
            double amplitude = Math.Abs(difference);

            if (amplitude < 0.1)
                return null;

            // opposite progression e.g higher diff has lower settings
            if (difference < 0)
            {
                return new Issue(GetTemplate((amplitude >= 1) ? "Warning" : "Minor"), currentMap, settingName,
                    prevValue, currentValue, prevMap.metadataSettings.version,
                    (amplitude >= 2) ? "a lot lower for a higher diff" : "lower for a higher diff");
            }
            else
            {
                // Only a warning if the difference is really high (OD 7 -> OD 10) for example.
                return new Issue(GetTemplate((amplitude >= 3) ? "Warning" : "Minor"), currentMap, settingName,
                    prevValue, currentValue, prevMap.metadataSettings.version,
                    (amplitude >= 2) ? "a lot higher" : "higher");
            }
        }
    }
}