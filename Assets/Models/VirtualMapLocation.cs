using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Models;
using System.Globalization;


namespace Models
{

    public class VirtualMapLocation
    {
        public string id; 
        public string name;
        public float coordinateX;
        public float coordinateY;
        public float coordinateZ;
        public float rotation;
        public List<string> activityIds;
        public List<VerticesCoordinates> vertices;
        public string color;
        public int capacity;
    }
}
