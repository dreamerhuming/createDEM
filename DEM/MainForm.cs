using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using System.Windows.Forms;
using Autodesk.AutoCAD.Colors;

namespace DEM
{
    public partial class MainForm : Form
    {
        Class1 myClass;
        List<mNode> NodeList = new List<mNode>();
        List<mEdge> EdgeList = new List<mEdge>();
        List<mTriangle> TriList = new List<mTriangle>();
        public MainForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            myClass = new Class1();
            OpenFile();
            DrawDelaunay();
            //DrawDEM();
        }

        public void OpenFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            string fileName;
            openFileDialog.Filter = "文本文件(*.txt)|*.txt";
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string readStr;
                mNode tempNode;
                fileName = openFileDialog.FileName;

                using (StreamReader sReader = new StreamReader(fileName))
                {
                    readStr = "";
                    while (readStr != null)
                    {
                        if (readStr.Length > 10)
                        {
                            string[] sArray = readStr.Split(',');
                            tempNode = new mNode();
                            tempNode.N = int.Parse(sArray[0]) - 1;   //点序号从0开始
                            tempNode.X = double.Parse(sArray[1]);
                            tempNode.Y = double.Parse(sArray[2]);
                            tempNode.Z = double.Parse(sArray[3]);
                            NodeList.Add(tempNode);
                        }
                        readStr = sReader.ReadLine();
                    }
                }
            }
        }
        public void DrawDelaunay()
        {
            //---获取凸包边界
            GetEdgeList();
            double angleV1V2, angleV2V3, angleMaxV1V2 = 0, angleMaxV2V3 = 0;
            int newIndex;
            double[] vector1 = new double[2];
            double[] vector2 = new double[2];
            double[] vector3 = new double[2];
            double lengthV1, lengthV2, lengthV3;
            bool isTriExist = false;    //判断三角形是否存在
            mEdge edge;
            for (int i = 0; i < EdgeList.Count; i++)
            {
                edge = new mEdge();
                edge = EdgeList[i];

                //------------------------------左三角形不存在时
                if (edge.LeftTri == -1)
                {
                    newIndex = -1;
                    angleMaxV1V2 = 0; angleMaxV2V3 = 0;
                    vector1[0] = NodeList[edge.End].X - NodeList[edge.Start].X;
                    vector1[1] = NodeList[edge.End].Y - NodeList[edge.Start].Y;
                    for (int j = 0; j < NodeList.Count; j++)
                    {
                        if (j != edge.Start && j != edge.End) //排除端点
                        {
                            vector2[0] = NodeList[j].X - NodeList[edge.Start].X;
                            vector2[1] = NodeList[j].Y - NodeList[edge.Start].Y;
                            if (vector1[0] * vector2[1] - vector2[0] * vector1[1] > 0)  //点在vector1左侧
                            {
                                vector3[0] = NodeList[j].X - NodeList[edge.End].X;
                                vector3[1] = NodeList[j].Y - NodeList[edge.End].Y;

                                lengthV1 = Math.Sqrt(vector1[0] * vector1[0] + vector1[1] * vector1[1]);
                                lengthV2 = Math.Sqrt(vector2[0] * vector2[0] + vector2[1] * vector2[1]);
                                lengthV3 = Math.Sqrt(vector3[0] * vector3[0] + vector3[1] * vector3[1]);

                                angleV1V2 = Math.Acos((vector1[0] * vector2[0] + vector1[1] * vector2[1]) / (lengthV1 * lengthV2));
                                angleV2V3 = Math.Acos((vector2[0] * vector3[0] + vector2[1] * vector3[1]) / (lengthV2 * lengthV3));

                                if (angleV2V3 > angleMaxV2V3)
                                {
                                    angleMaxV2V3 = angleV2V3;
                                    angleMaxV1V2 = angleV1V2;
                                    newIndex = j;
                                }
                                else if (angleV2V3 == angleMaxV2V3 && angleMaxV1V2 >= angleV1V2)
                                {
                                    angleMaxV1V2 = angleV1V2;
                                    newIndex = j;
                                }
                            }
                        }
                    }
                    if (newIndex != -1) //找到符合要求的点，然后记录
                    {
                        mTriangle triangle = new mTriangle();
                        triangle.NodeA = edge.Start;
                        triangle.NodeB = edge.End;
                        triangle.NodeC = newIndex;

                        edge.LeftTri = TriList.Count;  //三角形索引从0开始
                        isTriExist = false;

                        //记录第一条边
                        for (int k = 0; k < EdgeList.Count; k++)
                        {
                            mEdge tempEdge = EdgeList[k];
                            if (tempEdge.Start == edge.Start && tempEdge.End == newIndex)
                            {
                                tempEdge.RightTri = TriList.Count;
                                triangle.EdgeB = k;
                                isTriExist = true;
                                break;
                            }
                            else if (tempEdge.Start == newIndex && tempEdge.End == edge.Start)
                            {
                                tempEdge.LeftTri = TriList.Count;
                                triangle.EdgeB = k;
                                isTriExist = true;
                                break;
                            }
                        }
                        if (isTriExist == false)
                        {
                            mEdge newEdge = new mEdge();
                            newEdge.Start = newIndex;
                            newEdge.End = edge.Start;
                            newEdge.LeftTri = TriList.Count;
                            triangle.EdgeB = EdgeList.Count;
                            EdgeList.Add(newEdge);

                        }

                        isTriExist = false;
                        //记录第二条边
                        for (int k = 0; k < EdgeList.Count; k++)
                        {
                            mEdge tempEdge = EdgeList[k];
                            if (tempEdge.Start == newIndex && tempEdge.End == edge.End)
                            {
                                tempEdge.RightTri = TriList.Count;
                                triangle.EdgeC = k;
                                isTriExist = true;
                                break;
                            }
                            else if (tempEdge.Start == edge.End && tempEdge.End == newIndex)
                            {
                                tempEdge.LeftTri = TriList.Count;
                                triangle.EdgeC = k;
                                isTriExist = true;
                                break;
                            }
                        }

                        if (isTriExist == false)
                        {
                            mEdge newEdge = new mEdge();
                            newEdge.Start = edge.End;
                            newEdge.End = newIndex;
                            newEdge.LeftTri = TriList.Count;
                            triangle.EdgeC = EdgeList.Count;
                            EdgeList.Add(newEdge);

                        }
                        triangle.EdgeA = i;
                        TriList.Add(triangle);
                    }
                }

                ////////////////////////////////////////////////////////////////////////////右三角形不存在时
                else if (edge.RightTri == -1)
                {
                    newIndex = -1;
                    angleMaxV1V2 = 0; angleMaxV2V3 = 0;
                    vector1[0] = NodeList[edge.End].X - NodeList[edge.Start].X;
                    vector1[1] = NodeList[edge.End].Y - NodeList[edge.Start].Y;

                    for (int j = 0; j < NodeList.Count; j++)
                    {
                        if (j != edge.Start && j != edge.End)
                        {
                            vector2[0] = NodeList[j].X - NodeList[edge.Start].X;
                            vector2[1] = NodeList[j].Y - NodeList[edge.Start].Y;

                            if (vector1[0] * vector2[1] - vector2[0] * vector1[1] < 0)
                            {

                                vector3[0] = NodeList[j].X - NodeList[edge.End].X;
                                vector3[1] = NodeList[j].Y - NodeList[edge.End].Y;

                                lengthV1 = Math.Sqrt(vector1[0] * vector1[0] + vector1[1] * vector1[1]);
                                lengthV2 = Math.Sqrt(vector2[0] * vector2[0] + vector2[1] * vector2[1]);
                                lengthV3 = Math.Sqrt(vector3[0] * vector3[0] + vector3[1] * vector3[1]);

                                angleV1V2 = Math.Acos((vector1[0] * vector2[0] + vector1[1] * vector2[1]) / (lengthV1 * lengthV2));
                                angleV2V3 = Math.Acos((vector2[0] * vector3[0] + vector2[1] * vector3[1]) / (lengthV2 * lengthV3));

                                if (angleV2V3 > angleMaxV2V3)
                                {
                                    angleMaxV2V3 = angleV2V3;
                                    angleMaxV1V2 = angleV1V2;
                                    newIndex = j;
                                }
                                else if (angleV2V3 == angleMaxV2V3 && angleMaxV1V2 < angleV1V2)
                                {
                                    angleMaxV1V2 = angleV1V2;
                                    newIndex = j;
                                }
                            }
                        }
                    }

                    if (newIndex != -1)
                    {
                        mTriangle triangle = new mTriangle();
                        triangle.NodeA = edge.Start;
                        triangle.NodeB = edge.End;
                        triangle.NodeC = newIndex;
                        edge.RightTri = TriList.Count;
                        isTriExist = false;

                        //记录vector2向量边
                        for (int k = 0; k < EdgeList.Count; k++)
                        {
                            mEdge tempEdge = EdgeList[k];
                            if (tempEdge.Start == newIndex && tempEdge.End == edge.Start)
                            {
                                tempEdge.RightTri = TriList.Count;
                                triangle.EdgeB = k;
                                isTriExist = true;
                                break;
                            }
                            else if (tempEdge.Start == edge.Start && tempEdge.End == newIndex)
                            {
                                tempEdge.LeftTri = TriList.Count;
                                triangle.EdgeB = k;
                                isTriExist = true;
                                break;
                            }
                        }
                        if (isTriExist == false)
                        {
                            mEdge newEdge = new mEdge();
                            newEdge.Start = edge.Start;
                            newEdge.End = newIndex;
                            newEdge.LeftTri = TriList.Count;
                            triangle.EdgeB = EdgeList.Count;
                            EdgeList.Add(newEdge);

                        }

                        isTriExist = false;

                        //记录Vector3向量边
                        for (int k = 0; k < EdgeList.Count; k++)
                        {
                            mEdge tempEdge = EdgeList[k];
                            if (tempEdge.Start == edge.End && tempEdge.End == newIndex)
                            {
                                tempEdge.RightTri = TriList.Count;
                                triangle.EdgeC = k;
                                isTriExist = true;
                                break;
                            }
                            else if (tempEdge.Start == newIndex && tempEdge.End == edge.End)
                            {
                                tempEdge.LeftTri = TriList.Count;
                                triangle.EdgeC = k;
                                isTriExist = true;
                                break;
                            }
                        }
                        if (isTriExist == false)
                        {
                            mEdge newEdge = new mEdge();
                            newEdge.Start = newIndex;
                            newEdge.End = edge.End;
                            newEdge.LeftTri = TriList.Count;
                            triangle.EdgeC = EdgeList.Count;
                            EdgeList.Add(newEdge);

                        }
                        triangle.EdgeA = i;
                        TriList.Add(triangle);
                    }
                }
            }

            //////////////////////////////////////////////////////////////////////////
            ///绘图
            //////////////////////////////////////////////////////////////////////////
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database acDb = acDoc.Database;

            using (Transaction acDelaunayTrans = acDb.TransactionManager.StartTransaction())
            {
                string layerName = "LineLayer";
                LayerTable acLTable = acDelaunayTrans.GetObject(acDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (acLTable.Has(layerName) == false)
                {
                    using (Transaction acLayerTrans = acDb.TransactionManager.StartTransaction())
                    {
                        LayerTable acLTable2 = acLayerTrans.GetObject(acDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                        LayerTableRecord acLTRecord = new LayerTableRecord();
                        acLTRecord.Name = layerName;
                        acLTRecord.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);

                        acLTable2.UpgradeOpen();
                        acLTable2.Add(acLTRecord);
                        acLayerTrans.AddNewlyCreatedDBObject(acLTRecord, true);
                        acLayerTrans.Commit();
                    }

                }

                if (acLTable.Has(layerName) == true)
                    acDb.Clayer = acLTable[layerName];

                BlockTable acBLTable;
                acBLTable = acDelaunayTrans.GetObject(acDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBTRecord;
                acBTRecord = acDelaunayTrans.GetObject(acBLTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                int i;
                for (i = 0; i < EdgeList.Count; i++)
                {
                    Point3d startPoint = new Point3d(NodeList[EdgeList[i].Start].X, NodeList[EdgeList[i].Start].Y, NodeList[EdgeList[i].Start].Z);
                    Point3d endPoint = new Point3d(NodeList[EdgeList[i].End].X, NodeList[EdgeList[i].End].Y, NodeList[EdgeList[i].End].Z);

                    Line acLine = new Line(startPoint, endPoint);
                    acBTRecord.AppendEntity(acLine);
                    acDelaunayTrans.AddNewlyCreatedDBObject(acLine, true);
                }
                DocumentCollection acDC = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager;
                acDC.MdiActiveDocument.Editor.WriteMessage("\n Delaunay三角网绘制完毕！");

                acDelaunayTrans.Commit();
            }
            //Zoom(new Point3d(), new Point3d(), new Point3d(), 1.01075);
        }
        public void GetEdgeList()
        {
            double[] vectorA = new double[2];
            double[] vectorB = new double[2];
            vectorA[0] = 0; vectorA[1] = 100;    //用于计算两边夹角，且夹角始终小于或等于180°
            double minX = NodeList[0].X;
            int startIndex = 0, endIndex, tempIndex;
            for (int i = 1; i < NodeList.Count; i++)
            {
                if (minX > NodeList[i].X)
                {
                    minX = NodeList[i].X;
                    startIndex = i;
                }
            }
            mEdge edge = new mEdge();
            edge.Start = startIndex;
            endIndex = startIndex - 1;
            tempIndex = startIndex;

            double VALength, VBLength, tempCos, minCos = 1, minLength = double.MaxValue;
            while (endIndex != startIndex)
            {
                int j = 0;
                VALength = Math.Sqrt(vectorA[0] * vectorA[0] + vectorA[1] * vectorA[1]);
                minLength = double.MaxValue;
                for (j = 0; j < NodeList.Count; j++)  //边界
                {
                    if (j != edge.Start)
                    {
                        vectorB[0] = NodeList[j].X - NodeList[tempIndex].X;
                        vectorB[1] = NodeList[j].Y - NodeList[tempIndex].Y;
                        VBLength = Math.Sqrt(vectorB[0] * vectorB[0] + vectorB[1] * vectorB[1]);

                        tempCos = (vectorA[0] * vectorB[0] + vectorA[1] * vectorB[1]) / (VALength * VBLength);

                        if (minCos > tempCos)
                        {
                            minCos = tempCos;
                            edge.End = j;
                            minLength = VBLength;
                        }
                        else if (tempCos == minCos && VBLength < minLength)
                        {
                            edge.End = j;
                            minLength = VBLength;
                        }
                    }
                }
                EdgeList.Add(edge);
                endIndex = edge.End;
                edge = new mEdge();
                edge.Start = endIndex;
                minCos = 1;
                vectorA[0] = NodeList[tempIndex].X - NodeList[endIndex].X;
                vectorA[1] = NodeList[tempIndex].Y - NodeList[endIndex].Y;
                tempIndex = endIndex;
            }

            /*
            //画凸包
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database acDb = acDoc.Database;

            using (Transaction acTransLine = acDb.TransactionManager.StartTransaction())
            {
                string layerName = "LineLayer";
                using (Transaction acTransLineLayer = acDb.TransactionManager.StartTransaction())
                {
                    //创建线图层
                    LayerTable acLTable = acTransLineLayer.GetObject(acDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (acLTable.Has(layerName) == false)
                    {
                        LayerTableRecord acLTRecord = new LayerTableRecord();
                        acLTRecord.Name = layerName;
                        acLTRecord.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);  //7表示白色

                        acLTable.UpgradeOpen();
                        acLTable.Add(acLTRecord);
                        acTransLineLayer.AddNewlyCreatedDBObject(acLTRecord, true);
                    }
                    acTransLineLayer.Commit();
                }

                LayerTable acLTable2 = acTransLine.GetObject(acDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (acLTable2.Has(layerName) == true)
                {
                    acDb.Clayer = acLTable2[layerName];
                }
                BlockTable acBlkTbl;
                acBlkTbl = acTransLine.GetObject(acDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = acTransLine.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                int i, j;
                for (i = 0; i < EdgeList.Count; i++)
                {
                    j = i + 1;
                    if (j == EdgeList.Count)
                        j = 0;
                    Point3d StartPoint = new Point3d(NodeList[EdgeList[i].Start].X, NodeList[EdgeList[i].Start].Y, NodeList[EdgeList[i].Start].Z);
                    Point3d EndPoint = new Point3d(NodeList[EdgeList[i].End].X, NodeList[EdgeList[i].End].Y, NodeList[EdgeList[i].End].Z);
                    Line acLine = new Line(StartPoint, EndPoint);
                    acBlkTblRec.AppendEntity(acLine);
                    acTransLine.AddNewlyCreatedDBObject(acLine, true);
                }
                acTransLine.Commit();
            }
            Zoom(new Point3d(), new Point3d(), new Point3d(), 1.01075);
            */
        }
    }
}
