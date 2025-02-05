﻿using Mapperator.Matching;
using Mapperator.Model;
using Mapping_Tools_Core.BeatmapHelper.Enums;
using Mapping_Tools_Core.BeatmapHelper.HitObjects;
using Mapping_Tools_Core.BeatmapHelper.HitObjects.Objects;
using Mapping_Tools_Core.BeatmapHelper.IO.Decoding.HitObjects;
using Mapping_Tools_Core.BeatmapHelper.TimingStuff;
using Mapping_Tools_Core.MathUtil;
using Mapping_Tools_Core.ToolHelpers;

namespace Mapperator.Construction {
    public class BeatmapConstructor2 {
        private readonly HitObjectDecoder decoder;

        public BeatmapConstructor2() : this(new HitObjectDecoder()) { }

        public BeatmapConstructor2(HitObjectDecoder decoder) {
            this.decoder = decoder;
        }

        /// <summary>
        /// Returns the (position, angle, time) at the end of the hitobjects
        /// </summary>
        /// <param name="hitObjects"></param>
        /// <returns></returns>
        public static (Vector2, double, double) GetContinuation(IList<HitObject> hitObjects) {
            if (hitObjects.Count == 0)
                return ( new Vector2(256, 192), 0, 0);

            var lastPos = hitObjects[^1].EndPos;

            var beforeLastPos = new Vector2(256, 192);
            for (var i = hitObjects.Count - 1; i >= 0; i--) {
                var ho = hitObjects[i];

                if (Vector2.DistanceSquared(ho.EndPos, lastPos) > Precision.DOUBLE_EPSILON) {
                    beforeLastPos = ho.EndPos;
                    break;
                }

                if (Vector2.DistanceSquared(ho.Pos, lastPos) > Precision.DOUBLE_EPSILON) {
                    beforeLastPos = ho.Pos;
                    break;
                }
            }

            var angle = Vector2.DistanceSquared(beforeLastPos, lastPos) > Precision.DOUBLE_EPSILON
                ? (lastPos - beforeLastPos).Theta
                : 0;

            return (lastPos, angle, hitObjects[^1].EndTime);
        }

