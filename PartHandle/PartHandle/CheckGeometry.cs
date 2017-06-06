#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace PartHandle
{
    public static class CheckGeometry
    {
        public static UserPlane GetPlane(PlanarFace f)
        {
            return new UserPlane(f.Origin, f.XVector, f.YVector);
        }
        public static UserPlane GetPlaneWithBasisX(PlanarFace f, XYZ vecX)
        {
            if (!GeomUtil.IsEqual(GeomUtil.DotMatrix(vecX, f.FaceNormal), 0)) throw new Exception("VecX is not perpendicular with Normal!");
            return new UserPlane(f.Origin, vecX, GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecX, f.FaceNormal)));
        }
        public static UserPlane GetPlaneWithBasisY(PlanarFace f, XYZ vecY)
        {
            if (!GeomUtil.IsEqual(GeomUtil.DotMatrix(vecY, f.FaceNormal), 0)) throw new Exception("VecY is not perpendicular with Normal!");
            return new UserPlane(f.Origin, GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecY, f.FaceNormal)), vecY);
        }
        public static List<Curve> GetCurves(PlanarFace f)
        {
            List<Curve> curves = new List<Curve>();
            IList<CurveLoop> curveLoops = f.GetEdgesAsCurveLoops();
            foreach (CurveLoop cl in curveLoops)
            {
                foreach (Curve c in cl)
                {
                    curves.Add(c);
                }
                break;
            }
            return curves;
        }
        public static double GetSignedDistance(UserPlane plane, XYZ point)
        {
            XYZ v = point - plane.Origin;
            return Math.Abs(GeomUtil.DotMatrix(plane.Normal, v));
        }
        public static double GetSignedDistance(Line line, XYZ point)
        {
            if (IsPointInLineOrExtend(line, point)) return 0;
            return GeomUtil.GetLength(point, GetProjectPoint(line, point));
        }
        public static double GetSignedDistance(Curve line, XYZ point)
        {
            if (IsPointInLineOrExtend(ConvertLine(line), point)) return 0;
            return GeomUtil.GetLength(point, GetProjectPoint(line, point));
        }
        public static XYZ GetProjectPoint(Line line, XYZ point)
        {
            if (IsPointInLineOrExtend(line, point)) return point;
            XYZ vecL = GeomUtil.SubXYZ(line.GetEndPoint(1), line.GetEndPoint(0));
            XYZ vecP = GeomUtil.SubXYZ(point, line.GetEndPoint(0));
            UserPlane p = new UserPlane(line.GetEndPoint(0), GeomUtil.UnitVector(vecL), GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecL, vecP)));
            return GetProjectPoint(p, point);
        }
        public static XYZ GetProjectPoint(Curve line, XYZ point)
        {
            if (IsPointInLineOrExtend(CheckGeometry.ConvertLine(line), point)) return point;
            XYZ vecL = GeomUtil.SubXYZ(line.GetEndPoint(1), line.GetEndPoint(0));
            XYZ vecP = GeomUtil.SubXYZ(point, line.GetEndPoint(0));
            UserPlane p = new UserPlane(line.GetEndPoint(0), GeomUtil.UnitVector(vecL), GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecL, vecP)));
            return GetProjectPoint(p, point);
        }
        public static XYZ GetProjectPoint(UserPlane plane, XYZ point)
        {
            double d = GetSignedDistance(plane, point);
            XYZ q = GeomUtil.AddXYZ(point, GeomUtil.MultiplyVector(plane.Normal, d));
            return IsPointInPlane(plane, q) ? q : GeomUtil.AddXYZ(point, GeomUtil.MultiplyVector(plane.Normal, -d));
        }
        public static XYZ GetProjectPoint(PlanarFace f, XYZ point)
        {
            UserPlane p = GetPlane(f);
            return GetProjectPoint(p, point);
        }
        public static Curve GetProjectLine(UserPlane plane, Curve c)
        {
            return Line.CreateBound(GetProjectPoint(plane, c.GetEndPoint(0)), GetProjectPoint(plane, c.GetEndPoint(1)));
        }
        public static Polygon GetProjectPolygon(UserPlane plane, Polygon polygon)
        {
            List<Curve> cs = new List<Curve>();
            foreach (Curve c in polygon.ListCurve)
            {
                cs.Add(GetProjectLine(plane, c));
            }
            return new Polygon(cs);
        }
        public static UV Evaluate(UserPlane plane, XYZ point)
        {
            if (!IsPointInPlane(plane, point)) point = GetProjectPoint(plane, point);
            UserPlane planeOx = new UserPlane(plane.Origin, plane.XVector, plane.Normal);
            UserPlane planeOy = new UserPlane(plane.Origin, plane.YVector, plane.Normal);
            double lenX = GetSignedDistance(planeOy, point);
            double lenY = GetSignedDistance(planeOx, point);
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    double tLenX = lenX * Math.Pow(-1, i + 1);
                    double tLenY = lenY * Math.Pow(-1, j + 1);
                    XYZ tPoint = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(plane.Origin, plane.XVector, tLenX), plane.YVector, tLenY);
                    if (GeomUtil.IsEqual(tPoint, point)) return new UV(tLenX, tLenY);
                }
            }
            throw new Exception("Code complier should never be here!");
        }
        public static XYZ Evaluate(UserPlane p, UV point)
        {
            XYZ pnt = p.Origin;
            pnt = GeomUtil.OffsetPoint(pnt, p.XVector, point.U);
            pnt = GeomUtil.OffsetPoint(pnt, p.YVector, point.V);
            return pnt;
        }
        public static UV Evaluate(PlanarFace f, XYZ point)
        {
            return Evaluate(GetPlane(f), point);
        }
        public static XYZ Evaluate(PlanarFace f, UV point)
        {
            return f.Evaluate(point);
        }
        public static UV Evaluate(Polygon f, XYZ point)
        {
            return Evaluate(GetPlane(f.Face), point);
        }
        public static XYZ Evaluate(Polygon f, UV point)
        {
            return f.Face.Evaluate(point);
        }
        public static bool IsPointInPlane(UserPlane plane, XYZ point)
        {
            return GeomUtil.IsEqual(GetSignedDistance(plane, point), 0) ? true : false;
        }
        public static bool IsPointInPolygon(UV p, List<UV> polygon)
        {
            double minX = polygon[0].U;
            double maxX = polygon[0].U;
            double minY = polygon[0].V;
            double maxY = polygon[0].V;
            for (int i = 1; i < polygon.Count; i++)
            {
                UV q = polygon[i];
                minX = Math.Min(q.U, minX);
                maxX = Math.Max(q.U, maxX);
                minY = Math.Min(q.V, minY);
                maxY = Math.Max(q.V, maxY);
            }

            if (p.U < minX || p.U > maxX || p.V < minY || p.V > maxY)
            {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].V > p.V) != (polygon[j].V > p.V) &&
                     p.U < (polygon[j].U - polygon[i].U) * (p.V - polygon[i].V) / (polygon[j].V - polygon[i].V) + polygon[i].U)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
        public static bool IsPointInLine(Line line, XYZ point)
        {
            if (GeomUtil.IsEqual(point, line.GetEndPoint(0)) || GeomUtil.IsEqual(point, line.GetEndPoint(1))) return true;
            if (GeomUtil.IsOppositeDirection(GeomUtil.SubXYZ(point, line.GetEndPoint(0)), GeomUtil.SubXYZ(point, line.GetEndPoint(1)))) return true;
            return false;
        }
        public static bool IsPointInLineExtend(Line line, XYZ point)
        {
            if (GeomUtil.IsEqual(point, line.GetEndPoint(0)) || GeomUtil.IsEqual(point, line.GetEndPoint(1))) return true;
            if (GeomUtil.IsSameDirection(GeomUtil.SubXYZ(point, line.GetEndPoint(0)), GeomUtil.SubXYZ(point, line.GetEndPoint(1)))) return true;
            return false;
        }
        public static bool IsPointInLineOrExtend(Line line, XYZ point)
        {
            if (GeomUtil.IsEqual(point, line.GetEndPoint(0)) || GeomUtil.IsEqual(point, line.GetEndPoint(1))) return true;
            if (GeomUtil.IsSameOrOppositeDirection(GeomUtil.SubXYZ(point, line.GetEndPoint(0)), GeomUtil.SubXYZ(point, line.GetEndPoint(1)))) return true;
            return false;
        }
        public static PointComparePolygonResult PointComparePolygon(UV p, List<UV> polygon)
        {
            bool check1 = IsPointInPolygon(p, polygon);
            for (int i = 0; i < polygon.Count; i++)
            {
                if (GeomUtil.IsEqual(p, polygon[i])) return PointComparePolygonResult.Node;

                UV vec1 = GeomUtil.SubXYZ(p, polygon[i]);
                UV vec2 = null;
                if (i != polygon.Count - 1)
                {
                    if (GeomUtil.IsEqual(p, polygon[i + 1])) continue;
                    vec2 = GeomUtil.SubXYZ(p, polygon[i + 1]);
                }
                else
                {
                    if (GeomUtil.IsEqual(p, polygon[0])) continue;
                    vec2 = GeomUtil.SubXYZ(p, polygon[0]);
                }
                if (GeomUtil.IsOppositeDirection(vec1, vec2)) return PointComparePolygonResult.Boundary;
            }
            if (check1) return PointComparePolygonResult.Inside;
            return PointComparePolygonResult.Outside;
        }
        public static Line ConvertLine(Curve c)
        {
            return Line.CreateBound(c.GetEndPoint(0), c.GetEndPoint(1));
        }
        public static XYZ GetDirection(Curve c)
        {
            return GeomUtil.UnitVector(GeomUtil.SubXYZ(c.GetEndPoint(1), c.GetEndPoint(0)));
        }
        public static void CreateDetailLine(Curve c, Document doc, View v)
        {
            DetailLine dl = doc.Create.NewDetailCurve(v, c) as DetailLine;
        }
        public static void CreateDetailLinePolygon(Polygon pl, Document doc, View v)
        {
            foreach (Curve c in pl.ListCurve)
            {
                CreateDetailLine(c, doc, v);
            }
        }
        public static void CreateModelLine(Curve c, Document doc, UserPlane p)
        {
            Plane plane = p;
            ModelLine ml = doc.Create.NewModelCurve(c, SketchPlane.Create(doc, plane)) as ModelLine;
        }
        public static void CreateModelLinePolygon(Polygon pl, Document doc, UserPlane p)
        {
            foreach (Curve c in pl.ListCurve)
            {
                CreateModelLine(c, doc, p);
            }
        }
        public static bool CreateListPolygon(List<Curve> listCurve, out List<Polygon> pls)
        {
            pls = new List<Polygon>();
            foreach (Curve c in listCurve)
            {
                List<Curve> cs = new List<Curve>();
                cs.Add(Line.CreateBound(c.GetEndPoint(0), c.GetEndPoint(1)));
                int i = 0; bool check = true;
                while (!GeomUtil.IsEqual(cs[0].GetEndPoint(0), cs[cs.Count - 1].GetEndPoint(1)))
                {
                    i++;
                    foreach (Curve c1 in listCurve)
                    {
                        XYZ pnt = cs[cs.Count - 1].GetEndPoint(1);
                        XYZ prePnt = cs[cs.Count - 1].GetEndPoint(0);
                        if (GeomUtil.IsEqual(pnt, c1.GetEndPoint(0)))
                        {
                            if (GeomUtil.IsEqual(prePnt, c1.GetEndPoint(1)))
                            {
                                continue;
                            }
                            cs.Add(Line.CreateBound(c1.GetEndPoint(0), c1.GetEndPoint(1)));
                            break;
                        }
                        else if (GeomUtil.IsEqual(pnt, c1.GetEndPoint(1)))
                        {
                            if (GeomUtil.IsEqual(prePnt, c1.GetEndPoint(0)))
                            {
                                continue;
                            }
                            cs.Add(Line.CreateBound(c1.GetEndPoint(1), c1.GetEndPoint(0)));
                            break;
                        }
                        else continue;
                    }
                    if (i == 200) { check = false; break; }
                }
                if (check)
                {
                    Polygon plgon = new Polygon(cs);

                    if (pls.Count == 0) pls.Add(plgon);
                    else
                    {
                        check = true;
                        foreach (Polygon pl in pls)
                        {
                            if (pl == plgon) { check = false; break; }
                        }
                        if (check) pls.Add(plgon);
                    }
                }
            }
            if (pls.Count == 0) return false;
            return true;
        }
        public static Polygon GetPolygonFromFaceFamilyInstance(FamilyInstance fi)
        {
            GeometryElement geoElem = fi.get_Geometry(new Options { ComputeReferences = true });
            List<Curve> cs = new List<Curve>();
            foreach (GeometryObject geoObj in geoElem)
            {
                GeometryInstance geoIns = geoObj as GeometryInstance;
                if (geoIns == null) continue;
                Transform tf = geoIns.Transform;
                foreach (GeometryObject geoSymObj in geoIns.GetSymbolGeometry())
                {
                    Curve c = geoSymObj as Line;
                    if (c != null)
                        cs.Add(GeomUtil.TransformCurve(c, tf));
                }
            }
            if (cs.Count < 3) throw new Exception("Incorrect input curve!");
            return new Polygon(cs);
        }
        public static XYZ ConvertStringToXYZ(string pointString)
        {
            List<double> nums = new List<double>();
            foreach (string s in pointString.Split('(', ',', ' ', ')'))
            {
                double x = 0;
                if (double.TryParse(s, out x))
                {
                    
                    nums.Add(x);
                }
            }
            return new XYZ(nums[0], nums[1], nums[2]);
        }
        public static ViewSection CreateWallSection(Document linkedDoc, Document doc, ElementId id, string viewName, double offset)
        {
            Element e = linkedDoc.GetElement(id);
            if (!(e is Wall)) throw new Exception("Element is not a wall!");
            Wall wall = (Wall)e;
            Line line = (wall.Location as LocationCurve).Curve as Line;

            ViewFamilyType vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault<ViewFamilyType>(x => ViewFamily.Section == x.ViewFamily);

            XYZ p1 = line.GetEndPoint(0), p2 = line.GetEndPoint(1);
            List<XYZ> ps = new List<XYZ> { p1, p2 }; ps.Sort(new ZYXComparer());
            p1 = ps[0]; p2 = ps[1];

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            double minZ = bb.Min.Z, maxZ = bb.Max.Z;

            double l = GeomUtil.GetLength(GeomUtil.SubXYZ(p2, p1));
            double h = maxZ - minZ;
            double w = wall.WallType.Width;

            XYZ min = new XYZ(-l / 2 - offset, minZ - offset, -w - offset);
            XYZ max = new XYZ(l / 2 + offset, maxZ + offset, w + offset);

            Transform tf = Transform.Identity;
            tf.Origin = (p1 + p2) / 2;
            tf.BasisX = GeomUtil.UnitVector(p1 - p2);
            tf.BasisY = XYZ.BasisZ;
            tf.BasisZ = GeomUtil.CrossMatrix(tf.BasisX, tf.BasisY);

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ() { Transform = tf, Min = min, Max = max };
            ViewSection vs = ViewSection.CreateSection(doc, vft.Id, sectionBox);

            XYZ wallDir = GeomUtil.UnitVector(p2 - p1);
            XYZ upDir = XYZ.BasisZ;
            XYZ viewDir = GeomUtil.CrossMatrix(wallDir, upDir);

            min = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(p1, -wallDir, offset), -viewDir, offset);
            min = new XYZ(min.X, min.Y, minZ - offset);
            max = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(p2, wallDir, offset), viewDir, offset);
            max = new XYZ(max.X, max.Y, maxZ + offset);

            tf = vs.get_BoundingBox(null).Transform.Inverse;
            max = tf.OfPoint(max);
            min = tf.OfPoint(min);
            double maxx = 0, maxy = 0, maxz = 0, minx = 0, miny = 0, minz = 0;
            if (max.Z > min.Z)
            {
                maxz = max.Z;
                minz = min.Z;
            }
            else
            {
                maxz = min.Z;
                minz = max.Z;
            }


            if (Math.Round(max.X, 4) == Math.Round(min.X, 4))
            {
                maxx = max.X;
                minx = minz;
            }
            else if (max.X > min.X)
            {
                maxx = max.X;
                minx = min.X;
            }

            else
            {
                maxx = min.X;
                minx = max.X;
            }

            if (Math.Round(max.Y, 4) == Math.Round(min.Y, 4))
            {
                maxy = max.Y;
                miny = minz;
            }
            else if (max.Y > min.Y)
            {
                maxy = max.Y;
                miny = min.Y;
            }

            else
            {
                maxy = min.Y;
                miny = max.Y;
            }

            BoundingBoxXYZ sectionView = new BoundingBoxXYZ();
            sectionView.Max = new XYZ(maxx, maxy, maxz);
            sectionView.Min = new XYZ(minx, miny, minz);

            vs.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP).Set(ElementId.InvalidElementId);

            vs.get_Parameter(BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING).Set(0);

            vs.CropBoxActive = true;
            vs.CropBoxVisible = true;

            doc.Regenerate();

            vs.CropBox = sectionView;
            vs.Name = viewName;
            return vs;
        }
        public static ViewSection CreateWallSection(Document linkedDoc, Document doc, Polygon directPolygon, ElementId id, string viewName, double offset)
        {
            Element e = linkedDoc.GetElement(id);
            if (!(e is Wall)) throw new Exception("Element is not a wall!");
            Wall wall = (Wall)e;
            Line line = (wall.Location as LocationCurve).Curve as Line;

            ViewFamilyType vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault<ViewFamilyType>(x => ViewFamily.Section == x.ViewFamily);

            XYZ p1 = line.GetEndPoint(0), p2 = line.GetEndPoint(1);
            List<XYZ> ps = new List<XYZ> { p1, p2 }; ps.Sort(new ZYXComparer());
            p1 = ps[0]; p2 = ps[1];

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            double minZ = bb.Min.Z, maxZ = bb.Max.Z;

            double l = GeomUtil.GetLength(GeomUtil.SubXYZ(p2, p1));
            double h = maxZ - minZ;
            double w = wall.WallType.Width;

            XYZ tfMin = new XYZ(-l / 2 - offset, minZ - offset, -w - offset);
            XYZ tfMax = new XYZ(l / 2 + offset, maxZ + offset, w + offset);

            XYZ wallDir = GeomUtil.UnitVector(p2 - p1);
            XYZ upDir = XYZ.BasisZ;
            XYZ viewDir = GeomUtil.CrossMatrix(wallDir, upDir);

            XYZ midPoint = (p1 + p2) / 2;
            XYZ pMidPoint = GetProjectPoint(directPolygon.Plane, midPoint);

            XYZ pPnt = GeomUtil.OffsetPoint(pMidPoint, viewDir, w * 10);
            if (GeomUtil.IsBigger(GeomUtil.GetLength(pMidPoint, directPolygon.CentralXYZPoint), GeomUtil.GetLength(pPnt, directPolygon.CentralXYZPoint)))
            {
                wallDir = -wallDir;
                upDir = XYZ.BasisZ;
                viewDir = GeomUtil.CrossMatrix(wallDir, upDir);
            }
            else
            {

            }

            pPnt = GeomUtil.OffsetPoint(p1, wallDir, offset);
            XYZ min = null, max = null;
            if (GeomUtil.IsBigger(GeomUtil.GetLength(GeomUtil.SubXYZ(pPnt, midPoint)), GeomUtil.GetLength(GeomUtil.SubXYZ(p1, midPoint))))
            {
                min = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(p1, wallDir, offset), -viewDir, offset);
                max = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(p2, -wallDir, offset), viewDir, offset);
            }
            else
            {
                min = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(p1, -wallDir, offset), -viewDir, offset);
                max = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(p2, wallDir, offset), viewDir, offset);
            }
            min = new XYZ(min.X, min.Y, minZ - offset);
            max = new XYZ(max.X, max.Y, maxZ + offset);

            Transform tf = Transform.Identity;
            tf.Origin = (p1 + p2) / 2;
            tf.BasisX = wallDir;
            tf.BasisY = XYZ.BasisZ;
            tf.BasisZ = GeomUtil.CrossMatrix(wallDir, upDir);

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ() { Transform = tf, Min = tfMin, Max = tfMax };
            ViewSection vs = ViewSection.CreateSection(doc, vft.Id, sectionBox);

            tf = vs.get_BoundingBox(null).Transform.Inverse;
            max = tf.OfPoint(max);
            min = tf.OfPoint(min);
            double maxx = 0, maxy = 0, maxz = 0, minx = 0, miny = 0, minz = 0;
            if (max.Z > min.Z)
            {
                maxz = max.Z;
                minz = min.Z;
            }
            else
            {
                maxz = min.Z;
                minz = max.Z;
            }


            if (Math.Round(max.X, 4) == Math.Round(min.X, 4))
            {
                maxx = max.X;
                minx = minz;
            }
            else if (max.X > min.X)
            {
                maxx = max.X;
                minx = min.X;
            }

            else
            {
                maxx = min.X;
                minx = max.X;
            }

            if (Math.Round(max.Y, 4) == Math.Round(min.Y, 4))
            {
                maxy = max.Y;
                miny = minz;
            }
            else if (max.Y > min.Y)
            {
                maxy = max.Y;
                miny = min.Y;
            }

            else
            {
                maxy = min.Y;
                miny = max.Y;
            }

            BoundingBoxXYZ sectionView = new BoundingBoxXYZ();
            sectionView.Max = new XYZ(maxx, maxy, maxz);
            sectionView.Min = new XYZ(minx, miny, minz);

            vs.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP).Set(ElementId.InvalidElementId);

            vs.get_Parameter(BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING).Set(0);

            vs.CropBoxActive = true;
            vs.CropBoxVisible = true;

            doc.Regenerate();

            vs.CropBox = sectionView;
            vs.Name = viewName;
            return vs;
        }
        public static View CreateFloorCallout(Document doc, List<View> views, string level, BoundingBoxXYZ bb, string viewName, double offset)
        {
            ViewFamilyType vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>().FirstOrDefault<ViewFamilyType>(x => ViewFamily.FloorPlan == x.ViewFamily);
            XYZ max = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(bb.Max, XYZ.BasisX, offset), XYZ.BasisY, offset), XYZ.BasisZ, offset);
            XYZ min = GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(GeomUtil.OffsetPoint(bb.Min, -XYZ.BasisX, offset), -XYZ.BasisY, offset), -XYZ.BasisZ, offset);
            bb = new BoundingBoxXYZ { Max = max, Min = min };
            View pv = null;
            string s = string.Empty;
            bool check = false;
            foreach (View v in views)
            {
                try
                {
                    s = v.LookupParameter("Associated Level").AsString();
                    if (s == level) { pv = v; check = true; break; }
                }
                catch
                {
                    continue;
                }
            }
            if (!check) throw new Exception("Invalid level name!");
            View vs = ViewSection.CreateCallout(doc, pv.Id, vft.Id, min, max);
            vs.CropBox = bb;
            vs.Name = viewName;
            return vs;
        }
        public static string GetDirectoryPath(Document doc)
        {
            return Path.GetDirectoryName(doc.PathName);
        }
        public static string GetDirectoryPath(string documentName)
        {
            return Path.GetDirectoryName(documentName);
        }
        public static string CreateNameWithDocumentPathName(Document doc, string name, string exten)
        {
            string s = GetDirectoryPath(doc);
            string s1 = doc.PathName.Substring(s.Length + 1);
            return Path.Combine(s, s1.Substring(0, s1.Length - 4) + name + "." + exten);
        }
        public static string CreateNameWithDocumentPathName(string documentName, string name, string exten)
        {
            string s = GetDirectoryPath(documentName);
            string s1 = documentName.Substring(s.Length + 1);
            return Path.Combine(s, s1.Substring(0, s1.Length - 4) + name + "." + exten);
        }
        public static bool IsFileInUse(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("'path' cannot be null or empty.", "path");

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read)) { }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }
        public static string ConvertBoundingBoxToString(BoundingBoxXYZ bb)
        {
            return "{" + bb.Min.ToString() + ";" + bb.Max.ToString() + "}";
        }
        public static BoundingBoxXYZ ConvertStringToBoundingBox(string bbString)
        {
            BoundingBoxXYZ bb = new BoundingBoxXYZ();
            string[] ss = bbString.Split(';');
            ss[0] = ss[0].Substring(1, ss[0].Length - 1); ss[1] = ss[1].Substring(0, ss[1].Length - 1);
            bb.Min = ConvertStringToXYZ(ss[0]);
            bb.Max = ConvertStringToXYZ(ss[1]);
            return bb;
        }
        public static string ConvertPolygonToString(Polygon plgon)
        {
            string s = "{";
            for (int i = 0; i < plgon.ListXYZPoint.Count; i++)
            {
                if (i != plgon.ListXYZPoint.Count - 1)
                {
                    s += plgon.ListXYZPoint[i].ToString() + ";";
                }
                else
                {
                    s += plgon.ListXYZPoint[i].ToString() + "}";
                }
            }
            return s;
        }
        public static Polygon ConvertStringToPolygon(string bbString)
        {
            BoundingBoxXYZ bb = new BoundingBoxXYZ();
            string[] ss = bbString.Split(';');
            ss[0] = ss[0].Substring(1, ss[0].Length - 1);
            ss[ss.Length - 1] = ss[ss.Length - 1].Substring(0, ss[ss.Length - 1].Length - 1);
            List<XYZ> points = new List<XYZ>();
            foreach (string s in ss)
            {
                points.Add(ConvertStringToXYZ(s));
            }
            return new Polygon(points);
        }
        public static List<Curve> ConvertCurveLoopToCurveList(CurveLoop cl)
        {
            List<Curve> cs = new List<Curve>();
            foreach (Curve c in cl)
            {
                cs.Add(c);
            }
            return cs;
        }
        public static Polygon GetPolygonCut(Polygon mainPolygon, Polygon secPolygon)
        {
            PolygonComparePolygonResult res = new PolygonComparePolygonResult(mainPolygon, secPolygon);
            if (res.ListPolygon[0] != secPolygon) throw new Exception("Secondary Polygon must inside Main Polygon!");
            bool isInside = true;
            List<Curve> cs = new List<Curve>();
            foreach (Curve c in secPolygon.ListCurve)
            {
                LineComparePolygonResult lpRes = new LineComparePolygonResult(mainPolygon, CheckGeometry.ConvertLine(c));
                if (lpRes.Type == LineComparePolygonType.Inside)
                {
                    foreach (Curve c1 in mainPolygon.ListCurve)
                    {
                        LineCompareLineResult llres = new LineCompareLineResult(c, c1);
                        if (llres.Type == LineCompareLineType.SameDirectionLineOverlap)
                        {
                            goto Here;
                        }
                    }
                    cs.Add(c);
                }
            Here: continue;
            }
            isInside = false;
            foreach (Curve c in mainPolygon.ListCurve)
            {
                LineComparePolygonResult lpRes = new LineComparePolygonResult(secPolygon, CheckGeometry.ConvertLine(c));
                if (lpRes.Type == LineComparePolygonType.Outside)
                {
                    cs.Add(c);
                    continue;
                }
                foreach (Curve c1 in secPolygon.ListCurve)
                {
                    LineCompareLineResult llRes = new LineCompareLineResult(c, c1);
                    if (llRes.Type == LineCompareLineType.SameDirectionLineOverlap)
                    {
                        isInside = false;
                        if (llRes.ListOuterLine.Count == 0) break;
                        foreach (Line l in llRes.ListOuterLine)
                        {
                            LineComparePolygonResult lpRes1 = new LineComparePolygonResult(secPolygon, l);
                            if (lpRes1.Type != LineComparePolygonType.Inside)
                                cs.Add(l);
                        }
                        break;
                    }
                }
            }
            if (isInside) throw new Exception("Secondary Polygon must be tangential with Main Polygon!");
            return new Polygon(cs);
        }
        public static List<Curve> GetCurvesCut(Polygon mainPolygon, Polygon secPolygon)
        {
            PolygonComparePolygonResult res = new PolygonComparePolygonResult(mainPolygon, secPolygon);
            if (res.ListPolygon[0] != secPolygon) throw new Exception("Secondary Polygon must inside Main Polygon!");
            bool isInside = true;
            List<Curve> cs = new List<Curve>();
            foreach (Curve c in secPolygon.ListCurve)
            {
                LineComparePolygonResult lpRes = new LineComparePolygonResult(mainPolygon, CheckGeometry.ConvertLine(c));
                if (lpRes.Type == LineComparePolygonType.Inside)
                {
                    foreach (Curve c1 in mainPolygon.ListCurve)
                    {
                        LineCompareLineResult llres = new LineCompareLineResult(c, c1);
                        if (llres.Type == LineCompareLineType.SameDirectionLineOverlap)
                        {
                            goto Here;
                        }
                    }
                    cs.Add(c);
                }
            Here: continue;
            }
            isInside = false;
            foreach (Curve c in mainPolygon.ListCurve)
            {
                LineComparePolygonResult lpRes = new LineComparePolygonResult(secPolygon, CheckGeometry.ConvertLine(c));
                if (lpRes.Type == LineComparePolygonType.Outside)
                {
                    cs.Add(c);
                    continue;
                }
                foreach (Curve c1 in secPolygon.ListCurve)
                {
                    LineCompareLineResult llRes = new LineCompareLineResult(c, c1);
                    if (llRes.Type == LineCompareLineType.SameDirectionLineOverlap)
                    {
                        isInside = false;
                        if (llRes.ListOuterLine.Count == 0) break;
                        foreach (Line l in llRes.ListOuterLine)
                        {
                            LineComparePolygonResult lpRes1 = new LineComparePolygonResult(secPolygon, l);
                            if (lpRes1.Type != LineComparePolygonType.Inside)
                                cs.Add(l);
                        }
                        break;
                    }
                }
            }
            if (isInside) throw new Exception("Secondary Polygon must be tangential with Main Polygon!");
            return cs;
        }
        public static Transform GetTransform(Element e)
        {
            GeometryElement geoEle = e.get_Geometry(new Options() { ComputeReferences = true });
            foreach (GeometryObject geoObj in geoEle)
            {
                GeometryInstance geoIns = geoObj as GeometryInstance;
                if (geoIns != null)
                {
                    if (geoIns.Transform != null)
                    {
                        return geoIns.Transform;
                    }
                }
            }
            return null;
        }
        public static void InsertDetailItem(string familyName, XYZ location, Document doc, Transaction tx, View v, params string[] property_Values)
        {
            Family f = null;
            FilteredElementCollector col = new FilteredElementCollector(doc).OfClass(typeof(Family));
            string s = string.Empty;
            bool check = false;
            foreach (Element e in col)
            {
                if (e.Name == familyName)
                {
                    f = (Family)e;
                    check = true;
                    break;
                }
            }
            if (!check)
            {
                string filePath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                string directoryPath = Path.GetDirectoryName(filePath);
                string fullFamilyName = Path.Combine(directoryPath, familyName + ".rfa");
                doc.LoadFamily(fullFamilyName, out f);
            }

            FamilySymbol symbol = null;
            foreach (ElementId symbolId in f.GetFamilySymbolIds())
            {
                symbol = doc.GetElement(symbolId) as FamilySymbol;
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    //tx.Commit();
                    //tx.Start();
                }
                break;
            }
            FamilyInstance fi = doc.Create.NewFamilyInstance(location, symbol, v);

            for (int i = 0; i < property_Values.Length; i += 2)
            {
                fi.LookupParameter(property_Values[i]).Set(property_Values[i + 1]);
            }
        }
    }
    public class Polygon
    {
        public List<Curve> ListCurve { get; set; }
        public List<XYZ> ListXYZPoint { get; private set; }
        public List<UV> ListUVPoint { get; private set; }
        public XYZ XVector { get; private set; }
        public XYZ YVector { get; private set; }
        public XYZ XVecManual { get; private set; }
        public XYZ YVecManual { get; private set; }
        public UserPlane PlaneManual { get; private set; }
        public XYZ Normal { get; private set; }
        public XYZ Origin { get; private set; }
        public PlanarFace Face { get; set; }
        public UserPlane Plane { get; private set; }
        public XYZ CentralXYZPoint { get; private set; }
        public UV CentralUVPoint { get; private set; }
        public List<XYZ> TwoXYZPointsBoundary { get; private set; }
        public List<XYZ> TwoXYZPointsLimit { get; private set; }
        public List<UV> TwoUVPointsBoundary { get; private set; }
        public List<UV> TwoUVPointsLimit { get; private set; }
        public double Height { get; private set; }
        public double Width { get; private set; }
        public double Perimeter { get; private set; }
        public double Area { get; private set; }
        public Polygon(PlanarFace f)
        {
            this.Face = f;
            this.ListCurve = CheckGeometry.GetCurves(f);
            this.Plane = new UserPlane(Face.Origin, Face.XVector, Face.YVector);

            GetParameters();
        }
        public Polygon(List<Curve> cs)
        {
            this.ListCurve = new List<Curve>();
            int i = 0;
            ListCurve.Add(Line.CreateBound(cs[0].GetEndPoint(0), cs[0].GetEndPoint(1)));
            while (!GeomUtil.IsEqual(ListCurve[ListCurve.Count - 1].GetEndPoint(1), ListCurve[0].GetEndPoint(0)))
            {
                i++;
                foreach (Curve c in cs)
                {
                    XYZ pnt = ListCurve[ListCurve.Count - 1].GetEndPoint(1);
                    XYZ prePnt = ListCurve[ListCurve.Count - 1].GetEndPoint(0);
                    if (GeomUtil.IsEqual(pnt, c.GetEndPoint(0)))
                    {
                        if (GeomUtil.IsEqual(prePnt, c.GetEndPoint(1)))
                        {
                            continue;
                        }
                        ListCurve.Add(Line.CreateBound(c.GetEndPoint(0), c.GetEndPoint(1)));
                        break;
                    }
                    else if (GeomUtil.IsEqual(pnt, c.GetEndPoint(1)))
                    {
                        if (GeomUtil.IsEqual(prePnt, c.GetEndPoint(0)))
                        {
                            continue;
                        }
                        ListCurve.Add(Line.CreateBound(c.GetEndPoint(1), c.GetEndPoint(0)));
                        break;
                    }
                    else continue;
                }
                if (i == 200) throw new Exception("Error when creating polygon");
            }
            XYZ origin = ListCurve[0].GetEndPoint(0);
            XYZ vecX = GeomUtil.UnitVector(CheckGeometry.GetDirection(cs[0]));
            XYZ vecT = GeomUtil.UnitVector(CheckGeometry.GetDirection(ListCurve[ListCurve.Count - 1]));
            XYZ normal = GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecX, vecT));
            XYZ vecY = GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecX, normal));
            this.Plane = new UserPlane(origin, vecX, vecY);

            GetParameters();
        }
        public Polygon(List<XYZ> points)
        {
            List<Curve> cs = new List<Curve>();
            for (int i = 0; i < points.Count; i++)
            {
                if (i < points.Count - 1)
                {
                    cs.Add(Line.CreateBound(points[i], points[i + 1]));
                }
                else
                {
                    cs.Add(Line.CreateBound(points[i], points[0]));
                }
            }

            this.ListCurve = cs;

            XYZ origin = cs[0].GetEndPoint(0);
            XYZ vecX = GeomUtil.UnitVector(CheckGeometry.GetDirection(cs[0]));
            XYZ vecT = GeomUtil.UnitVector(CheckGeometry.GetDirection(ListCurve[ListCurve.Count - 1]));
            XYZ normal = GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecX, vecT));
            XYZ vecY = GeomUtil.UnitVector(GeomUtil.CrossMatrix(vecX, normal));
            this.Plane = new UserPlane(origin, vecX, vecY);

            GetParameters();
        }
        private void GetParameters()
        {
            List<XYZ> points = new List<XYZ>();
            foreach (Curve c in this.ListCurve)
            {
                points.Add(c.GetEndPoint(0));
            }
            this.ListXYZPoint = points;

            List<UV> uvpoints = new List<UV>();
            CentralXYZPoint = new XYZ(0, 0, 0);
            foreach (XYZ p in ListXYZPoint)
            {
                uvpoints.Add(CheckGeometry.Evaluate(this.Plane, p));
                CentralXYZPoint = GeomUtil.AddXYZ(CentralXYZPoint, p);
            }
            this.ListUVPoint = uvpoints;
            this.CentralXYZPoint = new XYZ(CentralXYZPoint.X / ListXYZPoint.Count, CentralXYZPoint.Y / ListXYZPoint.Count, CentralXYZPoint.Z / ListXYZPoint.Count);
            this.CentralUVPoint = CheckGeometry.Evaluate(this.Plane, CentralXYZPoint);
            this.XVector = this.Plane.XVector; this.YVector = this.Plane.YVector; this.Normal = this.Plane.Normal; this.Origin = this.Plane.Origin;

            GetPerimeter(); GetArea();
        }
        private void GetArea()
        {
            int j;
            double area = 0;

            for (int i = 0; i < ListUVPoint.Count; i++)
            {
                j = (i + 1) % ListUVPoint.Count;

                area += ListUVPoint[i].U * ListUVPoint[j].V;
                area -= ListUVPoint[i].V * ListUVPoint[j].U;
            }

            area /= 2;
            this.Area = (area < 0 ? -area : area);
        }
        private void GetPerimeter()
        {
            double len = 0;
            foreach (Curve c in ListCurve)
            {
                len += GeomUtil.GetLength(CheckGeometry.ConvertLine(c));
            }
            this.Perimeter = len;
        }
        public void SetManualDirection(XYZ vec, bool isXVector = true)
        {
            if (!GeomUtil.IsEqual(GeomUtil.DotMatrix(vec, Normal), 0)) throw new Exception("Input vector is not perpendicular with Normal!");
            XYZ xvec = null, yvec = null;
            if (isXVector)
            {
                xvec = GeomUtil.UnitVector(vec);
                yvec = GeomUtil.UnitVector(GeomUtil.CrossMatrix(xvec, this.Normal));
            }
            else
            {
                yvec = GeomUtil.UnitVector(vec);
                xvec = GeomUtil.UnitVector(GeomUtil.CrossMatrix(yvec, this.Normal));
            }
            this.XVecManual = GeomUtil.IsBigger(xvec, -xvec) ? xvec : -xvec;
            this.YVecManual = GeomUtil.IsBigger(yvec, -yvec) ? yvec : -yvec;
            this.PlaneManual = new UserPlane(CentralXYZPoint, XVecManual, YVecManual);
        }
        public void SetTwoPointsBoundary(XYZ vec, bool isXVector = true)
        {
            SetManualDirection(vec, isXVector);
            double maxU = 0, maxV = 0;
            foreach (XYZ xyzP in ListXYZPoint)
            {
                UV uvP = CheckGeometry.Evaluate(this.PlaneManual, xyzP);
                if (GeomUtil.IsBigger(Math.Abs(uvP.U), maxU)) maxU = Math.Abs(uvP.U);
                if (GeomUtil.IsBigger(Math.Abs(uvP.V), maxV)) maxV = Math.Abs(uvP.V);
            }
            UV uvboundP = new UV(-maxU, -maxV);
            XYZ p1 = CheckGeometry.Evaluate(this.PlaneManual, uvboundP), p2 = CheckGeometry.Evaluate(this.PlaneManual, -uvboundP);
            TwoXYZPointsBoundary = new List<XYZ> { p1, p2 };
            TwoUVPointsBoundary = new List<UV> { CheckGeometry.Evaluate(this.Plane, p1), CheckGeometry.Evaluate(this.Plane, p2) };
        }
        public void SetTwoPointsLimit(XYZ vec, bool isXVector = true)
        {
            SetManualDirection(vec, isXVector);
            double maxU = 0, maxV = 0, minU = 0, minV = 0;
            foreach (XYZ xyzP in ListXYZPoint)
            {
                UV uvP = CheckGeometry.Evaluate(this.PlaneManual, xyzP);
                if (GeomUtil.IsBigger(uvP.U, maxU)) maxU = uvP.U;
                if (GeomUtil.IsBigger(uvP.V, maxV)) maxV = uvP.V;
                if (GeomUtil.IsSmaller(uvP.U, minU)) minU = uvP.U;
                if (GeomUtil.IsSmaller(uvP.V, minV)) minV = uvP.V;
            }
            UV min = new UV(minU, minV), max = new UV(maxU, maxV);
            TwoUVPointsLimit = new List<UV> { min, max };
            XYZ p1 = CheckGeometry.Evaluate(this.PlaneManual, min), p2 = CheckGeometry.Evaluate(this.PlaneManual, max);
            TwoXYZPointsLimit = new List<XYZ> { p1, p2 };
        }
        public void SetTwoDimension(XYZ vec, bool isXVector = true)
        {
            SetManualDirection(vec, isXVector);
            double maxU = 0, maxV = 0;
            List<UV> uvPs = new List<UV>();
            foreach (XYZ xyzP in ListXYZPoint)
            {
                uvPs.Add(CheckGeometry.Evaluate(this.PlaneManual, xyzP));
            }
            for (int i = 0; i < uvPs.Count; i++)
            {
                for (int j = i + 1; j < uvPs.Count; j++)
                {
                    if (GeomUtil.IsBigger(Math.Abs(uvPs[i].U - uvPs[j].U), maxU)) maxU = Math.Abs(uvPs[i].U - uvPs[j].U);
                    if (GeomUtil.IsBigger(Math.Abs(uvPs[i].V - uvPs[j].V), maxV)) maxV = Math.Abs(uvPs[i].V - uvPs[j].V);
                }
            }
            this.Width = maxU;
            this.Height = maxV;
        }
        public bool IsPointInPolygon(UV p)
        {
            List<UV> polygon = this.ListUVPoint;
            double minX = polygon[0].U;
            double maxX = polygon[0].U;
            double minY = polygon[0].V;
            double maxY = polygon[0].V;
            for (int i = 1; i < polygon.Count; i++)
            {
                UV q = polygon[i];
                minX = Math.Min(q.U, minX);
                maxX = Math.Max(q.U, maxX);
                minY = Math.Min(q.V, minY);
                maxY = Math.Max(q.V, maxY);
            }

            if (p.U < minX || p.U > maxX || p.V < minY || p.V > maxY)
            {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].V > p.V) != (polygon[j].V > p.V) &&
                     p.U < (polygon[j].U - polygon[i].U) * (p.V - polygon[i].V) / (polygon[j].V - polygon[i].V) + polygon[i].U)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
        public bool IsPointInPolygonNewCheck(UV p)
        {
            List<UV> polygon = this.ListUVPoint;
            double minX = polygon[0].U;
            double maxX = polygon[0].U;
            double minY = polygon[0].V;
            double maxY = polygon[0].V;
            for (int i = 1; i < polygon.Count; i++)
            {
                UV q = polygon[i];
                minX = Math.Min(q.U, minX);
                maxX = Math.Max(q.U, maxX);
                minY = Math.Min(q.V, minY);
                maxY = Math.Max(q.V, maxY);
            }

            if (!GeomUtil.IsBigger(p.U, minX) || !GeomUtil.IsSmaller(p.U, maxX) || !GeomUtil.IsBigger(p.V, minY) || !GeomUtil.IsSmaller(p.V, maxY))
            {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((!GeomUtil.IsSmaller(polygon[i].V, p.V) != (!GeomUtil.IsSmaller(polygon[j].V, p.V)) &&
                     !GeomUtil.IsBigger(p.U, (polygon[j].U - polygon[i].U) * (p.V - polygon[i].V) / (polygon[j].V - polygon[i].V) + polygon[i].U)))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
        public bool IsLineInPolygon(Line l)
        {
            XYZ p1 = l.GetEndPoint(0);
            if (CheckXYZPointPosition(l.GetEndPoint(0)) == PointComparePolygonResult.NonPlanar || CheckXYZPointPosition(l.GetEndPoint(0)) == PointComparePolygonResult.NonPlanar) return false;
            double len = GeomUtil.GetLength(l);
            XYZ dir = GeomUtil.UnitVector(l.Direction);
            if (GeomUtil.IsEqual(l.GetEndPoint(1), GeomUtil.OffsetPoint(p1, dir, len)))
            {
            }
            else if (GeomUtil.IsEqual(l.GetEndPoint(1), GeomUtil.OffsetPoint(p1, -dir, len)))
            {
                dir = -dir;
            }
            else throw new Exception("Error when retrieve result!");

            for (int i = 0; i <= 100; i++)
            {
                XYZ p = GeomUtil.OffsetPoint(p1, dir, len / 100 * i);
                if (CheckXYZPointPosition(p) == PointComparePolygonResult.Outside) return false;
            }
            return true;
        }
        public bool IsLineOutPolygon(Line l)
        {
            XYZ p1 = l.GetEndPoint(0);
            if (CheckXYZPointPosition(l.GetEndPoint(0)) == PointComparePolygonResult.NonPlanar || CheckXYZPointPosition(l.GetEndPoint(0)) == PointComparePolygonResult.NonPlanar) return false;
            double len = GeomUtil.GetLength(l);
            XYZ vec = GeomUtil.IsEqual(GeomUtil.AddXYZ(p1, GeomUtil.MultiplyVector(l.Direction, len)), l.GetEndPoint(1)) ? l.Direction : -l.Direction;
            int count = 0;
            for (int i = 0; i <= 100; i++)
            {
                XYZ p = GeomUtil.OffsetPoint(p1, vec, len / 100 * i);
                if (CheckXYZPointPosition(p) != PointComparePolygonResult.Outside)
                {
                    if (CheckXYZPointPosition(p) == PointComparePolygonResult.Inside) return false;
                    count++;
                }
            }
            if (count > 2) return false;
            return true;
        }
        public PointComparePolygonResult CheckUVPointPosition(UV p)
        {
            List<UV> polygon = this.ListUVPoint;
            bool check1 = IsPointInPolygon(p);
            for (int i = 0; i < polygon.Count; i++)
            {
                if (GeomUtil.IsEqual(p, polygon[i])) return PointComparePolygonResult.Node;

                UV vec1 = GeomUtil.SubXYZ(p, polygon[i]);
                UV vec2 = null;
                if (i != polygon.Count - 1)
                {
                    if (GeomUtil.IsEqual(p, polygon[i + 1])) continue;
                    vec2 = GeomUtil.SubXYZ(p, polygon[i + 1]);
                }
                else
                {
                    if (GeomUtil.IsEqual(p, polygon[0])) continue;
                    vec2 = GeomUtil.SubXYZ(p, polygon[0]);
                }
                if (GeomUtil.IsOppositeDirection(vec1, vec2)) return PointComparePolygonResult.Boundary;
            }
            if (check1) return PointComparePolygonResult.Inside;
            return PointComparePolygonResult.Outside;
        }
        public PointComparePolygonResult CheckXYZPointPosition(XYZ p)
        {
            if (!GeomUtil.IsEqual(CheckGeometry.GetSignedDistance(this.Plane, p), 0)) return PointComparePolygonResult.NonPlanar;
            UV uvP = Evaluate(p);
            return CheckUVPointPosition(uvP);
        }
        public XYZ Evaluate(UV p) { return CheckGeometry.Evaluate(this.Plane, p); }
        public UV Evaluate(XYZ p) { return CheckGeometry.Evaluate(this.Plane, p); }
        public XYZ GetTopDirectionFromCurve()
        {
            List<XYZ> vecs = new List<XYZ>();
            foreach (Curve c in this.ListCurve)
            {
                XYZ vec = GeomUtil.UnitVector(CheckGeometry.GetDirection(c));
                vec = GeomUtil.IsBigger(vec, -vec) ? vec : -vec;
                vecs.Add(vec);
            }
            vecs.Sort(new ZYXComparer());
            return vecs[vecs.Count - 1];
        }
        public void OffsetPolygon(XYZ direction, double distance)
        {
            for (int i = 0; i < ListCurve.Count; i++)
            {
                ListCurve[i] = GeomUtil.OffsetCurve(ListCurve[i], direction, distance);
            }
        }
        public static bool operator ==(Polygon pl1, Polygon pl2)
        {
            try
            {
                List<XYZ> points = pl1.ListXYZPoint;
            }
            catch
            {
                try
                {
                    List<XYZ> points = pl2.ListXYZPoint;
                    return false;
                }
                catch
                {
                    return true;
                }
            }
            try
            {
                List<XYZ> points = pl2.ListXYZPoint;
            }
            catch
            {
                return false;
            }
            List<XYZ> pnts1 = pl1.ListXYZPoint, pnts2 = pl2.ListXYZPoint;
            pnts1.Sort(new ZYXComparer()); pnts2.Sort(new ZYXComparer());
            for (int i = 0; i < pnts1.Count; i++)
            {
                if (!GeomUtil.IsEqual(pnts1[i], pnts2[i])) return false;
            }
            return true;
        }
        public static bool operator !=(Polygon pl1, Polygon pl2)
        {
            return !(pl1 == pl2);
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public enum PointComparePolygonResult
    {
        Inside, Outside, Boundary, Node, NonPlanar
    }
    public class LineCompareLineResult
    {
        public LineCompareLineType Type { get; private set; }
        public Line Line { get; private set; }
        public List<Line> ListOuterLine { get; private set; }
        public Line MergeLine { get; private set; }
        public XYZ Point { get; private set; }
        private Line line1;
        private Line line2;
        public LineCompareLineResult(Line l1, Line l2)
        {
            this.line1 = l1; this.line2 = l2; GetParameter();
        }
        public LineCompareLineResult(Curve l1, Curve l2)
        {
            this.line1 = Line.CreateBound(l1.GetEndPoint(0), l1.GetEndPoint(1));
            this.line2 = Line.CreateBound(l2.GetEndPoint(0), l2.GetEndPoint(1));
            GetParameter();
        }
        private void GetParameter()
        {
            XYZ vec1 = line1.Direction, vec2 = line2.Direction;
            if (GeomUtil.IsSameOrOppositeDirection(vec1, vec2))
            {
                #region SameDirection
                if (GeomUtil.IsEqual(CheckGeometry.GetSignedDistance(line1, line2.GetEndPoint(0)), 0))
                {
                    if (GeomUtil.IsEqual(line1.GetEndPoint(0), line2.GetEndPoint(0)))
                    {
                        if (GeomUtil.IsOppositeDirection(GeomUtil.SubXYZ(line1.GetEndPoint(1), line1.GetEndPoint(0)), GeomUtil.SubXYZ(line2.GetEndPoint(1), line2.GetEndPoint(0))))
                        {
                            this.Point = line1.GetEndPoint(0);
                            this.MergeLine = Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(1));
                            this.Type = LineCompareLineType.SameDirectionPointOverlap; return;
                        }
                        if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(1)))
                        {
                            this.Line = line1;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        else
                        {
                            this.Line = line2;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        ListOuterLine = new List<Line>();
                        try
                        {
                            Line l = Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(1));
                            ListOuterLine = new List<Line>() { l };
                        }
                        catch
                        { }
                        return;
                    }
                    else if (GeomUtil.IsEqual(line1.GetEndPoint(1), line2.GetEndPoint(0)))
                    {
                        if (GeomUtil.IsOppositeDirection(GeomUtil.SubXYZ(line1.GetEndPoint(0), line1.GetEndPoint(1)), GeomUtil.SubXYZ(line2.GetEndPoint(1), line2.GetEndPoint(0))))
                        {
                            this.Point = line1.GetEndPoint(1);
                            this.MergeLine = Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(1));
                            this.Type = LineCompareLineType.SameDirectionPointOverlap; return;
                        }
                        if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(0)))
                        {
                            this.Line = line1;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        else
                        {
                            this.Line = line2;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        ListOuterLine = new List<Line>();
                        try
                        {
                            Line l = Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(1));
                            ListOuterLine = new List<Line>() { l };
                        }
                        catch
                        { }
                        return;
                    }
                    else if (GeomUtil.IsEqual(line1.GetEndPoint(1), line2.GetEndPoint(1)))
                    {
                        if (GeomUtil.IsOppositeDirection(GeomUtil.SubXYZ(line1.GetEndPoint(1), line1.GetEndPoint(0)), GeomUtil.SubXYZ(line2.GetEndPoint(1), line2.GetEndPoint(0))))
                        {
                            this.Point = line1.GetEndPoint(1);
                            this.MergeLine = Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(0));
                            this.Type = LineCompareLineType.SameDirectionPointOverlap; return;
                        }
                        if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(0)))
                        {
                            this.Line = line1;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        else
                        {
                            this.Line = line2;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        ListOuterLine = new List<Line>();
                        try
                        {
                            Line l = Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(0));
                            ListOuterLine = new List<Line>() { l };
                        }
                        catch
                        { }
                        return;
                    }
                    else if (GeomUtil.IsEqual(line1.GetEndPoint(0), line2.GetEndPoint(1)))
                    {
                        if (GeomUtil.IsOppositeDirection(GeomUtil.SubXYZ(line1.GetEndPoint(0), line1.GetEndPoint(1)), GeomUtil.SubXYZ(line2.GetEndPoint(1), line2.GetEndPoint(0))))
                        {
                            this.Point = line1.GetEndPoint(0);
                            this.MergeLine = Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(0));
                            this.Type = LineCompareLineType.SameDirectionPointOverlap; return;
                        }
                        if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(1)))
                        {
                            this.Line = line1;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        else
                        {
                            this.Line = line2;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        }
                        ListOuterLine = new List<Line>();
                        try
                        {
                            Line l = Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(0));
                            ListOuterLine = new List<Line>() { l };
                        }
                        catch
                        { }
                        return;
                    }
                    if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(0)))
                    {
                        if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(1)))
                        {
                            this.Line = line1;
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                            if (CheckGeometry.IsPointInLine(Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(1)), line1.GetEndPoint(0)))
                            {
                                Line l1 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(0));
                                Line l2 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(1));
                                ListOuterLine = new List<Line> { l1, l2 };
                            }
                            else
                            {
                                Line l1 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(0));
                                Line l2 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(1));
                                ListOuterLine = new List<Line> { l1, l2 };

                            }
                            return;
                        }
                        if (CheckGeometry.IsPointInLine(line1, line2.GetEndPoint(0)))
                        {
                            this.Line = Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(0));
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                            Line l1 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(1));
                            Line l2 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(0));
                            ListOuterLine = new List<Line> { l1, l2 };
                        }
                        else
                        {
                            this.Line = Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(1));
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                            Line l1 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(0));
                            Line l2 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(1));
                            ListOuterLine = new List<Line> { l1, l2 };
                        }
                        return;
                    }
                    if (CheckGeometry.IsPointInLine(line2, line1.GetEndPoint(1)))
                    {
                        if (CheckGeometry.IsPointInLine(line1, line2.GetEndPoint(0)))
                        {
                            this.Line = Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(0));
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                            Line l1 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(0));
                            Line l2 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(1));
                            ListOuterLine = new List<Line> { l1, l2 };
                        }
                        else
                        {
                            this.Line = Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(1));
                            this.Type = LineCompareLineType.SameDirectionLineOverlap;
                            Line l1 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(1));
                            Line l2 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(0));
                            ListOuterLine = new List<Line> { l1, l2 };
                        }
                        return;
                    }
                    if (CheckGeometry.IsPointInLine(line1, line2.GetEndPoint(0)))
                    {
                        this.Line = line2;
                        this.Type = LineCompareLineType.SameDirectionLineOverlap;
                        if (CheckGeometry.IsPointInLine(Line.CreateBound(line1.GetEndPoint(0), line2.GetEndPoint(1)), line2.GetEndPoint(0)))
                        {
                            Line l1 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(0));
                            Line l2 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(1));
                            ListOuterLine = new List<Line> { l1, l2 };
                        }
                        else
                        {
                            Line l1 = Line.CreateBound(line2.GetEndPoint(1), line1.GetEndPoint(0));
                            Line l2 = Line.CreateBound(line2.GetEndPoint(0), line1.GetEndPoint(1));
                            ListOuterLine = new List<Line> { l1, l2 };

                        }
                        return;
                    }
                    this.Type = LineCompareLineType.SameDirectionNonOverlap; return;
                }
                else
                { this.Type = LineCompareLineType.Parallel; return; }
                #endregion
            }
            XYZ p1 = line1.GetEndPoint(0), p2 = line1.GetEndPoint(1), p3 = line2.GetEndPoint(0), p4 = line2.GetEndPoint(1);
            if (CheckGeometry.IsPointInLineOrExtend(line2, p1))
            {
                this.Point = p1;
                if (CheckGeometry.IsPointInLine(line2, p1)) { this.Type = LineCompareLineType.Intersect; return; }
                this.Type = LineCompareLineType.NonIntersectPlanar; return;
            }
            if (CheckGeometry.IsPointInLineOrExtend(line2, p2))
            {
                this.Point = p2;
                if (CheckGeometry.IsPointInLine(line2, p2)) { this.Type = LineCompareLineType.Intersect; return; }
                this.Type = LineCompareLineType.NonIntersectPlanar; return;
            }
            if (CheckGeometry.IsPointInLineOrExtend(line1, p3))
            {
                this.Point = p3;
                if (CheckGeometry.IsPointInLine(line1, p3)) { this.Type = LineCompareLineType.Intersect; return; }
                this.Type = LineCompareLineType.NonIntersectPlanar; return;
            }
            if (CheckGeometry.IsPointInLineOrExtend(line1, p4))
            {
                this.Point = p4;
                if (CheckGeometry.IsPointInLine(line1, p4)) { this.Type = LineCompareLineType.Intersect; return; }
                this.Type = LineCompareLineType.NonIntersectPlanar; return;
            }
            if (GeomUtil.IsEqual(GeomUtil.DotMatrix(GeomUtil.SubXYZ(p1, p3), GeomUtil.CrossMatrix(vec1, vec2)), 0))
            {
                double h1 = CheckGeometry.GetSignedDistance(line2, p1), h2 = CheckGeometry.GetSignedDistance(line2, p2);
                double deltaH = 0, L1 = 0;
                double L = GeomUtil.GetLength(p1, p2);
                XYZ pP1 = CheckGeometry.GetProjectPoint(line2, p1), pP2 = CheckGeometry.GetProjectPoint(line2, p2);
                if (GeomUtil.IsEqual(pP1, p1))
                {
                    this.Point = p1; this.Type = LineCompareLineType.Intersect; return;
                }
                if (GeomUtil.IsEqual(pP2, p2))
                {
                    this.Point = p2; this.Type = LineCompareLineType.Intersect; return;
                }
                XYZ tP1 = null, tP2 = null;
                if (GeomUtil.IsSameDirection(GeomUtil.SubXYZ(pP1, p1), GeomUtil.SubXYZ(pP2, p2)))
                {
                    deltaH = Math.Abs(h1 - h2);
                    L1 = L * h1 / deltaH;
                    tP1 = GeomUtil.OffsetPoint(p1, line1.Direction, L1); tP2 = GeomUtil.OffsetPoint(p1, line1.Direction, -L1);
                    if (CheckGeometry.IsPointInLineOrExtend(line2, tP1)) { this.Point = tP1; }
                    else if (CheckGeometry.IsPointInLineOrExtend(line2, tP2)) { this.Point = tP2; }
                    else
                    {
                        throw new Exception("Two points is not in line extend!");
                    }
                    this.Type = LineCompareLineType.NonIntersectPlanar; return;
                }

                deltaH = h1 + h2;
                L1 = L * h1 / deltaH;
                tP1 = GeomUtil.OffsetPoint(p1, line1.Direction, L1); tP2 = GeomUtil.OffsetPoint(p1, line1.Direction, -L1);
                if (CheckGeometry.IsPointInLineOrExtend(line2, tP1)) { this.Point = tP1; }
                else if (CheckGeometry.IsPointInLineOrExtend(line2, tP2)) { this.Point = tP2; }
                else { throw new Exception("Two points is not in line extend!"); }
                if (CheckGeometry.IsPointInLine(line2, this.Point) && CheckGeometry.IsPointInLine(line1, this.Point))
                {

                    this.Type = LineCompareLineType.Intersect; return;
                }
                this.Type = LineCompareLineType.NonIntersectPlanar; return;
            }
            this.Type = LineCompareLineType.NonIntersectNonPlanar; return;
        }
    }
    public enum LineCompareLineType
    {
        SameDirectionPointOverlap, SameDirectionNonOverlap, SameDirectionLineOverlap, Parallel, Intersect, NonIntersectPlanar, NonIntersectNonPlanar
    }
    public class LineComparePolygonResult
    {
        public LineComparePolygonType Type { get; private set; }
        public List<Line> ListLine { get; private set; }
        public Line ProjectLine { get; private set; }
        public List<XYZ> ListPoint { get; private set; }
        private Line line;
        private Polygon polygon;
        public LineComparePolygonResult(Polygon plgon, Line l)
        {
            this.line = l; this.polygon = plgon; GetParameter();
        }
        private void GetParameter()
        {
            #region Planar
            if (GeomUtil.IsEqual(GeomUtil.DotMatrix(line.Direction, polygon.Normal), 0))
            {
                if (polygon.CheckXYZPointPosition(line.GetEndPoint(0)) == PointComparePolygonResult.NonPlanar)
                {
                    XYZ p11 = line.GetEndPoint(0), p21 = line.GetEndPoint(1);
                    XYZ pP11 = CheckGeometry.GetProjectPoint(polygon.Plane, p11);
                    XYZ pP21 = CheckGeometry.GetProjectPoint(polygon.Plane, p21);
                    Line l11 = Line.CreateBound(pP11, pP21);
                    this.ProjectLine = l11;
                    this.Type = LineComparePolygonType.NonPlanarParallel; return;
                }
                if (polygon.IsLineOutPolygon(line))
                {
                    this.Type = LineComparePolygonType.Outside; return;
                }
                this.ListLine = new List<Line>();
                this.ListPoint = new List<XYZ>();
                if (polygon.IsLineInPolygon(line))
                {
                    this.ListLine.Add(line); this.Type = LineComparePolygonType.Inside; return;
                }
                foreach (Curve c in polygon.ListCurve)
                {
                    LineCompareLineResult res2 = new LineCompareLineResult(c, line);
                    if (res2.Type == LineCompareLineType.SameDirectionLineOverlap)
                    {
                        this.ListLine.Add(res2.Line);
                    }
                    if (res2.Type == LineCompareLineType.Intersect)
                    {

                        this.ListPoint.Add(res2.Point);
                    }
                }
                if (ListPoint.Count != 0)
                {
                    ListPoint.Sort(new ZYXComparer());
                    List<XYZ> points = new List<XYZ>();
                    for (int i = 0; i < ListPoint.Count; i++)
                    {
                        bool check = true;
                        for (int j = i + 1; j < ListPoint.Count; j++)
                        {
                            if (GeomUtil.IsEqual(ListPoint[i], ListPoint[j]))
                            {
                                check = false; break;
                            }
                        }
                        if (check) points.Add(ListPoint[i]);
                    }
                    ListPoint = points;
                    if (ListPoint.Count == 1)
                    {
                        ListPoint.Insert(0, line.GetEndPoint(0)); ListPoint.Add(line.GetEndPoint(1));
                    }
                    else
                    {
                        if (GeomUtil.IsEqual(ListPoint[0], line.GetEndPoint(0)))
                        {
                            if (GeomUtil.IsEqual(ListPoint[ListPoint.Count - 1], line.GetEndPoint(1)))
                            { }
                            else ListPoint.Add(line.GetEndPoint(1));
                        }
                        else if (GeomUtil.IsEqual(ListPoint[0], line.GetEndPoint(1)))
                        {
                            if (GeomUtil.IsEqual(ListPoint[ListPoint.Count - 1], line.GetEndPoint(0)))
                            { }
                            else ListPoint.Add(line.GetEndPoint(0));
                        }
                        else if (GeomUtil.IsEqual(ListPoint[ListPoint.Count - 1], line.GetEndPoint(0)))
                        {
                            ListPoint.Insert(0, line.GetEndPoint(1));
                        }
                        else if (GeomUtil.IsEqual(ListPoint[ListPoint.Count - 1], line.GetEndPoint(1)))
                        {
                            ListPoint.Insert(0, line.GetEndPoint(0));
                        }
                        else if (GeomUtil.IsSameDirection(GeomUtil.SubXYZ(ListPoint[ListPoint.Count - 1], ListPoint[0]), GeomUtil.SubXYZ(line.GetEndPoint(1), line.GetEndPoint(0))))
                        {
                            ListPoint.Insert(0, line.GetEndPoint(0)); ListPoint.Add(line.GetEndPoint(1));
                        }
                        else
                        {
                            ListPoint.Insert(0, line.GetEndPoint(1)); ListPoint.Add(line.GetEndPoint(0));
                        }
                    }
                    for (int i = 0; i < this.ListPoint.Count - 1; i++)
                    {
                        if (GeomUtil.IsEqual(ListPoint[i], ListPoint[i + 1])) continue;
                        Line l = null;
                        try
                        {
                            l = Line.CreateBound(ListPoint[i], ListPoint[i + 1]);
                        }
                        catch
                        {
                            continue;
                        }
                        bool check = true;
                        if (polygon.IsLineInPolygon(l))
                        {
                            bool check2 = false;
                            for (int j = 0; j < ListLine.Count; j++)
                            {
                                LineCompareLineResult res1 = new LineCompareLineResult(ListLine[j], l);
                                if (res1.Type == LineCompareLineType.SameDirectionLineOverlap) check = false;
                                if (res1.Type == LineCompareLineType.SameDirectionPointOverlap)
                                {
                                    ListLine[j] = res1.MergeLine;
                                    check2 = true; break;
                                }
                            }
                            if (check2) continue;
                            if (check)
                            {
                                ListLine.Add(l);
                            }
                        }
                    }
                    this.Type = LineComparePolygonType.OverlapOrIntersect; return;
                }
            }
            #endregion
            XYZ p1 = line.GetEndPoint(0), p2 = line.GetEndPoint(1);
            XYZ pP1 = CheckGeometry.GetProjectPoint(polygon.Plane, p1);
            XYZ pP2 = CheckGeometry.GetProjectPoint(polygon.Plane, p2);
            ListPoint = new List<XYZ>();
            if (GeomUtil.IsEqual(pP1, pP2))
            {
                this.ListPoint.Add(pP1);
                if (CheckGeometry.IsPointInLine(line, pP1))
                {
                    if (polygon.CheckXYZPointPosition(pP1) != PointComparePolygonResult.Outside) { this.Type = LineComparePolygonType.PerpendicularIntersectFace; return; }
                    this.Type = LineComparePolygonType.PerpendicularIntersectPlane; return;
                }
                this.Type = LineComparePolygonType.PerpendicularNonIntersect; return;
            }
            Line l1 = Line.CreateBound(pP1, pP2);
            ProjectLine = l1;
            LineCompareLineResult res = new LineCompareLineResult(line, l1);
            if (res.Type == LineCompareLineType.Intersect)
            {
                PointComparePolygonResult resP = polygon.CheckXYZPointPosition(res.Point);
                ListPoint.Add(res.Point);
                if (resP == PointComparePolygonResult.Outside) { this.Type = LineComparePolygonType.NonPlanarIntersectPlane; return; }
                this.Type = LineComparePolygonType.NonPlanarIntersectFace; return;
            }
            ListPoint.Add(res.Point);
            this.Type = LineComparePolygonType.NonPlanarNonIntersect; return;
        }
    }
    public enum LineComparePolygonType
    {
        NonPlanarIntersectPlane, NonPlanarIntersectFace, NonPlanarNonIntersect, NonPlanarParallel, Outside, Inside, OverlapOrIntersect,
        PerpendicularNonIntersect, PerpendicularIntersectPlane, PerpendicularIntersectFace
    }
    public class PolygonComparePolygonResult
    {
        public PolygonComparePolygonPositionType PositionType { get; private set; }
        public PolygonComparePolygonIntersectType IntersectType { get; private set; }
        public List<Line> ListLine { get; private set; }
        public List<XYZ> ListPoint { get; private set; }
        public MultiPolygon OuterMultiPolygon { get; private set; }
        public List<Polygon> ListPolygon { get; private set; }
        private Polygon polygon1;
        private Polygon polygon2;
        public PolygonComparePolygonResult(Polygon pl1, Polygon pl2)
        {
            this.polygon1 = pl1; this.polygon2 = pl2; GetPositionType(); GetIntersectTypeAndOtherParameter();
        }
        private void GetPositionType()
        {
            if (GeomUtil.IsSameOrOppositeDirection(polygon1.Normal, polygon2.Normal))
            {
                if (polygon1.CheckXYZPointPosition(polygon2.ListXYZPoint[0]) != PointComparePolygonResult.NonPlanar) { this.PositionType = PolygonComparePolygonPositionType.Planar; return; }
                this.PositionType = PolygonComparePolygonPositionType.Parallel; return;
            }
            this.PositionType = PolygonComparePolygonPositionType.NonPlanar; return;
        }
        private void GetIntersectTypeAndOtherParameter()
        {
            switch (PositionType)
            {
                case PolygonComparePolygonPositionType.Parallel: this.IntersectType = PolygonComparePolygonIntersectType.NonIntersect; return;
                #region NonPlanar
                case PolygonComparePolygonPositionType.NonPlanar:
                    bool check = false, check2 = false;
                    List<XYZ> points = new List<XYZ>();
                    List<Line> lines = new List<Line>();
                    foreach (Curve c in polygon2.ListCurve)
                    {
                        LineComparePolygonResult res = new LineComparePolygonResult(polygon1, CheckGeometry.ConvertLine(c));
                        if (res.Type == LineComparePolygonType.NonPlanarIntersectFace || res.Type == LineComparePolygonType.PerpendicularIntersectFace)
                        {
                            check = true;
                            bool checkP = true;
                            foreach (XYZ point in points)
                            {
                                if (GeomUtil.IsEqual(point, res.ListPoint[0])) { checkP = false; break; }
                            }
                            if (checkP)
                                points.Add(res.ListPoint[0]);
                        }
                        if (res.Type == LineComparePolygonType.OverlapOrIntersect)
                        {
                            check2 = true;
                            lines = res.ListLine;
                        }
                    }
                    if (check2)
                    {
                        if (points.Count >= 4)
                        {
                            for (int i = 0; i < points.Count - 1; i++)
                            {
                                if (GeomUtil.IsEqual(points[i], points[i + 1])) continue;
                                Line l = Line.CreateBound(points[i], points[i + 1]);
                                bool check3 = true;
                                if (polygon2.IsLineInPolygon(l))
                                {
                                    bool check4 = false;
                                    for (int j = 0; j < lines.Count; j++)
                                    {
                                        LineCompareLineResult res1 = new LineCompareLineResult(lines[j], l);
                                        if (res1.Type == LineCompareLineType.SameDirectionLineOverlap) check3 = false;
                                        if (res1.Type == LineCompareLineType.SameDirectionPointOverlap)
                                        {
                                            ListLine[j] = res1.MergeLine;
                                            check4 = true; break;
                                        }
                                    }
                                    if (check4) continue;
                                    if (check3)
                                    {
                                        lines.Add(l);
                                    }
                                }
                            }
                        }
                        this.ListLine = lines;
                        this.IntersectType = PolygonComparePolygonIntersectType.Boundary; return;
                    }
                    if (check)
                    {
                        if (points.Count >= 2)
                        {
                            for (int i = 0; i < points.Count - 1; i++)
                            {
                                if (GeomUtil.IsEqual(points[i], points[i + 1])) continue;
                                Line l = Line.CreateBound(points[i], points[i + 1]);
                                bool check3 = true;

                                if (polygon2.IsLineInPolygon(l))
                                {
                                    bool check4 = false;
                                    for (int j = 0; j < lines.Count; j++)
                                    {
                                        LineCompareLineResult res1 = new LineCompareLineResult(lines[j], l);
                                        if (res1.Type == LineCompareLineType.SameDirectionLineOverlap) check3 = false;
                                        if (res1.Type == LineCompareLineType.SameDirectionPointOverlap)
                                        {
                                            lines[j] = res1.MergeLine;
                                            check4 = true; break;
                                        }
                                    }
                                    if (check4) continue;
                                    if (check3)
                                    {
                                        lines.Add(l);
                                    }
                                }
                            }
                            if (lines.Count != 0)
                            {
                                this.ListLine = lines; this.IntersectType = PolygonComparePolygonIntersectType.Boundary; return;
                            }
                        }
                        this.ListPoint = points; this.IntersectType = PolygonComparePolygonIntersectType.Point; return;
                    }
                    this.IntersectType = PolygonComparePolygonIntersectType.NonIntersect; return;
                #endregion
                case PolygonComparePolygonPositionType.Planar:
                    check = false;
                    check2 = false;
                    List<Line> lines1 = new List<Line>(), lines2 = new List<Line>();
                    List<XYZ> points1 = new List<XYZ>(), points2 = new List<XYZ>();
                    foreach (Curve c1 in polygon1.ListCurve)
                    {
                        LineComparePolygonResult res = new LineComparePolygonResult(polygon2, CheckGeometry.ConvertLine(c1));
                        if (res.Type == LineComparePolygonType.OverlapOrIntersect || res.Type == LineComparePolygonType.Inside)
                        {
                            check2 = true;
                            foreach (Line l in res.ListLine)
                            {
                                lines1.Add(l);
                            }
                        }
                        if (res.Type == LineComparePolygonType.Outside)
                        {
                            if (polygon2.CheckXYZPointPosition(c1.GetEndPoint(0)) == PointComparePolygonResult.Boundary || polygon2.CheckXYZPointPosition(c1.GetEndPoint(0)) == PointComparePolygonResult.Node)
                            {
                                points1.Add(c1.GetEndPoint(0));
                                check = true;
                            }
                            if (polygon2.CheckXYZPointPosition(c1.GetEndPoint(1)) == PointComparePolygonResult.Boundary || polygon2.CheckXYZPointPosition(c1.GetEndPoint(1)) == PointComparePolygonResult.Node)
                            {
                                points1.Add(c1.GetEndPoint(1));
                                check = true;
                            }
                        }
                    }
                    foreach (Curve c1 in polygon2.ListCurve)
                    {
                        LineComparePolygonResult res = new LineComparePolygonResult(polygon1, CheckGeometry.ConvertLine(c1));
                        if (res.Type == LineComparePolygonType.OverlapOrIntersect || res.Type == LineComparePolygonType.Inside)
                        {
                            check2 = true;
                            foreach (Line l in res.ListLine)
                            {
                                lines2.Add(l);
                            }
                        }
                        if (res.Type == LineComparePolygonType.Outside)
                        {
                            if (polygon1.CheckXYZPointPosition(c1.GetEndPoint(0)) == PointComparePolygonResult.Boundary || polygon1.CheckXYZPointPosition(c1.GetEndPoint(0)) == PointComparePolygonResult.Node)
                            {
                                points2.Add(c1.GetEndPoint(0));
                                check = true;
                            }
                            if (polygon1.CheckXYZPointPosition(c1.GetEndPoint(1)) == PointComparePolygonResult.Boundary || polygon1.CheckXYZPointPosition(c1.GetEndPoint(1)) == PointComparePolygonResult.Node)
                            {
                                points2.Add(c1.GetEndPoint(1));
                                check = true;
                            }
                        }
                    }
                    if (check2)
                    {
                        foreach (Line l in lines2)
                        {
                            lines1.Add(l);
                        }
                        lines = new List<Line>();
                        for (int i = 0; i < lines1.Count; i++)
                        {
                            bool check3 = true;
                            for (int j = i + 1; j < lines1.Count; j++)
                            {
                                LineCompareLineResult res = new LineCompareLineResult(lines1[i], lines1[j]);
                                if (res.Type == LineCompareLineType.SameDirectionLineOverlap)
                                {
                                    check3 = false;
                                    break;
                                }
                            }
                            if (check3) lines.Add(lines1[i]);
                        }
                        this.ListLine = new List<Line>();
                        List<int> nums = new List<int>();
                        for (int i = 0; i < lines.Count; i++)
                        {
                            for (int k = 0; k < nums.Count; k++)
                            {
                                if (i == k) goto EndLoop;
                            }
                            bool check6 = true;
                            Line temp = null;
                            for (int j = i + 1; j < lines.Count; j++)
                            {
                                LineCompareLineResult llRes = new LineCompareLineResult(lines[i], lines[j]);
                                if (llRes.Type == LineCompareLineType.SameDirectionPointOverlap)
                                {
                                    nums.Add(i); nums.Add(j);
                                    check6 = false;
                                    temp = llRes.MergeLine;
                                    break;
                                }
                            }
                            if (!check6) ListLine.Add(temp);
                            else ListLine.Add(lines[i]);
                            EndLoop:
                            int a = 0;
                        }
                        List<Curve> cs = new List<Curve>();
                        foreach (Line l in ListLine)
                        {
                            cs.Add(l);
                        }
                        List<Polygon> pls = new List<Polygon>();
                        if (CheckGeometry.CreateListPolygon(cs, out pls))
                        {
                            ListPolygon = pls;
                            this.IntersectType = PolygonComparePolygonIntersectType.AreaOverlap; return;
                        }
                        this.IntersectType = PolygonComparePolygonIntersectType.Boundary; return;
                    }
                    if (check)
                    {
                        foreach (XYZ pnt in points2)
                        {
                            points1.Add(pnt);
                        }
                        points = new List<XYZ>();
                        for (int i = 0; i < points1.Count; i++)
                        {
                            bool check3 = true;
                            for (int j = i + 1; j < points1.Count; j++)
                            {
                                if (GeomUtil.IsEqual(points1[i], points1[j]))
                                {
                                    check3 = false; break;
                                }
                            }
                            if (check3)
                            {
                                points.Add(points1[i]);
                            }
                        }
                        this.ListPoint = points; this.IntersectType = PolygonComparePolygonIntersectType.Point; return;
                    }
                    this.IntersectType = PolygonComparePolygonIntersectType.NonIntersect; return;
            }
            throw new Exception("Code complier should never be here.");
        }
        public void GetOuterPolygon(Polygon polygonCut, out object outerPolygonOrMulti)
        {
            if (polygonCut != polygon1 && polygonCut != polygon2)
                throw new Exception("Choose polygon be cut from first two polygons!");
            if (ListPolygon[0] == polygon1 || ListPolygon[0] == polygon2)
            {
                if (ListPolygon[0] == polygonCut)
                {
                    outerPolygonOrMulti = null;
                }
                else
                {
                    outerPolygonOrMulti = new MultiPolygon(polygon1, polygon2);
                }
            }
            else
            {
                Polygon temp = polygonCut;
                foreach (Polygon pl in ListPolygon)
                {
                    temp = CheckGeometry.GetPolygonCut(temp, pl);
                }
                outerPolygonOrMulti = temp;
            }
        }
    }
    public enum PolygonComparePolygonPositionType
    {
        Planar, NonPlanar, Parallel
    }
    public enum PolygonComparePolygonIntersectType
    {
        AreaOverlap, Point, Boundary, NonIntersect
    }
}
