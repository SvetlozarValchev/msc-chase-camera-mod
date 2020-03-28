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
        }

        public void ApplySettings(float offsetY, float offsetZ, float lookAtOffsetY, float audioDistance)
        {
            this.offset = new Vector3(offset.x, offsetY, offsetZ);
            this.lookAtOffset = new Vector3(lookAtOffset.x, lookAtOffsetY, lookAtOffset.z);
            this.audioMinDistance = audioDistance;
        }
    }
}
