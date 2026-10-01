using System.Data;

namespace IFCO.WEB.Helpers
{
    /// <summary>
    /// Defensive helper for building Excel exports from a DataTable via
    /// ClosedXML (used by Report Generation and Historical Data's "Download
    /// Excel"). ClosedXML converts DateTime cells through .ToOADate(),
    /// which throws "Not a legal OleAut date" for any value before roughly
    /// 1900 - DateTime.MinValue (0001-01-01) is the classic trigger. This
    /// typically shows up on legacy/bulk-imported data that bypassed the
    /// application's own insert/update procedures and left a date column
    /// NULL or defaulted in a way ODP.NET surfaces as MinValue rather than
    /// DBNull. Call SanitizeDateColumns on a DataTable before handing it to
    /// ClosedXML's InsertTable - every export built this way is vulnerable
    /// to the same class of bug, not just the one that happened to hit it
    /// first.
    /// </summary>
    public static class ExcelExportHelper
    {
        private static readonly DateTime MinSafeExcelDate = new DateTime(1900, 1, 1);

        public static void SanitizeDateColumns(DataTable table)
        {
            var dateColumns = table.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(DateTime))
                .ToList();

            if (dateColumns.Count == 0) return;

            foreach (DataRow row in table.Rows)
            {
                foreach (var col in dateColumns)
                {
                    if (row[col] is DateTime dt && dt < MinSafeExcelDate)
                    {
                        row[col] = DBNull.Value;
                    }
                }
            }
        }
    }
}
