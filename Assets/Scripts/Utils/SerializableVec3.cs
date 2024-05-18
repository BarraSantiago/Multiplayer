using System;
using System.Numerics;

namespace Utils
{
    [Serializable]
    public struct Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        public static Vec3 FromVector3(Vector3 vector3)
        {
            return new Vec3(vector3.X, vector3.Y, vector3.Z);
        }
    }
}