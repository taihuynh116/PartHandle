#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Xml;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace PartHandle
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        const string r = "Revit";
        public Result Execute(ExternalCommandData commandData,ref string message,ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            Transaction tx = new Transaction(doc);
            tx.Start("Part Handle");

            Reference rf = sel.PickObject(ObjectType.Face);
            Part part = doc.GetElement(rf) as Part;
            PlanarFace face = part.GetGeometryObjectFromReference(rf) as PlanarFace;

            IList<ElementId> intersectionElementIds = new List<ElementId>();

            ICollection<ElementId> partIds = new List<ElementId> { part.Id };
            List<Curve> cs = new List<Curve>();
            foreach (Element e in sel.PickElementsByRectangle())
            {
                DetailLine dl = e as DetailLine;
                if (dl == null) continue;
                cs.Add(dl.GeometryCurve);
            }
            //TaskDialog.Show(r, cs.Count.ToString());
            PartUtils.DivideParts(doc, partIds, intersectionElementIds, cs, SketchPlane.Create(doc, rf).Id);

            tx.Commit();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class T1 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,ref string message,ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            Reference r = null;
            r = sel.PickObject(ObjectType.Element);
            Wall wall = (r == null || r.ElementId == ElementId.InvalidElementId) ? null : doc.GetElement(r) as Wall;
            if (wall == null)
            {
                message = "unable to retrieve wall.";
                return Result.Failed;
            }

            LocationCurve location = wall.Location as LocationCurve;

            if (null == location)
            {
                message = "Unable to retrieve wall locaiotn curve.";
                return Result.Failed;
            }

            Line line = location.Curve as Line;
            if (null == line)
            {
                message = "Unable to retrieve wall location line.";
                return Result.Failed;
            }

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("Building panels");
                IList<ElementId> wallList = new List<ElementId>(1);
                wallList.Add(r.ElementId);
                
                if (PartUtils.AreElementsValidForCreateParts(doc, wallList))
                {
                    PartUtils.CreateParts(doc, wallList);
                    doc.Regenerate();

                    ICollection<ElementId> parts = PartUtils.GetAssociatedParts(doc, wall.Id, false, false);
                    if (PartUtils.ArePartsValidForDivide(doc, parts))
                    {
                        int divisions = 5;
                        XYZ origin = line.Origin;
                        XYZ delta = line.Direction.Multiply(line.Length / divisions);
                        Transform shiftDelta = Transform.CreateTranslation(delta);

                        Transform rotation = Transform.CreateRotationAtPoint(XYZ.BasisZ, 0.5 * Math.PI, origin);

                        XYZ wallWithVector = rotation.OfVector(line.Direction.Multiply( wall.Width*2));

                        Curve intersectionLine = Line.CreateBound(origin + wallWithVector, origin - wallWithVector);

                        IList<Curve> curveArray = new List<Curve>();

                        for (int i = 0; i < divisions; i++)
                        {
                            intersectionLine = intersectionLine.CreateTransformed(shiftDelta);
                            curveArray.Add(intersectionLine);
                        }

                        SketchPlane divisionSketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, line.Origin));
                        IList<ElementId> intersectionElementIds = new List<ElementId>();

                        PartUtils.DivideParts(doc, parts, intersectionElementIds, curveArray, divisionSketchPlane.Id);
                    }
                    doc.ActiveView.PartsVisibility = PartsVisibility.ShowPartsOnly;
                }
                transaction.Commit();
            }
            return Result.Succeeded;
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class T2 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            Transaction tx = new Transaction(doc, "PartHandle");
            tx.Start();

            Reference rf= sel.PickObject(ObjectType.Face,new PartSelection(doc));
            PlanarFace pf = doc.GetElement(rf).GetGeometryObjectFromReference(rf) as PlanarFace;
            UserPlane plane = CheckGeometry.GetPlane(pf);

            XYZ originPoint = sel.PickPoint();
            originPoint = CheckGeometry.GetProjectPoint(plane, originPoint);

            string xmlFileName = @"D:\1. Work\Code Project\1. Revit Addin\PartHandle\Test\Structure plan - V1_PrecastSchedule.xml";
            if (!File.Exists(xmlFileName))
            {
                message = "Database file doesn't exist!";
                return Result.Failed;
            }
            List<List<XYZ>> listPoints = new List<List<XYZ>>();
            XmlReader reader = XmlReader.Create(xmlFileName);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "PolyLine":listPoints.Add(new List<XYZ>());break;
                        case "Point":
                            {
                                int i = listPoints.Count - 1;
                                XYZ mmP = CheckGeometry.ConvertStringToXYZ(reader.GetAttribute("Value"));
                                XYZ convertP = new XYZ(GeomUtil.milimeter2Feet(mmP.X), GeomUtil.milimeter2Feet(mmP.Y), GeomUtil.milimeter2Feet(mmP.Z));
                                listPoints[i].Add(convertP + originPoint);
                                break;
                            }
                    }
                }
            }
            List<Curve> cs = new List<Curve>();
            foreach (List<XYZ> points in listPoints)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (i != points.Count - 1)
                    {
                        cs.Add(Line.CreateBound(points[i], points[i + 1]));
                    }
                    else
                    {
                        cs.Add(Line.CreateBound(points[i], points[0]));
                    }
                }
            }
            foreach (Curve c in cs)
            {
                CheckGeometry.CreateDetailLine(c, doc, doc.ActiveView);
            }

            //IList<ElementId> intersectionElementIds = new List<ElementId>();

            //PartUtils.DivideParts(doc, new List<ElementId> { rf.ElementId}, intersectionElementIds, cs, SketchPlane.Create(doc, rf).Id);

            tx.Commit();
            return Result.Succeeded;
        }
    }
    public class PartSelection : ISelectionFilter
    {
        Document doc;
        public PartSelection(Document doc)
        {
            this.doc = doc;
        }
        public bool AllowElement(Element elem)
        {
            if (elem is Part) return true;
            return false;
        }
        public bool AllowReference(Reference reference, XYZ position)
        {
            if (doc.GetElement(reference).GetGeometryObjectFromReference(reference) is PlanarFace) return true;
            return false;
        }
    }
    public class FloorSelection : ISelectionFilter
    {
        Document doc;
        public FloorSelection(Document doc)
        {
            this.doc = doc;
        }
        public bool AllowElement(Element elem)
        {
            if (elem is Floor) return true;
            return false;
        }
        public bool AllowReference(Reference reference, XYZ position)
        {
            if (doc.GetElement(reference).GetGeometryObjectFromReference(reference) is PlanarFace) return true;
            return false;
        }
    }
}
