using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Overlays.PvP;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DateTime = System.DateTime;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for MiniMapView.xaml
    /// </summary>
    public partial class MiniMapView : UserControl
    {
        private MapInfo _currentMapInfo;
        private List<OpponentMapIcon> opponentImages => new List<OpponentMapIcon> { Op1, Op2, Op3, Op4, Op5, Op6, Op7, Op8, Op9, Op10, Op11, Op12, Op13, Op14, Op15, Op16 };
        public MiniMapView(MiniMapViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            HideAllOpponents();
        }
       

        private void UpdateIconPosition(double xFraction, double yFraction, double facing, OpponentMapInfo opponent, int opponentIndex)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var icon = opponent.IsLocalPlayer ? CharImage : opponentImages[opponentIndex];
                icon.Icon.Source = GetImageFromMenaceType(opponent.IsEnemy, opponent.IsTarget, opponent.IsLocalPlayer);
                icon.SelectionAdornment.IsVisible = opponent.IsTarget;

                //icon.PlayerName.Text = opponent.Name;
                icon.IsVisible = true;

                var imageLocation = GetBoundingBox(Arena, ImageCanvas);

                var characterXposOverlay = imageLocation.Width * xFraction;
                var characterYposOverlay = imageLocation.Height * yFraction;

                icon.Opacity = opponent.IsCurrentInfo ? 1 : 0.25;
                Canvas.SetLeft(icon, characterXposOverlay - (icon.Width / 2));
                Canvas.SetTop(icon, characterYposOverlay - (icon.Height / 2));

                var rotationTransform = new RotateTransform(facing * -1, 0, 0);
                icon.Icon.RenderTransform = rotationTransform;
            });
        }
        internal void AddOpponents(List<OpponentMapInfo> opponentInfos, DateTime startTime)
        {
            if (CombatIdentifier.CurrentCombat == null)
                return;
            var currentMap = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(startTime);
            _currentMapInfo = currentMap.MapInfo;
            var roomTop = _currentMapInfo.MinY;
            var roomLeft = _currentMapInfo.MinX;
            var roomWidth = _currentMapInfo.MaxX - _currentMapInfo.MinX;
            var roomHeight = _currentMapInfo.MaxY - _currentMapInfo.MinY;

            var opponentIndex = 0;
            HideAllOpponents();
            foreach (var opponent in opponentInfos.Where(o => o.IsEnemy != EnemyState.Friend && !o.IsLocalPlayer))
            {
                var xFraction = (opponent.Position.X - roomLeft) / roomWidth;
                var yFraction = (opponent.Position.Y - roomTop) / roomHeight;
                UpdateIconPosition(xFraction, yFraction, opponent.Position.Facing, opponent, opponentIndex);
                opponentIndex++;
            }
        }

        private Bitmap GetImageFromMenaceType(EnemyState isEnemy, bool isTaget, bool isLocalPlayer)
        {
            if (isLocalPlayer)
                return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/PlayerLocation.png")));
            if (isEnemy == EnemyState.Enemy)
            {
                return isTaget
                    ? new Bitmap(
                        AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/TargetedEnemyLocation.png")))
                    : new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/EnemyLocation.png")));
            }
            return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/UnknownPlayerLocation.png")));
        }

        private void HideAllOpponents()
        {
            CharImage.IsVisible = false;
            opponentImages.ForEach(o => o.IsVisible = false);
        }
        private static Rect GetBoundingBox(Control child, Control parent)
        {
            var transform = child.TransformToVisual(parent);
            var topLeft = transform.Value.Transform(new Point(0, 0));
            var bottomRight = transform.Value.Transform(new Point(child.Bounds.Width, child.Bounds.Height));
            return new Rect(topLeft, bottomRight);
        }
    }
}
