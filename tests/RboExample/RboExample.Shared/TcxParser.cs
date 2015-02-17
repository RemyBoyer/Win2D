using RBO.Win2D;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

                return trackPoints;
            }
        }
    }
}
