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
                    "Mentions when there's a note without any hitsound, where it's hitsounded in another diff."
                },
                {
                    "Reasoning",
                    @"This simplifies having to open the overview tab and compare several timelines to make sure the hitsounds were properly copied down."
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
                }
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            foreach (Beatmap beatmap in beatmapSet.beatmaps)
            {
                foreach (HitObject obj in beatmap.hitObjects)
                {
                    var issues = CompareHitObjectWithOtherMaps(obj, beatmap,
                        beatmapSet);
                    foreach (Issue issue in issues)
                        yield return issue;
                }
            }
        }

        private List<Issue> CompareHitObjectWithOtherMaps(HitObject hitObject, Beatmap currentMap,
            BeatmapSet beatmapSet)
        {
            HitSoundInfo objInfo = GetHitsoundInfoFromTime(hitObject.time, currentMap);

            List<string> missingHitsounds = new List<string>();
            List<string> maps = new List<string>();

            List<Issue> issues = new List<Issue>();
            foreach (Beatmap otherBeatmap in beatmapSet.beatmaps)
            {
                if (otherBeatmap == currentMap)
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

            // check the sliderend hitsound as well
            if (hitObject is Slider slider)
            {
                objInfo = GetHitsoundInfoFromTime(slider.GetEndTime(), currentMap);
                foreach (Beatmap otherBeatmap in beatmapSet.beatmaps)
                {
                    if (otherBeatmap == currentMap)
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
            }

            if (missingHitsounds.Count > 0 && maps.Count > 1)
            {
                issues.Add(new Issue(GetTemplate("MissingHitsound"), currentMap, Timestamp.Get(objInfo.Time), string.Join(", ", missingHitsounds.Distinct()), string.Join(", ", maps.Distinct())));
            }

            return issues;
        }

        // This will return the hitsound info from whatever is on the current timestamp
        private HitSoundInfo GetHitsoundInfoFromTime(double time, Beatmap beatmap)
        {
            HitSoundInfo info = new HitSoundInfo();
            info.IsValid = false;

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