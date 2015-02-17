using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using System.IO;
using System.Linq;
using Geo;
using Geo.Gps;
using Geo.Measure;

namespace RBO.Win2D
{
    static class GpxParser
    {
        public static async Task<IReadOnlyList<TrackPoint>> LoadGpx(string name)
        {
            var file = await Package.Current.InstalledLocation.GetFileAsync(name);
            var ras = await file.OpenReadAsync();
            var stream = ras.AsStreamForRead();

            var serializer = new Geo.Gps.Serialization.Gpx11Serializer();
            var data = serializer.DeSerialize(new Geo.Gps.Serialization.StreamWrapper(stream));

            var fixes = data.Tracks
                .SelectMany(t => t.Segments)
                .SelectMany(s => s.Fixes);

            var extendedFixes = new List<TrackPoint>();
            TrackPoint previousFix = null;

            foreach (var fix in fixes)
            {
                var extendedFix = new TrackPoint();

                if (previousFix != null)
                {
                    var distance = GeoContext.Current.GeodeticCalculator.CalculateLength(new CoordinateSequence(previousFix.Coordinate, extendedFix.Coordinate));

                    extendedFix.DistanceFromPrevious = distance;
                    extendedFix.AccumulatedDistance = previousFix.AccumulatedDistance + distance;
                }

                extendedFixes.Add(extendedFix);
                previousFix = extendedFix;
            }

            return extendedFixes;
        }
    }
}
