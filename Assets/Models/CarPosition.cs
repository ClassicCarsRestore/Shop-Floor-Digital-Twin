using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Models
{

    public class CarPosition
    {
        public Vector3 position;
        public bool car;
        public string area;
        public float rotationY;

        public CarPosition(Vector3 position, string area, float rotationY)
        {
            this.position = position;
            this.car = false;
            this.area = area;
            this.rotationY = rotationY;
        }

    }
}