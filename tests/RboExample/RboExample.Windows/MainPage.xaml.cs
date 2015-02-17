using Geo;
using Microsoft.Graphics.Canvas;
using RBO.Win2D;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
        private readonly TimeSpan _totalDuration = TimeSpan.FromSeconds(30);
        private bool _holdProgression;
        private double? _capturedDesiredX;

        public MainPage()
        {
            this.InitializeComponent();

            this.CanvasControl.Input.PointerPressed += Input_PointerPressed;
            this.CanvasControl.Input.PointerMoved += Input_PointerMoved;
            this.CanvasControl.Input.PointerReleased += Input_PointerReleased;
        }

        private async void CanvasControl_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            var loadTask = TcxParser.LoadTcx("activity_368801578.tcx");
            args.TrackAsyncAction(loadTask.AsAsyncAction());

            trackpoints = (await loadTask).ToArray();
        }

        private void CanvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            DrawElevation(args.DrawingSession, sender.Size, new Rect(0, 0, 1, .25));
            DrawSpeed(args.DrawingSession, sender.Size, new Rect(0, .25, 1, .25));
            DrawCandence(args.DrawingSession, sender.Size, new Rect(0, .5, 1, .25));

            DrawProgression(args.DrawingSession, sender.Size);
        }

        private void CanvasControl_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            if (_capturedDesiredX.HasValue)
            {
                _progression = _capturedDesiredX.Value / sender.Size.Width;
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

            var points = GetPolyLinePoints(absoluteDestination, tp => tp.Coordinate.Elevation, true).ToArray();
            var geometry = CreatePathGeometry(drawingSession, points.Select(p => p.Item1), true);
            drawingSession.FillGeometry(geometry, foreground);

            DrawTextPosition(drawingSession, size, points, tp => tp.Coordinate.Elevation.ToString("# m"));
        }

        private void DrawSpeed(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.Blue);
            var absoluteDestination = GetAbsoluteDestinationRect(size, destinationRelative);

            var points = GetPolyLinePoints(absoluteDestination, tp => tp.Speed, true).ToArray();
            var geometry = CreatePathGeometry(drawingSession, points.Select(p => p.Item1), true);
            drawingSession.FillGeometry(geometry, foreground);

            DrawTextPosition(drawingSession, size, points, tp => tp.Speed.ToString("# km/h"));
        }

        private void DrawCandence(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.Orange);
            var absoluteDestination = GetAbsoluteDestinationRect(size, destinationRelative);

            var points = GetPolyLinePoints(absoluteDestination, tp => tp.Cadence, true).ToArray();
            var geometry = CreatePathGeometry(drawingSession, points.Select(p => p.Item1), true);
            drawingSession.FillGeometry(geometry, foreground);

            DrawTextPosition(drawingSession, size, points, tp => tp.Cadence.ToString("0 tr/m"));
        }

        private void DrawProgression(CanvasDrawingSession drawingSession, Size size)
        {
            var x = (float)(_progression * size.Width);
            drawingSession.DrawLine(new Vector2(x, 0f), new Vector2(x, (float)size.Height), Colors.White, 2);

            // Distance
            var totalDistance = trackpoints[trackpoints.Length - 1].AccumulatedDistance.SiValue;
            var distanceKm = _progression * totalDistance / 1000;

            var whiteBrush = new CanvasSolidColorBrush(drawingSession, Colors.White);

            var position = new Vector2(x + 10, 0);

            drawingSession.DrawText(distanceKm.ToString("#.## km"), position, whiteBrush, new CanvasTextFormat());

        }

        private Rect GetAbsoluteDestinationRect(Size size, Rect relative)
        {
            var absoluteDestination = new Rect(relative.Left * size.Width, relative.Top * size.Height,
                relative.Width * size.Width, relative.Height * size.Height);

            return absoluteDestination;
        }

        private IEnumerable<Tuple<Vector2, TrackPoint>> GetPolyLinePoints(Rect destinationRect, Func<TrackPoint, double> valueGetter, bool close)
        {
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

                yield return Tuple.Create(new Vector2(x, y + maxHeight - barHeight), trackpoint);

                x += barWidth;
            }

            if (close)
            {

                yield return Tuple.Create<Vector2, TrackPoint>(new Vector2((float)destinationRect.Right, (float)destinationRect.Bottom), null);
                yield return Tuple.Create<Vector2, TrackPoint>(new Vector2((float)destinationRect.Left, (float)destinationRect.Bottom), null);

            }
        }

        private CanvasGeometry CreatePathGeometry(CanvasDrawingSession drawingSession, IEnumerable<Vector2> points, bool close)
        {
            var pathBuilder = new CanvasPathBuilder(drawingSession);
            pathBuilder.BeginFigure(points.First());

            foreach (var point in points.Skip(1))
                pathBuilder.AddLine(point);

            pathBuilder.EndFigure(close ? CanvasFigureLoop.Closed : CanvasFigureLoop.Open);

            var geometry = CanvasGeometry.CreatePath(pathBuilder);
            return geometry;
        }

        private void DrawTextPosition(CanvasDrawingSession drawingSession, Size size, Tuple<Vector2, TrackPoint>[] points, Func<TrackPoint, string> textGetter)
        {
            var x = _progression * size.Width;
            var nearestPoint = FindNearest(points, (float)x);
            if (nearestPoint != null && nearestPoint.Item2 != null)
            {
                var whiteBrush = new CanvasSolidColorBrush(drawingSession, Colors.White);
                drawingSession.FillCircle(nearestPoint.Item1, 3, whiteBrush);

                var position = new Vector2(nearestPoint.Item1.X + 10, nearestPoint.Item1.Y - 10);

                drawingSession.DrawText(textGetter(nearestPoint.Item2), position, whiteBrush, new CanvasTextFormat());
            }
        }

        private Tuple<Vector2, TrackPoint> FindNearest(Tuple<Vector2, TrackPoint>[] points, float target)
        {
            for (int i = 0; i < points.Length; i++)
            {
                var current = points[i];

                if (current.Item1.X > target)
                {
                    var previous = points[i - 1];

                    var previousOffset = Math.Abs(previous.Item1.X - target);
                    var currentOffset = Math.Abs(current.Item1.X - target);

                    if (previousOffset < currentOffset)
                        return previous;
                    else
                        return current;
                }
            }

            return null;
        }

        private void Input_PointerPressed(object sender, Windows.UI.Core.PointerEventArgs args)
        {
            _holdProgression = true;
            _capturedDesiredX = args.CurrentPoint.Position.X;
        }

        private void Input_PointerReleased(object sender, Windows.UI.Core.PointerEventArgs args)
        {
            _holdProgression = false;
            _capturedDesiredX = null;
        }

        private void Input_PointerMoved(object sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (args.CurrentPoint.IsInContact)
                _capturedDesiredX = args.CurrentPoint.Position.X;
        }
    }
}
