using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DEM
{
    public class mNode
    {
        public mNode(int n, double x, double y, double z)
        {
            N = n; X = x; Y = y; Z = z;
        }
        public mNode(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }
        public mNode() { }
        public int N;
        public double X;
        public double Y;
        public double Z;
    }

    public class mEdge
    {
        public int Start;
        public int End;

        public int LeftTri = -1;
        public int RightTri = -1;
    }

    public class mTriangle
    {
        public int NodeA;
        public int NodeB;
        public int NodeC;

        public int EdgeA;
        public int EdgeB;
        public int EdgeC;

        public bool isSearched = false;
        public bool isValid = false;
    }

}
