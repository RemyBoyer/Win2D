﻿using Geo;
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

        private Matrix3x2 _elevationTransform;
        private IDictionary<TrackPoint, Vector2> _elevationPoints;

        private Matrix3x2 _speedTransform;
        private IDictionary<TrackPoint, Vector2> _speedPoints;

        private Matrix3x2 _cadenceTransform;
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
            _mapRelativeBounds = new Rect(0, 0, 1, .5);
            _chartsRelativeBounds = new Rect(0, .5, 1, .5);
            _elevationRelativeBounds = new Rect(0, .5, 1, .25);
            _speedRelativeBounds = new Rect(0, .75, 1, .25);
            _candenceRelativeBounds = new Rect(0, .75, 1, .25);

            trackpoints = (await TcxParser.LoadTcx("activity_368801578.tcx")).ToArray();

            int i = 0;
            trackpoints = trackpoints.Where(tp => i++ % 3 == 0).ToArray();

            _bounds = new CoordinateSequence(trackpoints.Select(tp => tp.Coordinate)).GetBounds();

#if WINDOWS_PHONE_APP
            await this.MapControl.TrySetViewBoundsAsync(new Windows.Devices.Geolocation.GeoboundingBox(
                  new Windows.Devices.Geolocation.BasicGeoposition { Latitude = _bounds.MaxLat, Longitude = _bounds.MinLon },
                  new Windows.Devices.Geolocation.BasicGeoposition { Latitude = _bounds.MinLat, Longitude = _bounds.MaxLon }),
                  new Thickness(10), Windows.UI.Xaml.Controls.Maps.MapAnimationKind.None);
#else
            this.MapControl.SetView(new Bing.Maps.LocationRect(
                new Bing.Maps.Location(_bounds.MaxLat, _bounds.MinLon),
                new Bing.Maps.Location(_bounds.MinLat, _bounds.MaxLon)
                ), default(TimeSpan));
