using Geo;
using Geo.Gps;
using Geo.Measure;
using System;
using System.Collections.Generic;
using System.Text;

namespace RBO.Win2D
{
    class TrackPoint
    {

        public DateTime TimeUtc { get; set; }

        public CoordinateZ Coordinate { get; set; }

        public Distance DistanceFromPrevious { get; set; }

        public Distance AccumulatedDistance { get; set; }

        public int Cadence { get; set; }

        public double Speed { get; set; }

        public double Inclination { get; set; }

    }
}
