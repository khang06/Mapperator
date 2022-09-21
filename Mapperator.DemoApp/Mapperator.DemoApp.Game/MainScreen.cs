using System.Linq;
using Mapperator.DemoApp.Game.Drawables;
using Mapperator.Matching.DataStructures;
using Mapperator.Matching.Matchers;
using Mapping_Tools_Core.BeatmapHelper;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;

namespace Mapperator.DemoApp.Game
{
    public class MainScreen : Screen
    {
        private readonly Bindable<Beatmap> beatmap = new();
        private readonly BindableInt pos = new() { MinValue = 0 };
        private readonly int length = 5;
        private PatternVisualizer originalVisualizer;
        private PatternVisualizer newVisualizer;
        private RhythmDistanceTrieStructure dataStruct;
        private TrieDataMatcher2 matcher;

        [BackgroundDependencyLoader]
        private void load(BeatmapStore beatmapStore, MapDataStore mapDataStore)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = Color4.LightGray,
                    RelativeSizeAxes = Axes.Both,
                },
                new SpriteText
                {
                    Y = 20,
                    Text = "Main Screen",
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Font = FontUsage.Default.With(size: 40)
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    FillMode = FillMode.Stretch,
                    Position = new Vector2(0, 80),
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new BasicButton
                        {
                            Size = new Vector2(100, 50),
                            Margin = new MarginPadding(10),
                            Text = @"prev",
                            BackgroundColour = Color4.Blue,
                            HoverColour = Color4.CornflowerBlue,
                            FlashColour = Color4.MediumPurple,
                            Action = () => pos.Value--,
                        },
                        new BasicButton
                        {
                            Size = new Vector2(100, 50),
                            Margin = new MarginPadding(10),
                            Text = @"next",
                            BackgroundColour = Color4.Blue,
                            HoverColour = Color4.CornflowerBlue,
                            FlashColour = Color4.MediumPurple,
                            Action = () => pos.Value++,
                        }
                    }
                },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1184, 464),
                    Anchor = Anchor.TopLeft,
                    Position = new Vector2(0, 150),
                    Child = new FillFlowContainer
                    {
                        Direction = FillDirection.Horizontal,
                        FillMode = FillMode.Stretch,
                        Children = new Drawable[]
                        {
                            originalVisualizer = new PatternVisualizer { Margin = new MarginPadding(10) },
                            newVisualizer = new PatternVisualizer { Margin = new MarginPadding(10) }
                        }
                    }
                }
            };

            var data = mapDataStore.Get(@"test.txt");
            dataStruct = new RhythmDistanceTrieStructure();
            foreach (var map in data)
            {
                dataStruct.Add(map.ToArray());
            }

            pos.BindValueChanged(OnPosChange);
            beatmap.Value = beatmapStore.Get(@"input.osu");
            beatmap.BindValueChanged(OnBeatmapChange, true);
        }

        private void OnPosChange(ValueChangedEvent<int> obj)
        {
            var oldSpecialHo = beatmap.Value.HitObjects[obj.OldValue + length];
            oldSpecialHo.IsSelected = false;
            newVisualizer.HitObjects.Remove(oldSpecialHo);

            var newItems = beatmap.Value.HitObjects.GetRange(obj.NewValue, length);

            originalVisualizer.HitObjects.RemoveAll(o => !newItems.Contains(o));
            originalVisualizer.HitObjects.AddRange(newItems.Where(o => !originalVisualizer.HitObjects.Contains(o)));
            newVisualizer.HitObjects.RemoveAll(o => !newItems.Contains(o));
            newVisualizer.HitObjects.AddRange(newItems.Where(o => !newVisualizer.HitObjects.Contains(o)));

            var specialHo = beatmap.Value.HitObjects[obj.NewValue + length];
            specialHo.IsSelected = true;
            newVisualizer.HitObjects.Remove(specialHo);
            newVisualizer.HitObjects.Add(specialHo);

        }

        private void OnBeatmapChange(ValueChangedEvent<Beatmap> obj)
        {
            obj.NewValue.CalculateEndPositions();
            originalVisualizer.HitObjects.Clear();
            originalVisualizer.HitObjects.AddRange(obj.NewValue.HitObjects.GetRange(pos.Value, length));
            newVisualizer.HitObjects.Clear();
            newVisualizer.HitObjects.AddRange(obj.NewValue.HitObjects.GetRange(pos.Value, length));

            pos.MaxValue = obj.NewValue.HitObjects.Count - length - 1;

            var pattern = new DataExtractor().ExtractBeatmapData(obj.NewValue).ToArray();
            matcher = new TrieDataMatcher2(dataStruct, pattern);
        }
    }
}
