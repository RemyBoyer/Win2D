using RBO.Win2D;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;

namespace RboExample
{
    class TcxParser
    {
        public static async Task<IReadOnlyList<TrackPoint>> LoadTcx(string name)
        {
            var file = await Package.Current.InstalledLocation.GetFileAsync(name);
            var ras = await file.OpenReadAsync();
            using (var stream = ras.AsStreamForRead())
            {
                var doc = XDocument.Load(stream);
                var ns = XNamespace.Get("http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2");
                var extns = XNamespace.Get("http://www.garmin.com/xmlschemas/ActivityExtension/v2");

                var trackPoints = new List<TrackPoint>();
                TrackPoint previousTrackpoint = null;

                var trackPointNodes = doc.Descendants(ns + "Trackpoint");
                foreach (var trackPointNode in trackPointNodes)
                {
                    var timeText = trackPointNode.Element(ns + "Time").Value;

                    var positionNode = trackPointNode.Element(ns + "Position");
                    if (positionNode == null)
                        continue;

                    var latitude = positionNode.Element(ns + "LatitudeDegrees").Value;
                    var longitude = positionNode.Element(ns + "LongitudeDegrees").Value;
                    var elevation = trackPointNode.Element(ns + "AltitudeMeters").Value;

                    var distance = trackPointNode.Element(ns + "DistanceMeters").Value;

                    var cadenceNode = trackPointNode.Element(ns + "Cadence");
                    var cadence = cadenceNode != null ? cadenceNode.Value : "0";

                    var speed = trackPointNode.Element(ns + "Extensions").Element(extns + "TPX").Element(extns + "Speed").Value;
                    // elevation : decimal

                    var trackpoint = new TrackPoint
                    {
                        Coordinate = new Geo.CoordinateZ(
                            (double)decimal.Parse(latitude, CultureInfo.InvariantCulture),
                            (double)decimal.Parse(longitude, CultureInfo.InvariantCulture),
                            (double)decimal.Parse(elevation, CultureInfo.InvariantCulture)
                            ),
                        TimeUtc = DateTime.Parse(timeText, CultureInfo.InvariantCulture),
                        AccumulatedDistance = new Geo.Measure.Distance(double.Parse(distance, CultureInfo.InvariantCulture)),
                        Cadence = int.Parse(cadence, CultureInfo.InvariantCulture),
                        Speed = double.Parse(speed, CultureInfo.InvariantCulture),
                    };

                    if (previousTrackpoint != null)
                        previousTrackpoint.DistanceFromPrevious = trackpoint.AccumulatedDistance - previousTrackpoint.AccumulatedDistance;

                    trackPoints.Add(trackpoint);
                    previousTrackpoint = trackpoint;
                }

                // Calculer l'inclinaison
                var averagePointsCount = 11;
                var analysisIndex = averagePointsCount / 2; // arrondi en dessous
                var pointsBuffer = new List<TrackPointAggregate>(averagePointsCount + 1);
                var inclinationsBuffer = new List<double>(averagePointsCount);

                foreach (var point in trackPoints)
                {
                    // calculer l'inclinaison
                    var previousPoint = pointsBuffer.Count > 0 ? pointsBuffer[pointsBuffer.Count - 1] : null;
                    var distanceGain = previousPoint != null ? point.AccumulatedDistance.SiValue - previousPoint.Distance : 0;
                    var elevationGain = pointsBuffer.Count > 0 ? point.Coordinate.Elevation - pointsBuffer[pointsBuffer.Count - 1].Elevation : 0;

                    // Aggreger les points lorsque la distance est trop courte car cela peut amener à des valeurs d'inclinaison aberrantes
                    if (distanceGain < 2)
                    {
                        if (previousPoint == null)
                            pointsBuffer.Add(new TrackPointAggregate(point));

                        pointsBuffer[pointsBuffer.Count - 1].AddTrackPoint(point);

                        continue;
                    }

                    // Valeurs aberrantes
                    if (elevationGain > distanceGain)
                        elevationGain = distanceGain;
                    if (elevationGain < -distanceGain)
                        elevationGain = -distanceGain;

                    var sin = elevationGain / distanceGain;
                    var inclinationAngleRad = Math.Sinh(sin);
                    var inclinationAngleDegree = inclinationAngleRad * 180 / Math.PI;

                    pointsBuffer.Add(new TrackPointAggregate(point));
                    inclinationsBuffer.Add(inclinationAngleDegree);

                    if (pointsBuffer.Count > averagePointsCount)
                    {
                        pointsBuffer.RemoveAt(0);
                        inclinationsBuffer.RemoveAt(0);
                    }
                    else
                        continue; // Ne pas calculer d'inclinaison tant qu'on n'a pas assez de points

                    // Faire une moyenne
                    var avgInclination = inclinationsBuffer.Average();

                    if (avgInclination > 200)
                        System.Diagnostics.Debugger.Break();

                    pointsBuffer[analysisIndex].SetInclination(avgInclination);
                }

                return trackPoints;
            }
        }


        private class TrackPointAggregate
        {
            private List<TrackPoint> _points = new List<TrackPoint>();
            private double _elevation;
            private double _distance;

            public TrackPointAggregate(TrackPoint tp)
            {
                _elevation = tp.Coordinate.Elevation;
                _distance = tp.AccumulatedDistance.SiValue;
                _points.Add(tp);
            }

            public void AddTrackPoint(TrackPoint tp)
            {
                _points.Add(tp);
            }

            public double Elevation
            {
                get { return _elevation; }
            }

            public double Distance
            {
                get { return _distance; }
            }

            public void SetInclination(double inclination)
            {
                foreach (var tp in _points)
                {
                    tp.Inclination = inclination;
                }
            }


        }
    }
}
