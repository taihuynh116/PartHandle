using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace PartHandleAutocad
{
    public static class AutocadUtil
    {
        public static bool IsEqual(Point3d p1, Point3d p2)
        {
            if (p1.Z == p2.Z)
            {
                if (p1.Y == p2.Y)
                {
                    return p1.X == p2.X;
                }
                return false;
            }
            return false;
        }
        public static string DocumentShortName(Database db)
        {
            string DocumentName = db.OriginalFileName;
            return (DocumentName.Remove(0, DocumentName.LastIndexOf(@"\") + 1)).Remove(DocumentName.Remove(0, DocumentName.LastIndexOf(@"\") + 1).Length - 4);
        }

        public static string DirectoryFolder(Database db)
        {
            string DocumentName = db.OriginalFileName;
            return DocumentName.Remove(DocumentName.LastIndexOf(@"\") + 1);
        }
    }
    public class ZYXComparer : IComparer<Point3d>
    {
        int IComparer<Point3d>.Compare(Point3d p1, Point3d p2)
        {
            if (p1.Z == p2.Z)
            {
                if (p1.Y == p2.Y)
                {
                    return p1.X.CompareTo(p2.X);
                }
                return p1.Y.CompareTo(p2.Y);
            }
            return p1.Z.CompareTo(p2.Z);
        }
    }
}