        /// <summary>
        /// Constructs the match onto the end of the list of hit objects.
        /// </summary>
        public void Construct(IList<HitObject> hitObjects, Match match, ReadOnlySpan<MapDataPoint> input, bool selectNewObjects, Timing? timing, out List<ControlChange> controlChanges) {
            var (pos, angle, time) = GetContinuation(hitObjects);
            MapDataPoint? lastDataPoint = null;
            controlChanges = new List<ControlChange>();
            var mult = match.MinMult == 0 && double.IsPositiveInfinity(match.MaxMult) ? 1 : Math.Sqrt(match.MinMult * match.MaxMult);

            for (var i = 0; i < match.Length; i++) {
                var dataPoint = match.Sequence.Span[i];

                var original = input[i];
                var originalHo = string.IsNullOrWhiteSpace(original.HitObject) ? null : decoder.Decode(original.HitObject);

                time = timing?.WalkBeatsInMillisecondTime(original.BeatsSince, time) ?? time + 1;
                angle += dataPoint.Angle;
                var dir = Vector2.Rotate(Vector2.UnitX, angle);
                pos += dataPoint.Spacing * mult * dir;
                // Wrap pos
                //pos = new Vector2(Helpers.Mod(pos.X, 512), Helpers.Mod(pos.Y, 384));
                pos = Vector2.Clamp(pos, Vector2.Zero, new Vector2(512, 382));

                //Console.WriteLine($"time = {time}, pos = {pos}, original = {original}, match = {match}");

                if (dataPoint.DataType == DataType.Release) {
                    if (lastDataPoint is { DataType: DataType.Hit }) {
                        // Make sure the last object is a slider of the release datapoint
                        if (hitObjects.LastOrDefault() is { } hitObject) {
                            hitObjects.RemoveAt(hitObjects.Count - 1);

                            var ho = decoder.Decode(dataPoint.HitObject);
                            if (ho is Slider) {
                                ho.StartTime = hitObject.StartTime;
                                ho.Move(hitObject.Pos - ho.Pos);
                            }
                            else {
                                ho = new Slider {
                                    Pos = hitObject.Pos,
                                    StartTime = hitObject.StartTime,
                                    SliderType = PathType.Linear,
                                    PixelLength = Vector2.Distance(hitObject.Pos, pos),
                                    CurvePoints = { pos }
                                };
                            }

                            if (original.Repeats.HasValue) {
                                ((Slider)ho).RepeatCount = original.Repeats.Value;
                            }

                            if (originalHo is not null) {
                                ho.ResetHitsounds();
                                ho.Hitsounds = originalHo.Hitsounds;
                                if (ho is Slider slider && originalHo is Slider slider2) {
                                    slider.EdgeHitsounds = slider2.EdgeHitsounds;
                                }
                            }

                            ho.NewCombo = hitObject.NewCombo;
                            ho.IsSelected = selectNewObjects;
                            hitObjects.Add(ho);
                        }

                        // Make sure the last object ends at time t and around pos
                        if (hitObjects.LastOrDefault() is Slider lastSlider) {
                            // Rotate and scale the end towards the release pos
                            lastSlider.RecalculateEndPosition();
                            var ogPos = lastSlider.Pos;
                            var ogTheta = (lastSlider.EndPos - ogPos).Theta;
                            var newTheta = (pos - ogPos).Theta;
                            var ogSize = (lastSlider.EndPos - ogPos).Length;
                            var newSize = (pos - ogPos).Length;
                            var scale = newSize / ogSize;

                            if (!double.IsNaN(ogTheta) && !double.IsNaN(newTheta)) {
                                lastSlider.Transform(Matrix2.CreateRotation(ogTheta - newTheta));
                                lastSlider.Transform(Matrix2.CreateScale(scale));
                                lastSlider.Move(ogPos - lastSlider.Pos);
                                lastSlider.PixelLength *= scale;
                            }

                            // Add the right number of repeats
                            if (original.Repeats.HasValue) {
                                lastSlider.RepeatCount = original.Repeats.Value;
                            }

                            if (timing is not null) {
                                // Adjust SV
                                var tp = timing.GetTimingPointAtTime(lastSlider.StartTime).Copy();
                                var mpb = timing.GetMpBAtTime(lastSlider.StartTime);
                                tp.Offset = lastSlider.StartTime;
                                tp.Uninherited = false;
                                tp.SetSliderVelocity(lastSlider.PixelLength / ((time - lastSlider.StartTime) / mpb *
                                                                               100 * timing.GlobalSliderMultiplier));
                                controlChanges.Add(new ControlChange(tp, true));
                            }
                        }
                    } else if (lastDataPoint is { DataType: DataType.Spin }) {
                        // Make sure the last object is a spinner
                        if (hitObjects.LastOrDefault() is HitCircle lastCircle) {
                            hitObjects.RemoveAt(hitObjects.Count - 1);
                            var spinner = new Spinner {
                                Pos = new Vector2(256, 196),
                                StartTime = lastCircle.StartTime,
                                Hitsounds = lastCircle.Hitsounds,
                                IsSelected = selectNewObjects
                            };
                            spinner.SetEndTime(time);
                            hitObjects.Add(spinner);
                        }

                        // Make sure the last object ends at time t
                        if (hitObjects.LastOrDefault() is Spinner lastSpinner) {
                            lastSpinner.SetEndTime(time);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(dataPoint.HitObject) && dataPoint.DataType != DataType.Release) {
                    var ho = decoder.Decode(dataPoint.HitObject);

                    if (ho is Slider) {
                        ho = new HitCircle();
                    }

                    ho.IsSelected = selectNewObjects;
                    ho.StartTime = time;
                    ho.NewCombo = original.NewCombo;
                    ho.Move(pos - ho.Pos);
                    ho.ResetHitsounds();

                    if (originalHo is not null) {
                        ho.Hitsounds = originalHo.Hitsounds;
                    }

                    hitObjects.Add(ho);
                }

                lastDataPoint = dataPoint;
            }
        }
    }
}