using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

[assembly: CommandClass(typeof(PartHandleAutocad.PartHandle))]

namespace PartHandleAutocad
{
    public class PartHandle
    {
        [CommandMethod("T1")]
        public void T1()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Transaction tx = db.TransactionManager.StartTransaction();

            string directFolder = AutocadUtil.DirectoryFolder(db); string docName = AutocadUtil.DocumentShortName(db);
            string layerName = string.Empty;

            string xmlfileName = Path.Combine(directFolder, docName + "_PrecastSchedule" + ".xml");
            XmlWriterSettings setting = new XmlWriterSettings();
            setting.Indent = true;
            XmlWriter writer = XmlWriter.Create(xmlfileName, setting);
            writer.WriteStartDocument(true);
            writer.WriteStartElement("PrecastSchedule");
            int count = 0;

            //Application.ShowAlertDialog(xmlfileName);
            PromptSelectionResult res = ed.GetSelection();
            if (res.Status != PromptStatus.OK)
            {
                Application.ShowAlertDialog("Cancel command!");
                tx.Commit();
                return;
            }

            SelectionSet ss = res.Value;
            foreach (SelectedObject ssObj in ss)
            {
                Entity ent = tx.GetObject(ssObj.ObjectId, OpenMode.ForRead) as Entity;
                layerName = ent.Layer;
                Application.ShowAlertDialog(layerName);
                break;
            }

            PromptPointResult pRes = ed.GetPoint("Pick origin point:");
            if (pRes.Status != PromptStatus.OK)
            {
                Application.ShowAlertDialog("Cancel command!");
                tx.Commit();
                return;
            }

            Point3d originPoint = pRes.Value;
            originPoint = new Point3d(originPoint.X, originPoint.Y, 0);

            res = ed.GetSelection();
            if (res.Status != PromptStatus.OK)
            {
                Application.ShowAlertDialog("Cancel command!");
                tx.Commit();
                return;
            }

            ss = res.Value;
            foreach (SelectedObject ssObj in ss)
            {
                Entity ent = tx.GetObject(ssObj.ObjectId, OpenMode.ForRead) as Entity;
                if (ent.Layer != layerName) continue;

                if (ent is Polyline)
                {
                    Polyline pl = ent as Polyline;
                    List<Point3d> p3ds = new List<Point3d>();
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        Point3d p3d = pl.GetPoint3dAt(i);
                        p3d = new Point3d(p3d.X, p3d.Y, 0);
                        Point3d tfP3d = new Point3d(p3d.X - originPoint.X, p3d.Y - originPoint.Y, p3d.Z - originPoint.Z);
                        p3ds.Add(tfP3d);
                    }
                    //p3ds.Sort(new ZYXComparer());
                    bool check = true;

                    //for (int i = 0; i < p3ds.Count - 1; i++)
                    //{
                    //    if (AutocadUtil.IsEqual(p3ds[i], p3ds[i + 1]))
                    //    {
                    //        check = true;
                    //        break;
                    //    }
                    //}
                    if (!pl.Closed)
                    {
                        if (p3ds[0] == p3ds[p3ds.Count - 1])
                        {
                            p3ds.RemoveAt(p3ds.Count - 1);
                        }
                        else check = false; 
                    }
                    if (check)
                    {
                        count++;
                        writer.WriteStartElement("PolyLine");
                        writer.WriteAttributeString("Number", count.ToString());
                        foreach (Point3d p3d in p3ds)
                        {
                            writer.WriteStartElement("Point");
                            writer.WriteAttributeString("Value", p3d.ToString());
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < p3ds.Count; i++)
                        {
                            sb.Append("p" + i + ":\t" + p3ds[i].ToString() + "\n");
                        }
                        Application.ShowAlertDialog(p3ds.Count.ToString());
                        Application.ShowAlertDialog(sb.ToString());
                    }
                }
                else if (ent is Polyline2d)
                {
                    Polyline2d pl = ent as Polyline2d;
                    List<Point3d> p3ds = new List<Point3d>();
                    foreach (ObjectId oI in pl)
                    {
                        Vertex2d e = tx.GetObject(oI, OpenMode.ForRead) as Vertex2d;
                        Point3d p3d = e.Position;
                        p3d = new Point3d(p3d.X, p3d.Y, 0);
                        Point3d tfP3d = new Point3d(p3d.X - originPoint.X, p3d.Y - originPoint.Y, p3d.Z - originPoint.Z);
                        p3ds.Add(tfP3d);
                    }
                    //p3ds.Sort(new ZYXComparer());
                    bool check = true;
                    //for (int i = 0; i < p3ds.Count - 1; i++)
                    //{
                    //    if (AutocadUtil.IsEqual(p3ds[i], p3ds[i + 1]))
                    //    {
                    //        check = true;
                    //        break;
                    //    }
                    //}
                    if (!pl.Closed)
                    {
                        if (p3ds[0] == p3ds[p3ds.Count - 1])
                        {
                            p3ds.RemoveAt(p3ds.Count - 1);
                        }
                        else check = false;
                    }
                    if (check)
                    {
                        count++;
                        writer.WriteStartElement("PolyLine");
                        writer.WriteAttributeString("Number", count.ToString());
                        foreach (Point3d p3d in p3ds)
                        {
                            writer.WriteStartElement("Point");
                            writer.WriteAttributeString("Value", p3d.ToString());
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < p3ds.Count; i++)
                        {
                            sb.Append("p" + i + ":\t" + p3ds[i].ToString() + "\n");
                        }
                        Application.ShowAlertDialog(p3ds.Count.ToString());
                        Application.ShowAlertDialog(sb.ToString());
                    }
                }
                else if (ent is Polyline3d)
                {
                    Polyline3d pl = ent as Polyline3d;
                    List<Point3d> p3ds = new List<Point3d>();
                    foreach (ObjectId oI in pl)
                    {
                        PolylineVertex3d v3d = tx.GetObject(oI, OpenMode.ForRead) as PolylineVertex3d;
                        Point3d p3d = v3d.Position;
                        p3d = new Point3d(p3d.X, p3d.Y, 0);
                        Point3d tfP3d = new Point3d(p3d.X - originPoint.X, p3d.Y - originPoint.Y, p3d.Z - originPoint.Z);
                        p3ds.Add(tfP3d);
                    }
                    //p3ds.Sort(new ZYXComparer());
                    bool check = true;
                    //for (int i = 0; i < p3ds.Count - 1; i++)
                    //{
                    //    if (AutocadUtil.IsEqual(p3ds[i], p3ds[i + 1]))
                    //    {
                    //        check = true;
                    //        break;
                    //    }
                    //}
                    if (!pl.Closed)
                    {
                        if (p3ds[0] == p3ds[p3ds.Count - 1])
                        {
                            p3ds.RemoveAt(p3ds.Count - 1);
                        }
                        else check = false;
                    }
                    if (check)
                    {
                        count++;
                        writer.WriteStartElement("PolyLine");
                        writer.WriteAttributeString("Number", count.ToString());
                        foreach (Point3d p3d in p3ds)
                        {
                            writer.WriteStartElement("Point");
                            writer.WriteAttributeString("Value", p3d.ToString());
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < p3ds.Count; i++)
                        {
                            sb.Append("p" + i + ":\t" + p3ds[i].ToString() + "\n");
                        }
                        Application.ShowAlertDialog(p3ds.Count.ToString());
                        Application.ShowAlertDialog(sb.ToString());
                    }
                }
            }



            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Close();

            tx.Commit();
        }
    }
}
