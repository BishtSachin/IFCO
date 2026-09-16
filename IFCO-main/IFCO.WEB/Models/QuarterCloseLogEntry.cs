namespace IFCO.WEB.Models
{
    public class QuarterCloseLogEntry
    {
        public int SqNo { get; set; }
        public string FinancialYear { get; set; } = "";
        public int Quarter { get; set; }
        public bool IsFinancialYearEnd { get; set; }
        public string NextFinancialYear { get; set; } = "";
        public int NextQuarter { get; set; }
        public int PointsArchivedCount { get; set; }
        public int PointsCarriedForwardCount { get; set; }
        public string? CertificateFileName { get; set; }
        public string? ClosedBy { get; set; }
        public DateTime ClosedDate { get; set; }
    }
}
