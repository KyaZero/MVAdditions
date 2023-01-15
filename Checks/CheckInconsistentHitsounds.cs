using System;
using System.Collections.Generic;
using System.Linq;
using MapsetParser.objects;
using MapsetParser.objects.hitobjects;
using MapsetParser.statics;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;

namespace MVAdditions.Checks
{
    [Check]
    public class CheckInconsistentHitsounds : BeatmapSetCheck
    {
        struct HitSoundInfo
        {
            public bool IsValid { get; set; }
            public double Time { get; set; }
            public HitObject.HitSound HitSound { get; set; }
            public Beatmap.Sampleset Addition { get; set; }
            public Beatmap.Sampleset Sampleset { get; set; }

            public Beatmap Beatmap { get; set; }
        };

        /// <summary> Determines which modes the check shows for, in which category the check appears, the message for the check, etc. </summary>
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata
        {
            Modes = new[]
            {
                Beatmap.Mode.Standard
            },
            Category = "Compose",
            Message = "Inconsistent hitsounds.",
            Author = "Zer0-",
            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    "Goes through the hitsounds and mentions out common points of failure."
                },
                {
                    "Reasoning",
                    @"This simplifies having to open the overview tab and compare several timelines to make sure the hitsounds were properly copied down. 
                     As well as finding some other hard to spot mistakes with hitsounds, such as sliderbody additions"
                }
            }
        };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                {
                    "MissingHitsound",
                    new IssueTemplate(Issue.Level.Warning,
                            "{0} is missing ({1}) which exists in {2}",
                            "timestamp -", "hitsound", "other difficulties")
                        .WithCause(
                            "Missing hitsounds when they are hitsounded in other diffs is most likely a mistake that should be fixed.")
                },
                {
                    "MissingHitsoundMinor",
                    new IssueTemplate(Issue.Level.Minor,
                            "{0} is missing ({1}) which exists in {2}",
                            "timestamp -", "hitsound", "other difficulties")
                        .WithCause(
                            "Same as the warning but more likely to be due to separate hs per diff.")
                },
                {
                    "SliderBody",
                    new IssueTemplate(Issue.Level.Warning,
                            "{0} This sliderbody has additions, ensure this is intentional.",
                            "timestamp -")
                        .WithCause(
                            "Most of the time sliderbody hitsounds are a mistake, and can be hard to spot.")
                },
                {
                    "UniqueHitsound",
                    new IssueTemplate(Issue.Level.Warning,
                            "This difficulty appears to have it's own hitsounding, make sure it makes sense.")
                        .WithCause(
                            @"Sometimes GD's have their own hitsounding due to very different rhythm, or taking it from another set. 
                            This means we cannot check for inconsistencies correctly.")
                }
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            var (maps, uniqueMaps) = GetCleanedListOfMaps(beatmapSet);
            var cutoff = GetReasonableCutoffForNumMaps(beatmapSet);

            foreach (var beatmap in uniqueMaps)
                yield return new Issue(GetTemplate("UniqueHitsound"), beatmap);

            foreach (Beatmap beatmap in maps)
            {
                foreach (HitObject obj in beatmap.hitObjects)
                {
                    HitSoundInfo objInfo = GetHitsoundInfoFromTime(obj.time, beatmap);
                    var issues = CompareHitObjectWithOtherMaps(objInfo, maps, cutoff);

                    // Check sliderend hitsounds as well.
                    if (obj is Slider asSlider)
                    {
                        HitSoundInfo sliderEndInfo = GetHitsoundInfoFromTime(asSlider.GetEndTime(), beatmap);
                        var additionalIssues = CompareHitObjectWithOtherMaps(sliderEndInfo, maps, cutoff);
                        foreach (Issue issue in additionalIssues)
                            issues.Add(issue);
                    }

                    foreach (Issue issue in issues)
                        yield return issue;
                }
            }

            // Check for sliderbody hitsounds
            foreach (Beatmap beatmap in beatmapSet.beatmaps)
            {
                foreach (HitObject obj in beatmap.hitObjects)
                    if (obj is Slider asSlider && asSlider.hitSound != HitObject.HitSound.None)
                        yield return new Issue(GetTemplate("SliderBody"), beatmap, Timestamp.Get(obj));
            }
        }

        private int GetReasonableCutoffForNumMaps(BeatmapSet beatmapSet)
        {
            if (beatmapSet.beatmaps.Count > 2)
            {
                return (beatmapSet.beatmaps.Count / 2) - 1;
            }

            return 0;
        }

        private List<Issue> CompareHitObjectWithOtherMaps(HitSoundInfo objInfo, List<Beatmap> mapsToCompare,
            int mapsCutoff)
        {
            List<string> missingHitsounds = new List<string>();
            List<string> maps = new List<string>();

            List<Issue> issues = new List<Issue>();
            foreach (Beatmap otherBeatmap in mapsToCompare)
            {
                if (otherBeatmap == objInfo.Beatmap)
                    continue;

                HitSoundInfo other = GetHitsoundInfoFromTime(objInfo.Time, otherBeatmap);
                if (!other.IsValid || !objInfo.IsValid)
                    continue;

                List<string> missing = DetermineMissingHitsound(objInfo.HitSound, other.HitSound);
                if (missing.Count > 0)
                {
                    foreach (string s in missing)
                        missingHitsounds.Add(s);

                    maps.Add(otherBeatmap.metadataSettings.version);
                }
            }

            if (missingHitsounds.Count > 0)
            {
                issues.Add(new Issue(
                    GetTemplate((maps.Count > mapsCutoff) ? "MissingHitsound" : "MissingHitsoundMinor"),
                    objInfo.Beatmap, Timestamp.Get(objInfo.Time), string.Join(", ", missingHitsounds.Distinct()),
                    string.Join(", ", maps.Distinct())));
                missingHitsounds.Clear();
                maps.Clear();
            }

            return issues;
        }

        // This will return the hitsound info from whatever is on the current timestamp
        private HitSoundInfo GetHitsoundInfoFromTime(double time, Beatmap beatmap)
        {
            HitSoundInfo info = new HitSoundInfo();
            info.IsValid = false;
            info.Beatmap = beatmap;

            HitObject obj = beatmap.GetHitObject(time);

            // Simplest case, just return the data.
            if (obj is Circle && Math.Abs(obj.time - time) < 1)
            {
                info.IsValid = true;

                info.Time = Math.Floor(obj.time);
                info.Addition = obj.addition;
                info.HitSound = obj.hitSound;
                info.Sampleset = obj.sampleset;
                return info;
            }

            // Gotta check if time is closer to sliderend or head (dont care about repeats)
            if (obj is Slider)
            {
                Slider objSlider = obj as Slider;

                // Sliderhead hitsound
                if (Math.Abs(objSlider.time - time) < 1)
                {
                    info.IsValid = true;

                    info.Time = Math.Floor(objSlider.time);
                    info.Addition = objSlider.startAddition;
                    info.HitSound = objSlider.startHitSound;
                    info.Sampleset = objSlider.startSampleset;
                    return info;
                }

                // Sliderend hitsound
                if (Math.Abs(objSlider.endTime - time) < 1)
                {
                    info.IsValid = true;

                    info.Time = Math.Floor(objSlider.endTime);
                    info.Addition = objSlider.endAddition;
                    info.HitSound = objSlider.endHitSound;
                    info.Sampleset = objSlider.endSampleset;
                    return info;
                }
            }

            return info;
        }

        private int GetHitsoundInconsistencies(Beatmap beatmap, BeatmapSet beatmapSet)
        {
            int num = 0;

            foreach (HitObject obj in beatmap.hitObjects)
            {
                var info = GetHitsoundInfoFromTime(obj.time, beatmap);
                foreach (Beatmap mapToCompare in beatmapSet.beatmaps)
                {
                    if (mapToCompare == beatmap)
                        continue;

                    var otherInfo = GetHitsoundInfoFromTime(info.Time, mapToCompare);
                    if (!otherInfo.IsValid)
                        continue;

                    if (!info.HitSound.Equals(otherInfo.HitSound) || !info.Addition.Equals(otherInfo.Addition) ||
                        !info.Sampleset.Equals(otherInfo.Sampleset))
                        num++;
                }
            }

            num /= beatmapSet.beatmaps.Count;

            return num;
        }

        // Performs a pre-pass on the maps, to check if some diff has far too many inconsistencies
        // which most likely means it uses differing hitsounding from the rest of the set and should
        // be discarded early, so as to not mention too many issues.
        private (List<Beatmap>, List<Beatmap>) GetCleanedListOfMaps(BeatmapSet beatmapSet)
        {
            List<Beatmap> mapsToReturn = new List<Beatmap>();
            List<Beatmap> uniquelyHitsoundedMaps = new List<Beatmap>();

            Dictionary<Beatmap, int> inconsistencyValues = new Dictionary<Beatmap, int>();
            // Pre pass to gather inconsistency values
            foreach (Beatmap beatmap in beatmapSet.beatmaps)
                inconsistencyValues[beatmap] = GetHitsoundInconsistencies(beatmap, beatmapSet);

            int minInconsistency = inconsistencyValues.Min(kv => kv.Value);
            double avgInconsistency = inconsistencyValues.Values.Average();

            foreach (Beatmap beatmap in beatmapSet.beatmaps)
            {
                int inconsistencies = Math.Max(inconsistencyValues[beatmap] - minInconsistency, 0);

                // Kind of arbitrary cutoff, but seems to work with most sets
                // I intentionally don't want to flag _potential_ maps,
                // since then it won't point out actually missing hitsounds
                if (inconsistencies > avgInconsistency && inconsistencies > beatmap.hitObjects.Count / 4)
                {
                    uniquelyHitsoundedMaps.Add(beatmap);
                    continue;
                }

                mapsToReturn.Add(beatmap);
            }

            return (mapsToReturn, uniquelyHitsoundedMaps);
        }

        private List<string> DetermineMissingHitsound(HitObject.HitSound original, HitObject.HitSound toCompare)
        {
            List<string> missingHitsounds = new List<string>();

            if (!original.HasFlag(HitObject.HitSound.Whistle) && toCompare.HasFlag(HitObject.HitSound.Whistle))
            {
                missingHitsounds.Add("Whistle");
            }

            if (!original.HasFlag(HitObject.HitSound.Clap) && toCompare.HasFlag(HitObject.HitSound.Clap))
            {
                missingHitsounds.Add("Clap");
            }

            if (!original.HasFlag(HitObject.HitSound.Finish) && toCompare.HasFlag(HitObject.HitSound.Finish))
            {
                missingHitsounds.Add("Finish");
            }

            return missingHitsounds;
        }
    }
}