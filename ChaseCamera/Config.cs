using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChaseCamera
{
    class Config
    {
        public float smoothFollow;
        public float smoothLook;
        public bool resetAfterInactivity;
        public float resetAfterTime;
        public bool showSpeedAndRpm;
        public bool lookBehindToggle;
        public List<ConfigVehicle> vehicles;
    }

    struct ConfigVehicle
    {
        public string name;
        public string gameObject;
        public List<float> offset;
        public List<float> lookAtOffset;
        public float audioMinDistance;
    }
}
