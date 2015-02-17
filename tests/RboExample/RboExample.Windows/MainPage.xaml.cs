using Geo;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using RBO.Win2D;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace RboExample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private TrackPoint[] trackpoints;
        private double _progression;
        private readonly TimeSpan _totalDuration = TimeSpan.FromSeconds(120);
        private bool _holdProgression;
        private double? _capturedDesiredX;
        private Envelope _bounds;
        private Envelope _mapBounds;

        private IDictionary<TrackPoint, Vector2> _elevationPoints;
        private IDictionary<TrackPoint, Vector2> _speedPoints;
        private IDictionary<TrackPoint, Vector2> _candencePoints;

        private Rect _chartsRelativeBounds;
        private Rect _elevationRelativeBounds;
        private Rect _speedRelativeBounds;
        private Rect _candenceRelativeBounds;
        private Rect _mapRelativeBounds;

        public MainPage()
        {
            this.InitializeComponent();

            this.CanvasControl.Input.PointerPressed += Input_PointerPressed;
            this.CanvasControl.Input.PointerMoved += Input_PointerMoved;
            this.CanvasControl.Input.PointerReleased += Input_PointerReleased;
        }

        private void CanvasControl_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            var loadTask = Load();
            args.TrackAsyncAction(loadTask.AsAsyncAction());
        }

        private async Task Load()
        {
            _chartsRelativeBounds = new Rect(0, .4, 1, .6);
            _elevationRelativeBounds = new Rect(0, .4, 1, .2);
            _speedRelativeBounds = new Rect(0, .6, 1, .2);
            _candenceRelativeBounds = new Rect(0, .8, 1, .2);
            _mapRelativeBounds = new Rect(0, 0, 1, .4);

            trackpoints = (await TcxParser.LoadTcx("activity_368801578.tcx")).ToArray();

            _bounds = new CoordinateSequence(trackpoints.Select(tp => tp.Coordinate)).GetBounds();

            this.MapControl.SetView(new Bing.Maps.LocationRect(
                new Bing.Maps.Location(_bounds.MaxLat, _bounds.MinLon),
                new Bing.Maps.Location(_bounds.MinLat, _bounds.MaxLon)
                ), default(TimeSpan));

            var timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += timer_Tick;
            timer.Start();
        }

        void timer_Tick(object sender, object e)
        {
            var bounds = this.MapControl.Bounds;
            _mapBounds = new Envelope(bounds.South, bounds.West, bounds.North, bounds.East);
        }

        private void CanvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            DrawElevation(args.DrawingSession, sender.Size, _elevationRelativeBounds);
            DrawSpeed(args.DrawingSession, sender.Size, _speedRelativeBounds);
            DrawCandence(args.DrawingSession, sender.Size, _candenceRelativeBounds);
            DrawMap(args.DrawingSession, sender.Size, _mapRelativeBounds);

            DrawProgression(args.DrawingSession, sender.Size, _chartsRelativeBounds);
        }

        private void CanvasControl_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            if (_capturedDesiredX.HasValue)
            {
                var chartsAbsoluteDest = GetAbsoluteDestinationRect(this.CanvasControl.Size, _chartsRelativeBounds);
                _progression = (_capturedDesiredX.Value - chartsAbsoluteDest.Left) / chartsAbsoluteDest.Width;
            }

            if (_holdProgression)
                return;

            _progression += (double)args.Timing.ElapsedTime.Ticks / _totalDuration.Ticks;
        }

        private void DrawElevation(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.Green);
            var absoluteDestination = GetAbsoluteDestinationRect(size, destinationRelative);

            _elevationPoints = GetPolyLinePoints(absoluteDestination, tp => tp.Coordinate.Elevation, true);
            var geometry = CreatePathGeometry(drawingSession, _elevationPoints.Values, absoluteDestination, true);
            drawingSession.FillGeometry(geometry, foreground);
        }

        private void DrawSpeed(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.Blue);
            var absoluteDestination = GetAbsoluteDestinationRect(size, destinationRelative);

            _speedPoints = GetPolyLinePoints(absoluteDestination, tp => tp.Speed, true);
            var geometry = CreatePathGeometry(drawingSession, _speedPoints.Values, absoluteDestination, true);
            drawingSession.FillGeometry(geometry, foreground);
        }

        private void DrawCandence(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.Orange);
            var absoluteDestination = GetAbsoluteDestinationRect(size, destinationRelative);

            _candencePoints = GetPolyLinePoints(absoluteDestination, tp => tp.Cadence, true);
            var geometry = CreatePathGeometry(drawingSession, _candencePoints.Values, absoluteDestination, true);
            drawingSession.FillGeometry(geometry, foreground);
        }

        private void DrawProgression(CanvasDrawingSession drawingSession, Size size, Rect relativeDestination)
        {
            var absoluteDestination = GetAbsoluteDestinationRect(size, relativeDestination);

            DrawTextPosition(drawingSession, size, relativeDestination, _elevationPoints, tp => tp.Coordinate.Elevation.ToString("# m"));
            DrawTextPosition(drawingSession, size, relativeDestination, _speedPoints, tp => tp.Speed.ToString("# km/h"));
            DrawTextPosition(drawingSession, size, relativeDestination, _candencePoints, tp => tp.Cadence.ToString("0 tr/m"));

            var x = (float)(absoluteDestination.Left + _progression * absoluteDestination.Width);
            drawingSession.DrawLine(new Vector2(x, (float)absoluteDestination.Top), new Vector2(x, (float)absoluteDestination.Bottom), Colors.White, 2);

            // Distance
            var totalDistance = trackpoints[trackpoints.Length - 1].AccumulatedDistance.SiValue;
            var distanceKm = _progression * totalDistance / 1000;

            var whiteBrush = new CanvasSolidColorBrush(drawingSession, Colors.White);

            var position = new Vector2(x + 10, (float)absoluteDestination.Top);

            drawingSession.DrawText(distanceKm.ToString("#.## km"), position, whiteBrush, new CanvasTextFormat());
        }

        private void DrawMap(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (_mapBounds == null)
                return;

            var destinationAbsolute = GetAbsoluteDestinationRect(size, destinationRelative);

            // Tracé sur la carte 
            var mapGeometry = CreateMapGeometry(drawingSession, destinationAbsolute);

            var renderTarget = new CanvasRenderTarget(drawingSession, (float)destinationAbsolute.Width, (float)destinationAbsolute.Height);
            using (var session = renderTarget.CreateDrawingSession())
            {
                session.DrawGeometry(mapGeometry, Colors.Red, 2);
            }

            // Transformation pour coller à la carte
            var transformMatrix = GetMapTransformationMatrix(destinationAbsolute);
            var transformEffect = new Transform2DEffect()
            {
                Source = renderTarget,
                TransformMatrix = transformMatrix,
            };

            drawingSession.DrawImage(transformEffect, new Vector2(), destinationAbsolute);

            // Position sur la carte
            var totalDistance = trackpoints[trackpoints.Length - 1].AccumulatedDistance.SiValue;
            var targetDistance = _progression * totalDistance;
            var nearestTrackpoint = this.FindNearest(trackpoints, targetDistance);
            var centerCoordinates = GetMapPosition(nearestTrackpoint, destinationAbsolute);
            var transformedCoordinates = MultiplyMatrix(transformMatrix, centerCoordinates);
            drawingSession.FillCircle(transformedCoordinates, 10, Colors.Black);

        }

        private Vector2 MultiplyMatrix(Matrix3x2 m, Vector2 v)
        {
            return new Vector2(
                v.X * m.M11 + v.Y * m.M21 + m.M31,
                v.X * m.M12 + v.Y * m.M22 + m.M32
                );
        }

        private CanvasGeometry CreateMapGeometry(CanvasDrawingSession drawingSession, Rect destination)
        {
            var longitudeWidth = _bounds.MaxLon - _bounds.MinLon;
            var latitudeHeight = _bounds.MaxLat - _bounds.MinLat;

            var pathBuilder = new CanvasPathBuilder(drawingSession);

            bool first = true;

            foreach (var trackPoint in trackpoints)
            {
                var point = GetMapPosition(trackPoint, destination);

                if (first)
                {
                    pathBuilder.BeginFigure(point);
                    first = false;
                }
                else
                    pathBuilder.AddLine(point);
            }

            pathBuilder.EndFigure(CanvasFigureLoop.Open);

            var geometry = CanvasGeometry.CreatePath(pathBuilder);
            return geometry;
        }

        private Vector2 GetMapPosition(TrackPoint trackpoint, Rect destination)
        {
            var longitudeWidth = _bounds.MaxLon - _bounds.MinLon;
            var latitudeHeight = _bounds.MaxLat - _bounds.MinLat;

            var x = destination.Left + (trackpoint.Coordinate.Longitude - _bounds.MinLon) / longitudeWidth * destination.Width;
            var y = destination.Bottom - (trackpoint.Coordinate.Latitude - _bounds.MinLat) / latitudeHeight * destination.Height;

            var point = new Vector2((float)x, (float)y);
            return point;
        }

        private Matrix3x2 GetMapTransformationMatrix(Rect destination)
        {
            var srcRect = _bounds;
            var targetRect = _mapBounds;

            var xRatio = (_bounds.MaxLon - _bounds.MinLon) / (_mapBounds.MaxLon - _mapBounds.MinLon);
            var yRatio = (_bounds.MaxLat - _bounds.MinLat) / (_mapBounds.MaxLat - _mapBounds.MinLat);

            var longitudeOffsetPerDegree = destination.Width / (_mapBounds.MaxLon - _mapBounds.MinLon);
            var latitudeOffsetPerDegree = destination.Height / (_mapBounds.MaxLat - _mapBounds.MinLat);

            var xOffset = (_bounds.MinLon - _mapBounds.MinLon) * longitudeOffsetPerDegree;
            var yOffset = (_mapBounds.MaxLat - _bounds.MaxLat) * latitudeOffsetPerDegree;

            var matrix = Matrix3x2.CreateScale((float)xRatio, (float)yRatio) * Matrix3x2.CreateTranslation((float)xOffset, (float)yOffset);
            return matrix;
        }

        private Rect GetAbsoluteDestinationRect(Size size, Rect relative)
        {
            var absoluteDestination = new Rect(relative.Left * size.Width, relative.Top * size.Height,
                relative.Width * size.Width, relative.Height * size.Height);

            return absoluteDestination;
        }

        private IDictionary<TrackPoint, Vector2> GetPolyLinePoints(Rect destinationRect, Func<TrackPoint, double> valueGetter, bool close)
        {
            var points = new Dictionary<TrackPoint, Vector2>();

            var indexedValues = trackpoints.ToDictionary(tp => tp, tp => valueGetter(tp));
            var maxValue = indexedValues.Values.Max();

            float x = (float)destinationRect.X;
            float y = (float)destinationRect.Y;

            float maxHeight = (float)destinationRect.Height;

            var totalDistance = trackpoints[trackpoints.Length - 1].AccumulatedDistance;
            double widthPerUnit = destinationRect.Width / totalDistance.SiValue;

            foreach (var trackpoint in trackpoints)
            {
                var value = indexedValues[trackpoint];

                var valueRatio = value / maxValue;
                valueRatio = Math.Max(0, valueRatio);
                valueRatio = Math.Min(1, valueRatio);

                var barHeight = (float)(maxHeight * valueRatio);
                var barWidth = (float)(widthPerUnit * trackpoint.DistanceFromPrevious.SiValue);

                points[trackpoint] = new Vector2(x, y + maxHeight - barHeight);

                x += barWidth;
            }

            return points;
        }

        private CanvasGeometry CreatePathGeometry(CanvasDrawingSession drawingSession, IEnumerable<Vector2> points, Rect destinationRect, bool close)
        {
            var pathBuilder = new CanvasPathBuilder(drawingSession);
            pathBuilder.BeginFigure(points.First());

            foreach (var point in points.Skip(1))
                pathBuilder.AddLine(point);

            if (close)
            {
                pathBuilder.AddLine(new Vector2((float)destinationRect.Right, (float)destinationRect.Bottom));
                pathBuilder.AddLine(new Vector2((float)destinationRect.Left, (float)destinationRect.Bottom));
            }

            pathBuilder.EndFigure(close ? CanvasFigureLoop.Closed : CanvasFigureLoop.Open);

            var geometry = CanvasGeometry.CreatePath(pathBuilder);
            return geometry;
        }

        private void DrawTextPosition(CanvasDrawingSession drawingSession, Size size, Rect relativeDestination, IDictionary<TrackPoint, Vector2> points, Func<TrackPoint, string> textGetter)
        {
            var dest = GetAbsoluteDestinationRect(size, relativeDestination);
            var x = dest.Left + _progression * dest.Width;
            var nearestPoint = FindNearest(points, (float)x);

            var trackPoint = nearestPoint.Key;
            var pointPosition = nearestPoint.Value;

            var whiteBrush = new CanvasSolidColorBrush(drawingSession, Colors.White);
            drawingSession.FillCircle(pointPosition, 3, whiteBrush);

            var textPosition = new Vector2(pointPosition.X + 10, pointPosition.Y - 10);

            drawingSession.DrawText(textGetter(trackPoint), textPosition, whiteBrush, new CanvasTextFormat());
        }

        private KeyValuePair<TrackPoint, Vector2> FindNearest(IDictionary<TrackPoint, Vector2> points, float target)
        {
            KeyValuePair<TrackPoint, Vector2>? previousKvp = null;

            foreach (var kvp in points)
            {
                var trackPoint = kvp.Key;
                var position = kvp.Value;

                if (position.X > target)
                {
                    if (previousKvp == null)
                        return kvp;

                    var previousOffset = Math.Abs(previousKvp.Value.Value.X - target);
                    var currentOffset = Math.Abs(position.X - target);

                    if (previousOffset < currentOffset)
                        return previousKvp.Value;
                    else
                        return kvp;
                }

                previousKvp = kvp;
            }

            return previousKvp.Value;
        }

        private TrackPoint FindNearest(IEnumerable<TrackPoint> points, double targetDistance)
        {
            TrackPoint previousTrackpoint = null;

            foreach (var point in points)
            {
                if (point.AccumulatedDistance.SiValue > targetDistance)
                {
                    if (previousTrackpoint == null)
                        return point;

                    var previousOffset = Math.Abs(previousTrackpoint.AccumulatedDistance.SiValue - targetDistance);
                    var currentOffset = Math.Abs(point.AccumulatedDistance.SiValue - targetDistance);

                    if (previousOffset < currentOffset)
                        return previousTrackpoint;
                    else
                        return point;
                }

                previousTrackpoint = point;
            }

            return previousTrackpoint;
        }

        private void Input_PointerPressed(object sender, PointerEventArgs args)
        {
            if (CheckIsInChartsBounds(args.CurrentPoint.Position))
            {
                _holdProgression = true;
                _capturedDesiredX = args.CurrentPoint.Position.X;
            }
        }

        private void Input_PointerReleased(object sender, PointerEventArgs args)
        {
            _holdProgression = false;
            _capturedDesiredX = null;
        }

        private void Input_PointerMoved(object sender, PointerEventArgs args)
        {
            if (CheckIsInChartsBounds(args.CurrentPoint.Position) && args.CurrentPoint.IsInContact)
                _capturedDesiredX = args.CurrentPoint.Position.X;
        }

        private bool CheckIsInChartsBounds(Point position)
        {
            var absoluteBounds = GetAbsoluteDestinationRect(this.CanvasControl.Size, _chartsRelativeBounds);

            return absoluteBounds.Contains(position);
        }
    }
}
