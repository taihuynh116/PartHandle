#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
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

            ICollection<ElementId> partIds = new List<ElementId> { part.Id };
            List<Curve> cs = (from element in sel.PickElementsByRectangle()
                              let dl = (element as DetailLine).GeometryCurve
                              select dl).ToList();

            PartUtils.DivideParts(doc, partIds, null, cs, SketchPlane.Create(doc, rf).Id);

            tx.Commit();
            return Result.Succeeded;
        }
    }
}
