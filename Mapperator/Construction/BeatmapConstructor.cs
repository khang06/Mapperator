﻿using System.Diagnostics.CodeAnalysis;
using Mapperator.Matching;
using Mapperator.Model;
using Mapping_Tools_Core.BeatmapHelper;
using Mapping_Tools_Core.BeatmapHelper.Enums;
using Mapping_Tools_Core.BeatmapHelper.HitObjects.Objects;
using Mapping_Tools_Core.BeatmapHelper.IO.Decoding.HitObjects;
using Mapping_Tools_Core.MathUtil;
using Mapping_Tools_Core.ToolHelpers;

namespace Mapperator.Construction {
    public class BeatmapConstructor : IBeatmapConstructor {
        private readonly HitObjectDecoder decoder;

        public BeatmapConstructor() : this(new HitObjectDecoder()) { }

        public BeatmapConstructor(HitObjectDecoder decoder) {
            this.decoder = decoder;
        }

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public void PopulateBeatmap(IBeatmap beatmap, IReadOnlyList<MapDataPoint> input, IDataMatcher matcher) {
            var i = 0;
            double time = 0;
            var pos = new Vector2(256, 196);
            double angle = 0;
            MapDataPoint? lastMatch = null;
            var controlChanges = new List<ControlChange>();
            foreach (var match in matcher.FindSimilarData(input, m => IsInBounds(m, pos, angle))) {
                var original = input[i++];
                var originalHo = string.IsNullOrWhiteSpace(original.HitObject) ? null : decoder.Decode(original.HitObject);

                time += beatmap.BeatmapTiming.GetMpBAtTime(time) * original.BeatsSince;
                angle += match.Angle;
                var dir = Vector2.Rotate(Vector2.UnitX, angle);
                pos += match.Spacing * dir;
                // Wrap pos
                //pos = new Vector2(Helpers.Mod(pos.X, 512), Helpers.Mod(pos.Y, 384));
                pos = Vector2.Clamp(pos, Vector2.Zero, new Vector2(512, 382));

                if (match.DataType == DataType.Release) {
                    beatmap.Editor.Bookmarks.Add(time);
                    if (lastMatch is { DataType: DataType.Hit }) {
                        // Make sure the last object is a slider
                        if (beatmap.HitObjects.LastOrDefault() is HitCircle lastCircle) {
                            beatmap.HitObjects.RemoveAt(beatmap.HitObjects.Count - 1);
                            var slider = new Slider {
                                Pos = lastCircle.Pos,
                                StartTime = lastCircle.StartTime,
                                SliderType = PathType.Linear,
                                PixelLength = Vector2.Distance(lastCircle.Pos, pos),
                                CurvePoints = { pos }
                            };
                            beatmap.HitObjects.Add(slider);
                        }

                        // Make sure the last object ends at time t and around pos
                        if (beatmap.HitObjects.LastOrDefault() is Slider lastSlider) {
                            // Adjust SV
                            var tp = beatmap.BeatmapTiming.GetTimingPointAtTime(lastSlider.StartTime).Copy();
                            var mpb = beatmap.BeatmapTiming.GetMpBAtTime(lastSlider.StartTime);
                            tp.Offset = lastSlider.StartTime;
                            tp.Uninherited = false;
                            tp.SetSliderVelocity(lastSlider.PixelLength / ((time - lastSlider.StartTime) / mpb * 100 *
                                                                           beatmap.Difficulty.SliderMultiplier));
                            controlChanges.Add(new ControlChange(tp, true));
                            // Rotate the end towards the release pos
                            lastSlider.RecalculateEndPosition();
                            var ogPos = lastSlider.Pos;
                            var ogTheta = (lastSlider.EndPos - ogPos).Theta;
                            var newTheta = (pos - ogPos).Theta;
                            if (!double.IsNaN(ogTheta) && !double.IsNaN(newTheta)) {
                                lastSlider.Transform(Matrix2.CreateRotation(ogTheta - newTheta));
                                lastSlider.Move(ogPos - lastSlider.Pos);
                            }
                            // Add the right number of repeats
                            if (original.Repeats.HasValue) {
                                lastSlider.RepeatCount = original.Repeats.Value;
                            }
                        }
                    } else if (lastMatch is { DataType: DataType.Spin }) {
                        // Make sure the last object is a spinner
                        if (beatmap.HitObjects.LastOrDefault() is HitCircle lastCircle) {
                            beatmap.HitObjects.RemoveAt(beatmap.HitObjects.Count - 1);
                            var spinner = new Spinner {
                                Pos = new Vector2(256, 196),
                                StartTime = lastCircle.StartTime,
                            };
                            spinner.SetEndTime(time);
                            beatmap.HitObjects.Add(spinner);
                        }

                        // Make sure the last object ends at time t
                        if (beatmap.HitObjects.LastOrDefault() is Spinner lastSpinner) {
                            lastSpinner.SetEndTime(time);
                        }
                    }
                }

                if (match.DataType == DataType.Hit) {
                    // If the last object is a slider and there is no release previously, then make sure the object is a circle
                    if (lastMatch is {DataType: DataType.Hit} && beatmap.HitObjects.LastOrDefault() is Slider lastSlider) {
                        beatmap.HitObjects.RemoveAt(beatmap.HitObjects.Count - 1);
                        beatmap.HitObjects.Add(new HitCircle { Pos = lastSlider.Pos, StartTime = lastSlider.StartTime });
                    }
                }

                if (!string.IsNullOrEmpty(match.HitObject) && match.DataType != DataType.Release) {
                    var ho = decoder.Decode(match.HitObject);
                    if (ho is Slider slider) {
                        slider.RepeatCount = 0;
                        if (original.Repeats.HasValue) {
                            slider.RepeatCount = original.Repeats.Value;
                        }
                    }
                    ho.StartTime = time;
                    ho.Move(pos - ho.Pos);
                    ho.ResetHitsounds();
                    if (originalHo is not null) {
                        ho.Hitsounds = originalHo.Hitsounds;
                    }
                    beatmap.HitObjects.Add(ho);
                }

                lastMatch = match;
            }
            ControlChange.ApplyChanges(beatmap.BeatmapTiming, controlChanges);
        }

        private static bool IsInBounds(MapDataPoint match, Vector2 pos, double angle) {
            angle += match.Angle;
            var dir = Vector2.Rotate(Vector2.UnitX, angle);
            pos += match.Spacing * dir;

            return PosInBounds(pos);
        }

        private static bool PosInBounds(Vector2 pos) {
            return pos.X is >= -5 and <= 517 && pos.Y is >= -5 and <= 387;
        }
    }
}