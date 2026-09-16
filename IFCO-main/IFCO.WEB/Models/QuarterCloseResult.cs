namespace IFCO.WEB.Models
{
    public class QuarterCloseResult
    {
        public int PointsArchived { get; set; }
        public int PointsCarriedForward { get; set; }
        public string NewFinancialYear { get; set; } = "";
        public int NewQuarter { get; set; }
    }
}