#endif

            var timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            timer.Tick += timer_Tick;
            timer.Start();
        }

        void timer_Tick(object sender, object e)
        {
#if WINDOWS_PHONE_APP
            Windows.Devices.Geolocation.Geopoint topLeft = null;

            try
            {
                this.MapControl.GetLocationFromOffset(new Windows.Foundation.Point(0, 0), out topLeft);
            }
            catch
            {
                var topOfMap = new Windows.Devices.Geolocation.Geopoint(new Windows.Devices.Geolocation.BasicGeoposition()
                {
                    Latitude = 85,
                    Longitude = 0
                });

                Windows.Foundation.Point topPoint;
                this.MapControl.GetOffsetFromLocation(topOfMap, out topPoint);
                this.MapControl.GetLocationFromOffset(new Windows.Foundation.Point(0, topPoint.Y), out topLeft);
            }

            Windows.Devices.Geolocation.Geopoint bottomRight = null;
            try
            {
                this.MapControl.GetLocationFromOffset(new Windows.Foundation.Point(this.MapControl.ActualWidth, this.MapControl.ActualHeight), out bottomRight);
            }
            catch
            {
                var bottomOfMap = new Windows.Devices.Geolocation.Geopoint(new Windows.Devices.Geolocation.BasicGeoposition()
                {
                    Latitude = -85,
                    Longitude = 0
                });

                Windows.Foundation.Point bottomPoint;
                this.MapControl.GetOffsetFromLocation(bottomOfMap, out bottomPoint);
                this.MapControl.GetLocationFromOffset(new Windows.Foundation.Point(0, bottomPoint.Y), out bottomRight);
            }

            if (topLeft != null && bottomRight != null)
            {
                var bounds = new Windows.Devices.Geolocation.GeoboundingBox(topLeft.Position, bottomRight.Position);
                _mapBounds = new Envelope(bounds.SoutheastCorner.Latitude, bounds.NorthwestCorner.Longitude, bounds.NorthwestCorner.Latitude, bounds.SoutheastCorner.Longitude);
            }
#else
            var bounds = this.MapControl.Bounds;
            _mapBounds = new Envelope(bounds.South, bounds.West, bounds.North, bounds.East);
#endif
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

        private void CanvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            DrawElevation(args.DrawingSession, sender.Size, _elevationRelativeBounds);
            DrawCadence(args.DrawingSession, sender.Size, _candenceRelativeBounds);
            DrawSpeed(args.DrawingSession, sender.Size, _speedRelativeBounds);
            DrawMap(args.DrawingSession, sender.Size, _mapRelativeBounds);

            DrawProgression(args.DrawingSession, sender.Size, _chartsRelativeBounds);
        }

        private void DrawElevation(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.Green);
            var absoluteDestination = GetAbsoluteDestinationRect(size, destinationRelative);

            var renderTarget = new CanvasRenderTarget(drawingSession, (float)absoluteDestination.Width, (float)absoluteDestination.Height);

            _elevationTransform = GetTransformationMatrix(size, destinationRelative);

            _elevationPoints = GetPolyLinePointsRelative(tp => tp.Coordinate.Elevation, true);

            using (var ds = renderTarget.CreateDrawingSession())
            {
                var elevationRenderTargetTransform = _elevationTransform * Matrix3x2.CreateTranslation((float)(-destinationRelative.Left * size.Width), (float)(-destinationRelative.Top * size.Height));

                var gradientStops = new List<CanvasGradientStop>();
                var minInclination = _elevationPoints.Keys.Min(tp => tp.Inclination);
                var maxInclination = _elevationPoints.Keys.Max(tp => tp.Inclination);

                var positions = new List<Vector2>();

                var pathBuilder = new CanvasPathBuilder(drawingSession);

                bool first = true;

                foreach (var point in _elevationPoints)
                {
                    var position = point.Value;

                    if (first)
                    {
                        pathBuilder.BeginFigure(position);
                        first = false;
                    }
                    else
                        pathBuilder.AddLine(position);

                    var mu = (point.Key.Inclination - minInclination) / (maxInclination - minInclination);

                    var color = GenerateColor(1, 1, 1, 4, 2, 1, 128, 127, mu * 2);

                    gradientStops.Add(new CanvasGradientStop { Color = color, Position = position.X });
                }

                pathBuilder.AddLine(new Vector2(1, 1));
                pathBuilder.AddLine(new Vector2(0, 1));

                pathBuilder.EndFigure(CanvasFigureLoop.Closed);

                var geometry = CanvasGeometry.CreatePath(pathBuilder);
                var brush = new CanvasLinearGradientBrush(ds, gradientStops.ToArray(), CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Straight)
                {
                    StartPoint = new Vector2((float)0, 0),
                    EndPoint = new Vector2((float)1, 0),
                };

                ds.Transform = elevationRenderTargetTransform;
                ds.FillGeometry(geometry, brush);
                ds.Transform = Matrix3x2.Identity;
            }


            var sharpen = new ConvolveMatrixEffect()
            {
                Source = renderTarget,
                KernelMatrix = new float[] { -1, -1, -1, -1, 9, -1, -1, -1, -1 },
            };

            //drawingSession.DrawImage(sharpen, (float)absoluteDestination.X, (float)absoluteDestination.Y);
            drawingSession.DrawImage(renderTarget, (float)absoluteDestination.X, (float)absoluteDestination.Y);
        }

        private void DrawSpeed(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Colors.DarkOliveGreen);

            _speedTransform = GetTransformationMatrix(size, destinationRelative);

            _speedPoints = GetPolyLinePointsRelative(tp => tp.Speed, true);
            var geometry = CreatePathGeometry(drawingSession, _speedPoints.Values, true, _speedTransform);

            drawingSession.DrawGeometry(geometry, foreground, 2f, new CanvasStrokeStyle
            {
                LineJoin = CanvasLineJoin.Round,
            });
        }

        private void DrawCadence(CanvasDrawingSession drawingSession, Size size, Rect destinationRelative)
        {
            if (trackpoints == null)
                return;

            var foreground = new CanvasSolidColorBrush(drawingSession, Color.FromArgb(255, 229, 252, 194));

            _cadenceTransform = GetTransformationMatrix(size, destinationRelative);
            drawingSession.Transform = _cadenceTransform;

            _candencePoints = GetPolyLinePointsRelative(tp => tp.Cadence, true);

            var geometry = CreatePathGeometry(drawingSession, _candencePoints.Values, true, Matrix3x2.Identity);
            drawingSession.FillGeometry(geometry, foreground);

            drawingSession.Transform = Matrix3x2.Identity;
        }

        private void DrawProgression(CanvasDrawingSession drawingSession, Size size, Rect relativeDestination)
        {
            var absoluteDestination = GetAbsoluteDestinationRect(size, relativeDestination);

            DrawTextPosition(drawingSession, _elevationTransform, _elevationPoints, tp => tp.Coordinate.Elevation.ToString("# m"));
            DrawTextPosition(drawingSession, _speedTransform, _speedPoints, tp => tp.Speed.ToString("# km/h"));
            DrawTextPosition(drawingSession, _cadenceTransform, _candencePoints, tp => tp.Cadence.ToString("0 tr/m"));

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
            var transformedCoordinates = Multiply(transformMatrix, centerCoordinates);
            drawingSession.FillCircle(transformedCoordinates, 10, Colors.Black);
        }

        private Matrix3x2 GetTransformationMatrix(Size size, Rect relativeDestination)
        {
            var xScale = size.Width * relativeDestination.Width;
            var yScale = size.Height * relativeDestination.Height;

            var xOffset = size.Width * relativeDestination.Left;
            var yOffset = size.Height * relativeDestination.Top;

            return Matrix3x2.CreateScale((float)xScale, (float)yScale) * Matrix3x2.CreateTranslation((float)xOffset, (float)yOffset);
        }

        private Vector2 Multiply(Matrix3x2 m, Vector2 v)
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

        private IDictionary<TrackPoint, Vector2> GetPolyLinePointsRelative(Func<TrackPoint, double> valueGetter, bool close)
        {
            var points = new Dictionary<TrackPoint, Vector2>();

            var indexedValues = trackpoints.ToDictionary(tp => tp, tp => valueGetter(tp));
            var maxValue = indexedValues.Values.Max();

            float x = 0;

            var totalDistance = trackpoints[trackpoints.Length - 1].AccumulatedDistance;
            double widthPerUnit = 1 / totalDistance.SiValue;

            TrackPoint previousTrackpoint = trackpoints[0];
            foreach (var trackpoint in trackpoints)
            {
                var value = indexedValues[trackpoint];

                var valueRatio = value / maxValue;
                valueRatio = Math.Max(0, valueRatio);
                valueRatio = Math.Min(1, valueRatio);

                var barHeight = (float)valueRatio;
                var barWidth = (float)(widthPerUnit * (trackpoint.AccumulatedDistance.SiValue - previousTrackpoint.AccumulatedDistance.SiValue));

                points[trackpoint] = new Vector2(x, 1f - barHeight);

                x += barWidth;
                previousTrackpoint = trackpoint;
            }

            return points;
        }

        private CanvasGeometry CreatePathGeometry(CanvasDrawingSession drawingSession, IEnumerable<Vector2> points, bool close, Matrix3x2 transformMatrix)
        {
            var pathBuilder = new CanvasPathBuilder(drawingSession);
            pathBuilder.BeginFigure(points.First());

            foreach (var point in points.Skip(1))
                pathBuilder.AddLine(Multiply(transformMatrix, point));

            if (close)
            {
                pathBuilder.AddLine(Multiply(transformMatrix, new Vector2(1, 1)));
                pathBuilder.AddLine(Multiply(transformMatrix, new Vector2(0, 1)));
            }

            pathBuilder.EndFigure(close ? CanvasFigureLoop.Closed : CanvasFigureLoop.Open);

            var geometry = CanvasGeometry.CreatePath(pathBuilder);
            return geometry;
        }

        private void DrawTextPosition(CanvasDrawingSession drawingSession, Matrix3x2 transform, IDictionary<TrackPoint, Vector2> points, Func<TrackPoint, string> textGetter)
        {
            var nearestPoint = FindNearest(points, (float)_progression);

            var trackPoint = nearestPoint.Key;
            var pointPosition = Multiply(transform, nearestPoint.Value);

            var whiteBrush = new CanvasSolidColorBrush(drawingSession, Colors.White);
            drawingSession.FillCircle(pointPosition, 3, whiteBrush);

            var textPosition = new Vector2(pointPosition.X + 10, pointPosition.Y - 10);

            var renderTarget = new CanvasRenderTarget(drawingSession, 100, 50);

            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.DrawText(textGetter(trackPoint), 0, 0, Colors.White);
            }

            var shadowEffect = new Transform2DEffect
            {
                Source = new ShadowEffect
                {
                    Source = renderTarget,
                    BlurAmount = 2
                },
            };

            var compositeEffect = new CompositeEffect
            {
                Inputs = { shadowEffect, renderTarget }
            };

            drawingSession.DrawImage(compositeEffect, textPosition);
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

        private Color GenerateColor(double rf, double gf, double bf, double rp, double gp, double bp, double center, double width, double value)
        {
            var r = (Math.Sin(rf * value + rp)) * width + center;
            var g = (Math.Sin(gf * value + gp)) * width + center;
            var b = (Math.Sin(bf * value + bp)) * width + center;

            return Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
        }
    }
}
