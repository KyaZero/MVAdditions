using System.Collections.Generic;
using MapsetParser.objects;
using MapsetParser.objects.hitobjects;
using MapsetParser.statics;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;

namespace MVAdditions.Checks
{
    [Check]
    public class CheckSliderOnlySections : BeatmapCheck
    {
        /// <summary> Determines which modes the check shows for, in which category the check appears, the message for the check, etc. </summary>
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata
        {
            Modes = new[]
            {
                Beatmap.Mode.Standard
            },
            Difficulties = new[]
            {
                Beatmap.Difficulty.Easy,
                Beatmap.Difficulty.Normal
            },
            Category = "Compose",
            Message = "Slider Only Section.",
            Author = "Zer0-",
            Documentation = new Dictionary<string, string>
            {
                { "Purpose", "To avoid slider only sections in Easy and Normal difficulties." },
                { "Reasoning", @"
                   Having a slider only section in lower difficulty can be tiring for new
                   players as they have to juggle clicking and holding, as opposed to only clicking." }
            }
        };

        /// <summary> Returns a dictionary of issue templates, which determine how each sub-issue is formatted, the issue level, etc. </summary>
        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                {
                    "Warning",
                    new IssueTemplate(Issue.Level.Warning, @"
                        {0} Section is slider only ({1} objects, spanning {2}s).
                        Ensure this includes plenty of time between objects.",
    "timestamp -", "num", "duration")
                        .WithCause("Slider-only sections should only exist given that there's enough time between objects to make sure the player has time to reset.")
                }
            };
        }

        public override IEnumerable<Issue> GetIssues(Beatmap beatmap)
        {
            var numSlidersInRow = 0;
            
            var lastSliderEnd = 0.0;
            var duration = 0.0;
            
            HitObject firstObject = null;
            
            foreach (HitObject hitObject in beatmap.hitObjects)
            {
                if (hitObject is Slider)
                {
                    if (firstObject == null)
                    {
                        firstObject = hitObject;
                    }

                    if (lastSliderEnd != 0)
                    {
                        duration += hitObject.time - lastSliderEnd;
                    }

                    lastSliderEnd = hitObject.GetEndTime();
                    duration += lastSliderEnd - hitObject.time;
                    
                    numSlidersInRow++;
                }
                else
                {
                    if (numSlidersInRow > 6 && duration > 5000)
                    {
                        yield return new Issue(GetTemplate("Warning"), beatmap, Timestamp.Get(firstObject),
                            numSlidersInRow, $"{(int)duration / 1000}");
                    }

                    lastSliderEnd = 0;
                    duration = 0;
                    firstObject = null;
                    numSlidersInRow = 0;
                }
            }
        }
    }
}