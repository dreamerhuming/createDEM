using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.EditorInput;

namespace DEM
{
    class DemClass
    {
        /// <summary>
        /// 绘点
        /// </summary>
        /// <param name="nodeList"></param>
        

        public void DrawTubao(List<mNode> nodeList, List<mEdge> edgeList)
        {
            List<mNode> tempNodeList = new List<mNode>();
            if (edgeList.Count != 0)
                edgeList.Clear();
            double[] vectorA = new double[2];
            double[] vectorB = new double[2];
            vectorA[0] = 0; vectorA[1] = 100;    //用于计算两边夹角，且夹角始终小于或等于180°
            double minX = nodeList[0].X;
            int startIndex = 0, endIndex, tempIndex;
            for (int i = 1; i < nodeList.Count; i++)
            {
                if (minX > nodeList[i].X)
                {
                    minX = nodeList[i].X;
                    startIndex = i;
                }
            }
            tempNodeList.Add(new mNode(nodeList[startIndex].N, nodeList[startIndex].X, nodeList[startIndex].Y, nodeList[startIndex].Z));    //最左边的点
            mEdge edge = new mEdge();
            edge.Start = startIndex;
            endIndex = -1;
            tempIndex = startIndex;

            double VALength, VBLength, tempCos, minCos = 1, minLength = double.MaxValue;
            while (endIndex != startIndex)
            {
                int j = 0;
                VALength = Math.Sqrt(vectorA[0] * vectorA[0] + vectorA[1] * vectorA[1]);
                minLength = double.MaxValue;
                for (j = 0; j < nodeList.Count; j++)  //边界
                {
                    if (j != edge.Start)
                    {
                        vectorB[0] = nodeList[j].X - nodeList[tempIndex].X;
                        vectorB[1] = nodeList[j].Y - nodeList[tempIndex].Y;
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
                edgeList.Add(edge);
                tempNodeList.Add(new mNode(edge.End, nodeList[edge.End].X, nodeList[edge.End].Y, nodeList[edge.End].Z));
                endIndex = edge.End;
                edge = new mEdge();
                edge.Start = endIndex;
                minCos = 1;
                vectorA[0] = nodeList[tempIndex].X - nodeList[endIndex].X;
                vectorA[1] = nodeList[tempIndex].Y - nodeList[endIndex].Y;
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
                for (i = 0; i < tempNodeList.Count; i++)
                {
                    j = i + 1;
                    if (j == tempNodeList.Count)
                        j = 0;
                    Point3d StartPoint = new Point3d(tempNodeList[i].X, tempNodeList[i].Y, tempNodeList[i].Z);
                    Point3d EndPoint = new Point3d(tempNodeList[j].X, tempNodeList[j].Y, tempNodeList[j].Z);
                    Line acLine = new Line(StartPoint, EndPoint);
                    acBlkTblRec.AppendEntity(acLine);
                    acTransLine.AddNewlyCreatedDBObject(acLine, true);
                }
                acTransLine.Commit();
            }
            Zoom(new Point3d(), new Point3d(), new Point3d(), 1.01075);
        }
        public void DrawDelaunay(List<mNode> nodeList, List<mEdge> edgeList, List<mTriangle> triList)
        {
            
        }

        
       
    }
}
