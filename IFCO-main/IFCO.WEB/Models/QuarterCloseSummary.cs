namespace IFCO.WEB.Models
{
    public class QuarterCloseSummary
    {
        public string CurrentFinancialYear { get; set; } = "";
        public int CurrentQuarter { get; set; }
        public string NextFinancialYear { get; set; } = "";
        public int NextQuarter { get; set; }
        public bool IsFinancialYearEnd { get; set; }
        public int VerifiedCount { get; set; }
        public int ForceClosedCount { get; set; }
        public int CarryForwardCount { get; set; }
        public int FyEndAdditionalCarryForward { get; set; }
        public bool CertificateAlreadyUploaded { get; set; }

        public int TotalArchivedCount => VerifiedCount + ForceClosedCount;
        public int TotalCarryForwardCount => CarryForwardCount + FyEndAdditionalCarryForward;
    }
}
