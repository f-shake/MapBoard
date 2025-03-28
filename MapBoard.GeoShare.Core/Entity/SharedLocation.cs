﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.GeoShare.Core.Entity
{
    public class SharedLocationEntity : EntityBase
    {
        public SharedLocationEntity() { }

        public SharedLocationEntity(double longitude,double latitude,double altitude) { }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double Altitude { get; set; }

        public double Accuracy { get; set; }

        public DateTime Time { get; set; }

        public int UserId { get; set; }
    }
}
