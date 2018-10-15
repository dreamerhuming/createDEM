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
    public class Class1
    {
        //  [6/29/2015 胡明 1351200]
        #region 公共变量的声明
        List<mNode> NodeList = new List<mNode>();
        List<mEdge> EdgeList = new List<mEdge>();
        List<mTriangle> TriList = new List<mTriangle>();
        #endregion
        //画三角网和等高线命令DEM
        [CommandMethod("MyTest")]
        public void MyTest()
        {
            MyForm mf = new MyForm();
            
            mf.Show();
        }
        public void Say()
        {
            DocumentLock documentLock = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.LockDocument();


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

                Point3d startPoint = new Point3d(0,0,0);
                Point3d endPoint = new Point3d(100, 100, 100);
                Line acLine = new Line(startPoint, endPoint);
                acBTRecord.AppendEntity(acLine);
                acDelaunayTrans.AddNewlyCreatedDBObject(acLine, true);
       
                DocumentCollection acDC = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager;
                acDC.MdiActiveDocument.Editor.WriteMessage("\n Delaunay三角网绘制完毕！");

                acDelaunayTrans.Commit();
            }
            documentLock.Dispose();
        }
        [CommandMethod("DEM")]
        public void acDrawDEM()
        {
            if (NodeList.Count == 0)
            {
                OpenFile();
            }
            else
            {
                NodeList.Clear();
                EdgeList.Clear();
                TriList.Clear();
                OpenFile();
            }
            DrawDelaunay();
            DrawDEM();
        }
        //---画点命令DrawPoint
        [CommandMethod("DrawPoint")]
        public void acDrawPoint()
        {
            if (NodeList.Count==0)
            {
                OpenFile();
            }
            else
            {
                NodeList.Clear();
                EdgeList.Clear();
                TriList.Clear();
                OpenFile();
            }
            DrawPoint();
        }
        //---画凸包命令Tubao
        [CommandMethod("Tubao")]
        public void acDrawTuBao()
        {
            try
            {
                DrawTubao();
            }
            catch (System.Exception ex) { }
        }
        //画三角网命令Delaunay
        [CommandMethod("Delaunay")]
        public void acDrawDelaunay()
        {
            if (NodeList.Count==0)
            {
                OpenFile();
            }
            else
            {
                NodeList.Clear();
                EdgeList.Clear();
                TriList.Clear();
                OpenFile();
            }
            GetEdgeList();
            DrawDelaunay();
        }
        


        public void DrawDEM()
        {
            //--备份高程
            List<double> TempZ = new List<double>();
            for (int z = 0; z < NodeList.Count; z++)
                TempZ.Add(NodeList[z].Z);

            //--计算高程级数
            int EDist = GetEquiDistance();
            double maxElevation = 0, minElevation = double.MaxValue;
            for (int i = 0; i < NodeList.Count; i++)
            {
                if (maxElevation < NodeList[i].Z)
                    maxElevation = NodeList[i].Z;
                if (minElevation > NodeList[i].Z)
                    minElevation = NodeList[i].Z;
            }
            int SplineNum = (int)((maxElevation - minElevation) / EDist) + 1;
            double[] Elevation = new double[SplineNum];
            for (int i = 0; i < SplineNum; i++)
            {
                Elevation[i] = (double)((int)(minElevation + 1) + EDist * i);
            }

            //--每一高程的处理
            for (int i = 0; i < SplineNum; i++)
            {
                //若等值线在端点上，端点高程增加一微小量
                for (int j = 0; j < NodeList.Count; j++)
                {
                    if (TempZ[j] == Elevation[i])
                         TempZ[j] += 0.0001;
                }
                //初始化
                for (int j = 0; j < TriList.Count; j++)
                {
                    TriList[j].isValid = false;
                    TriList[j].isSearched = false;
                }
                //寻找符合高值要求的三角形
                for (int j = 0; j < TriList.Count; j++)
                {
                    //只要等值线过三角形的一条边，就会过另外一条边
                    if ((TempZ[TriList[j].NodeA] - Elevation[i]) * (TempZ[TriList[j].NodeB] - Elevation[i]) < 0 || (TempZ[TriList[j].NodeA] - Elevation[i]) * (TempZ[TriList[j].NodeC] - Elevation[i]) < 0)
                    {
                        TriList[j].isValid = true;
                    }
                }
                mEdge edge = new mEdge();
                mEdge edgeR = new mEdge(), edgeL = new mEdge();
                int indexR = -1, indexL = -1;
                int nextTriR, nextTriL;
                double tempX, tempY;
                mTriangle tri = new mTriangle();
                int FirstTriIndex;
                List<mNode> tempNodeList = new List<mNode>();
                for (int k = 0; k < TriList.Count; k++)
                {
                    if (TriList[k].isSearched == false && TriList[k].isValid == true)
                    {
                        bool isClosed = false;

                        tri = TriList[k];
                        FirstTriIndex = k;
                        //-----找到初始三角形的左右有效边
                        if ((TempZ[EdgeList[tri.EdgeA].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeA].End] - Elevation[i]) < 0)
                            indexR = tri.EdgeA;
                        else if ((TempZ[EdgeList[tri.EdgeB].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeB].End] - Elevation[i]) < 0)
                            indexR = tri.EdgeB;
                        else if ((TempZ[EdgeList[tri.EdgeC].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeC].End] - Elevation[i]) < 0)
                            indexR = tri.EdgeC;

                        if ((TempZ[EdgeList[tri.EdgeA].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeA].End] - Elevation[i]) < 0 && tri.EdgeA != indexR)
                            indexL = tri.EdgeA;
                        else if ((TempZ[EdgeList[tri.EdgeB].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeB].End] - Elevation[i]) < 0 && tri.EdgeB != indexR)
                            indexL = tri.EdgeB;
                        else if ((TempZ[EdgeList[tri.EdgeC].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeC].End] - Elevation[i]) < 0 && tri.EdgeC != indexR)
                            indexL = tri.EdgeC;

                        //indexR的下一个三角形索引
                        if (EdgeList[indexR].LeftTri == FirstTriIndex)
                            nextTriR = EdgeList[indexR].RightTri;
                        else
                            nextTriR = EdgeList[indexR].LeftTri;
                        //indexL的下一个三角形索引
                        if (EdgeList[indexL].RightTri == FirstTriIndex)
                            nextTriL = EdgeList[indexL].LeftTri;
                        else
                            nextTriL = EdgeList[indexL].RightTri;
                        edgeR = EdgeList[indexR];
                        tempX = NodeList[edgeR.Start].X + (NodeList[edgeR.End].X - NodeList[edgeR.Start].X) * (Elevation[i] - TempZ[edgeR.Start])
                                        / (TempZ[edgeR.End] - TempZ[edgeR.Start]);
                        tempY = NodeList[edgeR.Start].Y + (NodeList[edgeR.End].Y - NodeList[edgeR.Start].Y) * (Elevation[i] - TempZ[edgeR.Start])
                                / (TempZ[edgeR.End] - TempZ[edgeR.Start]);
                        tempNodeList.Add(new mNode(tempX, tempY, Elevation[i]));
                        TriList[FirstTriIndex].isSearched = true;
                        //------右边不为空时往右继续寻找
                        while (nextTriR != -1 && TriList[nextTriR].isSearched == false && TriList[nextTriR].isValid)  
                        {
                            //判断下一个三角形的indexR
                            tri = TriList[nextTriR];
                            if ((TempZ[EdgeList[tri.EdgeA].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeA].End] - Elevation[i]) < 0 && tri.EdgeA != indexR)
                                indexR = tri.EdgeA;
                            else if ((TempZ[EdgeList[tri.EdgeB].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeB].End] - Elevation[i]) < 0 && tri.EdgeB != indexR)
                                indexR = tri.EdgeB;
                            else if ((TempZ[EdgeList[tri.EdgeC].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeC].End] - Elevation[i]) < 0 && tri.EdgeC != indexR)
                                indexR = tri.EdgeC;

                            edgeR = EdgeList[indexR];
                            tempX = NodeList[edgeR.Start].X + (NodeList[edgeR.End].X - NodeList[edgeR.Start].X) * (Elevation[i] - TempZ[edgeR.Start])
                                        / (TempZ[edgeR.End] - TempZ[edgeR.Start]);
                            tempY = NodeList[edgeR.Start].Y + (NodeList[edgeR.End].Y - NodeList[edgeR.Start].Y) * (Elevation[i] - TempZ[edgeR.Start])
                                        / (TempZ[edgeR.End] - TempZ[edgeR.Start]);
                            if(tempNodeList.Contains(new mNode(tempX, tempY, Elevation[i]))==false)
                                tempNodeList.Add(new mNode(tempX, tempY, Elevation[i]));

                            TriList[nextTriR].isSearched = true;
                            //判断indexR的下一个三角形
                            if (EdgeList[indexR].LeftTri == nextTriR)
                                nextTriR = EdgeList[indexR].RightTri;
                            else
                                nextTriR = EdgeList[indexR].LeftTri;
                            //--判断如果追踪路线闭合的话
                            if (nextTriR == FirstTriIndex)
                            {
                                edgeR = EdgeList[indexL];   //闭合时，edgeR = edgeL
                                tempX = NodeList[edgeR.Start].X + (NodeList[edgeR.End].X - NodeList[edgeR.Start].X) * (Elevation[i] - TempZ[edgeR.Start])
                                            / (TempZ[edgeR.End] - TempZ[edgeR.Start]);
                                tempY = NodeList[edgeR.Start].Y + (NodeList[edgeR.End].Y - NodeList[edgeR.Start].Y) * (Elevation[i] - TempZ[edgeR.Start])
                                            / (TempZ[edgeR.End] - TempZ[edgeR.Start]);
                                if (tempNodeList.Contains(new mNode(tempX, tempY, Elevation[i])) == false)
                                    tempNodeList.Add(new mNode(tempX, tempY, Elevation[i]));
                                TriList[nextTriR].isSearched = true;
                                isClosed = true;
                                break;
                            }
                        }
                        //路线不闭合并且左边不为空时
                        if (isClosed == false)
                        {
                            edgeL = EdgeList[indexL];
                            tempX = NodeList[edgeL.Start].X + (NodeList[edgeL.End].X - NodeList[edgeL.Start].X) * (Elevation[i] - TempZ[edgeL.Start])
                                            / (TempZ[edgeL.End] - TempZ[edgeL.Start]);
                            tempY = NodeList[edgeL.Start].Y + (NodeList[edgeL.End].Y - NodeList[edgeL.Start].Y) * (Elevation[i] - TempZ[edgeL.Start])
                                    / (TempZ[edgeL.End] - TempZ[edgeL.Start]);
                            //将新生成的点插入原点列中
                            if (tempNodeList.Contains(new mNode(tempX, tempY, Elevation[i])) == false)
                                tempNodeList.Insert(0, new mNode(tempX, tempY, Elevation[i]));
                            TriList[FirstTriIndex].isSearched = true;

                            while (nextTriL != -1 && TriList[nextTriL].isSearched == false && TriList[nextTriL].isValid)  
                            {
                                tri = TriList[nextTriL];
                                if ((TempZ[EdgeList[tri.EdgeA].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeA].End] - Elevation[i]) < 0 && tri.EdgeA != indexL)
                                    indexL = tri.EdgeA;
                                else if ((TempZ[EdgeList[tri.EdgeB].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeB].End] - Elevation[i]) < 0 && tri.EdgeB != indexL)
                                    indexL = tri.EdgeB;
                                else if ((TempZ[EdgeList[tri.EdgeC].Start] - Elevation[i]) * (TempZ[EdgeList[tri.EdgeC].End] - Elevation[i]) < 0 && tri.EdgeC != indexL)
                                    indexL = tri.EdgeC;

                                edgeL = EdgeList[indexL];
                                tempX = NodeList[edgeL.Start].X + (NodeList[edgeL.End].X - NodeList[edgeL.Start].X) * (Elevation[i] - TempZ[edgeL.Start])
                                                / (TempZ[edgeL.End] - TempZ[edgeL.Start]);
                                tempY = NodeList[edgeL.Start].Y + (NodeList[edgeL.End].Y - NodeList[edgeL.Start].Y) * (Elevation[i] - TempZ[edgeL.Start])
                                                / (TempZ[edgeL.End] - TempZ[edgeL.Start]);
                                if (tempNodeList.Contains(new mNode(tempX, tempY, Elevation[i])) == false)
                                    tempNodeList.Insert(0, new mNode(tempX, tempY, Elevation[i]));
                                TriList[nextTriL].isSearched = true;

                                //indexL的下一个三角形索引
                                if (EdgeList[indexL].RightTri == FirstTriIndex)
                                    nextTriL = EdgeList[indexL].LeftTri;
                                else
                                    nextTriL = EdgeList[indexL].RightTri;
                            }
                        }
                        drawEach(tempNodeList, Elevation[i], isClosed);
                        tempNodeList.Clear();
                    }
                }
            }
        }
        //---绘制每一点表Spline
        private void drawEach(List<mNode> ElevList, double elevation, bool isClosed)
        {
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database acDb = acDoc.Database;

            using (Transaction acDemTrans = acDb.TransactionManager.StartTransaction())
            {
                string layerName = "DemLayer";
                LayerTable acLTable = acDemTrans.GetObject(acDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (acLTable.Has(layerName) == false)
                {
                    using (Transaction acLayerTrans = acDb.TransactionManager.StartTransaction())
                    {
                        LayerTable acLTable2 = acLayerTrans.GetObject(acDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                        LayerTableRecord acLTRecord = new LayerTableRecord();
                        acLTRecord.Name = layerName;
                        acLTRecord.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);

                        acLTable2.UpgradeOpen();
                        acLTable2.Add(acLTRecord);
                        acLayerTrans.AddNewlyCreatedDBObject(acLTRecord, true);
                        acLayerTrans.Commit();
                    }
                }

                if (acLTable.Has(layerName) == true)
                    acDb.Clayer = acLTable[layerName];

                BlockTable acBLTable;
                acBLTable = acDemTrans.GetObject(acDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBTRecord;
                acBTRecord = acDemTrans.GetObject(acBLTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                int drawingNum = ElevList.Count;
                Point3dCollection acPC = new Point3dCollection();
                if (isClosed)
                {
                    for (int s1 = 0; s1 < ElevList.Count; s1++)
                    {
                        acPC.Add(new Point3d(ElevList[s1].X, ElevList[s1].Y, elevation));
                    }
                    acPC.Add(new Point3d(ElevList[0].X, ElevList[0].Y, elevation));
                }
                else
                {
                    for (int s1 = 0; s1 < ElevList.Count; s1++)
                    {
                        acPC.Add(new Point3d(ElevList[s1].X, ElevList[s1].Y, elevation));
                    }
                }

                /*
                 * for (int s2 = 0; s2 < ElevList.Count-1; s2++)
                {
                    int b = s2 + 1;
                    Line acLine = new Line(new Point3d(ElevList[s2].X, ElevList[s2].Y, elevation), new Point3d(ElevList[b].X, ElevList[b].Y, elevation));
                    acBTRecord.AppendEntity(acLine);
                    acDemTrans.AddNewlyCreatedDBObject(acLine, true);
                }*/

                Polyline3d pline = new Polyline3d(Poly3dType.CubicSplinePoly, acPC, false);
                acBTRecord.AppendEntity(pline);
                acDemTrans.AddNewlyCreatedDBObject(pline, true);
                acDemTrans.Commit();
                //Spline acSpline = new Spline(acPC, 1, 0);
                //acSpline.ElevateDegree(3);  //三次曲线拟合
                //acBTRecord.AppendEntity(acSpline);
                //acDemTrans.AddNewlyCreatedDBObject(acSpline, true);
                //acDemTrans.Commit();
                
            }
        }
        //---绘制Delaunay三角网
        private void DrawDelaunay()
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
            Zoom(new Point3d(), new Point3d(), new Point3d(), 1.01075);
        }

        #region 初始化数据
        //---获取等高距
        private int GetEquiDistance()
        {
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            PromptIntegerOptions acIntOp = new PromptIntegerOptions("");
            acIntOp.Message = "\n输入等高距 (1--100): ";

            acIntOp.AllowZero = false;
            acIntOp.AllowNegative = false;
            acIntOp.LowerLimit = 1;
            acIntOp.UpperLimit = 100;
            acIntOp.DefaultValue = 10;
            acIntOp.Keywords.Add("5");
            acIntOp.Keywords.Add("15");
            acIntOp.Keywords.Add("20");
            acIntOp.AllowNone = true;

            PromptIntegerResult acIntRe = acDoc.Editor.GetInteger(acIntOp);
            if (acIntRe.Status == PromptStatus.Keyword)
            {
                return Convert.ToInt32(acIntRe.StringResult);
            }
            else
                return acIntRe.Value;
        }
        //---获取边界
        private void GetEdgeList()
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
        }
        //---打开文件
        private void OpenFile()
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
        //---绘点
        private void DrawPoint()
        {
            // Get the current document and database
            //获取当前文档及数据库
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            // Start a transaction启动事务
            using (Transaction acTransPoint = acCurDb.TransactionManager.StartTransaction())
            {
                using (Transaction acTransLayerPoint = acCurDb.TransactionManager.StartTransaction())
                {
                    LayerTable acLTable = acTransLayerPoint.GetObject(acCurDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    string layerName = "PointLayer";
                    if (acLTable.Has(layerName) == false)
                    {
                        LayerTableRecord acLTRecord = new LayerTableRecord();
                        acLTRecord.Name = layerName;
                        acLTRecord.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);          //颜色索引ByAci，2表示黄色

                        acLTable.UpgradeOpen();
                        acLTable.Add(acLTRecord);
                        acTransLayerPoint.AddNewlyCreatedDBObject(acLTRecord, true);
                    }
                    acTransLayerPoint.Commit();
                }

                //设置图层为点图层
                LayerTable acLTable2 = acTransPoint.GetObject(acCurDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                string LayerName = "PointLayer";
                if (acLTable2.Has(LayerName) == true)
                {
                    acCurDb.Clayer = acLTable2[LayerName];
                }

                BlockTable acBlkTbl;
                acBlkTbl = acTransPoint.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                //以写的方式打开模型空间记录
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = acTransPoint.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                int i;
                double dX, dY, dZ;
                for (i = 0; i < NodeList.Count; i++)
                {
                    dX = NodeList[i].X;
                    dY = NodeList[i].Y;
                    dZ = NodeList[i].Z;
                    DBPoint acPoint = new DBPoint(new Point3d(dX, dY, dZ));
                    acBlkTblRec.AppendEntity(acPoint);
                    acTransPoint.AddNewlyCreatedDBObject(acPoint, true);
                }
                acTransPoint.Commit();
            }
            Zoom(new Point3d(), new Point3d(), new Point3d(), 1.01075);
        }
        //---绘制凸包
        private void DrawTubao()
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
        }
        #endregion

        #region 缩放Zoom()函数
        static void Zoom(Point3d pMin, Point3d pMax, Point3d pCenter, double dFactor)
        {
            //获取当前文档及数据库
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            int nCurVport = System.Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CVPORT"));

            // 没提供点或只提供了一个中心点时，获取当前空间的范围
            // 检查当前空间是否为模型空间
            if (acCurDb.TileMode == true)
            {
                if (pMin.Equals(new Point3d()) == true &&
                    pMax.Equals(new Point3d()) == true)
                {
                    pMin = acCurDb.Extmin;
                    pMax = acCurDb.Extmax;
                }
            }
            else
            {
                // 检查当前空间是否为图纸空间
                if (nCurVport == 1)
                {
                    // 获取图纸空间范围
                    if (pMin.Equals(new Point3d()) == true &&
                        pMax.Equals(new Point3d()) == true)
                    {
                        pMin = acCurDb.Pextmin;
                        pMax = acCurDb.Pextmax;
                    }
                }
                else
                {
                    // 获取模型空间范围
                    if (pMin.Equals(new Point3d()) == true &&
                        pMax.Equals(new Point3d()) == true)
                    {
                        pMin = acCurDb.Extmin;
                        pMax = acCurDb.Extmax;
                    }
                }
            }
            // 启动事务
            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                // 获取当前视图
                using (ViewTableRecord acView = acDoc.Editor.GetCurrentView())
                {
                    Extents3d eExtents;

                    // 将WCS坐标变换为DCS坐标
                    Matrix3d matWCS2DCS;
                    matWCS2DCS = Matrix3d.PlaneToWorld(acView.ViewDirection);
                    matWCS2DCS = Matrix3d.Displacement(acView.Target - Point3d.Origin) * matWCS2DCS;
                    matWCS2DCS = Matrix3d.Rotation(-acView.ViewTwist,
                                                   acView.ViewDirection,
                                                   acView.Target) * matWCS2DCS;

                    //如果指定了中心点，就为中心模式和比例模式
                    //设置显示范围的最小点和最大点；
                    if (pCenter.DistanceTo(Point3d.Origin) != 0)
                    {
                        pMin = new Point3d(pCenter.X - (acView.Width / 2),
                                           pCenter.Y - (acView.Height / 2), 0);

                        pMax = new Point3d((acView.Width / 2) + pCenter.X,
                                           (acView.Height / 2) + pCenter.Y, 0);
                    }

                    // 用直线创建范围对象；
                    using (Line acLine = new Line(pMin, pMax))
                    {
                        eExtents = new Extents3d(acLine.Bounds.Value.MinPoint,
                                                 acLine.Bounds.Value.MaxPoint);
                    }

                    // 计算当前视图的宽高比
                    double dViewRatio;
                    dViewRatio = (acView.Width / acView.Height);

                    // 变换视图范围
                    matWCS2DCS = matWCS2DCS.Inverse();
                    eExtents.TransformBy(matWCS2DCS);

                    double dWidth;
                    double dHeight;
                    Point2d pNewCentPt;

                    //检查是否提供了中心点（中心模式和比例模式）
                    if (pCenter.DistanceTo(Point3d.Origin) != 0)
                    {
                        dWidth = acView.Width;
                        dHeight = acView.Height;

                        if (dFactor == 0)
                        {
                            pCenter = pCenter.TransformBy(matWCS2DCS);
                        }

                        pNewCentPt = new Point2d(pCenter.X, pCenter.Y);
                    }
                    else // 窗口、范围和界限模式下
                    {
                        // 计算当前视图的宽高新值；
                        dWidth = eExtents.MaxPoint.X - eExtents.MinPoint.X;
                        dHeight = eExtents.MaxPoint.Y - eExtents.MinPoint.Y;

                        // 获取视图中心点
                        pNewCentPt = new Point2d(((eExtents.MaxPoint.X + eExtents.MinPoint.X) * 0.5),
                                                 ((eExtents.MaxPoint.Y + eExtents.MinPoint.Y) * 0.5));
                    }

                    // 检查宽度新值是否适于当前窗口
                    if (dWidth > (dHeight * dViewRatio)) dHeight = dWidth / dViewRatio;

                    // 调整视图大小；
                    if (dFactor != 0)
                    {
                        acView.Height = dHeight * dFactor;
                        acView.Width = dWidth * dFactor;
                    }

                    // 设置视图中心；
                    acView.CenterPoint = pNewCentPt;

                    // 更新当前视图；
                    acDoc.Editor.SetCurrentView(acView);
                }

                // 提交更改；
                acTrans.Commit();
            }
        }
        #endregion
    }
}