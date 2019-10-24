using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MSCLoader;

namespace ChaseCamera
{
    public class CameraOffset
    {
        public string name;
        public string gameObjectName;

        public Vector3 offset;
        public Vector3 lookAtOffset;

        public Settings settingsOffsetY;
        public Settings settingsOffsetZ;
        public Settings settingsLookAtOffsetY;

        public float audioMinDistance;

        public CameraOffset(string name, string gameObjectName, Vector3 offset, Vector3 lookAtOffset, float audioMinDistance)
        {
            this.name = name;
            this.gameObjectName = gameObjectName;
            this.offset = offset;
            this.lookAtOffset = lookAtOffset;
            this.audioMinDistance = audioMinDistance;

            settingsOffsetY = new Settings(name + "_offsetY_v1.1", "Offset Y", offset.y, () => ApplySettings());
            settingsOffsetZ = new Settings(name + "_offsetZ_v1.1", "Offset Z", offset.z, () => ApplySettings());
            settingsLookAtOffsetY = new Settings(name + "_lookAtOffsetY_v1.1", "Look At Offset Y", lookAtOffset.y, () => ApplySettings());
        }

        public void ApplySettings()
        {
            offset = new Vector3(offset.x, float.Parse(settingsOffsetY.GetValue().ToString(), CultureInfo.InvariantCulture), float.Parse(settingsOffsetZ.GetValue().ToString(), CultureInfo.InvariantCulture) * -1f);
            lookAtOffset = new Vector3(lookAtOffset.x, float.Parse(settingsLookAtOffsetY.GetValue().ToString(), CultureInfo.InvariantCulture), lookAtOffset.z);
        }
    }
}
