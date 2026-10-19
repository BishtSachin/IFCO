namespace IFCO.WEB.Models
{
    // One row of an RCM point's audit trail (IFCO.RCM_POINT_HISTORY)
    public class PointHistoryEntry
    {
        public int HistoryId { get; set; }
        public string EventType { get; set; } = "";
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string? ActedBy { get; set; }
        public string? ActedByName { get; set; }
        public string? ActedByRole { get; set; }
        public DateTime ActedDate { get; set; }
        public string? Remarks { get; set; }
    }

    // One in-app alert (IFCO.RCM_NOTIFICATION)
    public class NotificationItem
    {
        public int NotificationId { get; set; }
        public int? RcmPointSqNo { get; set; }
        public string Message { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
