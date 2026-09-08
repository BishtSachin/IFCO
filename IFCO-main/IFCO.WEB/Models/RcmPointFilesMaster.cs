// IFCO.WEB/Models/RcmPointFilesMaster.cs
namespace IFCO.WEB.Models
{
    public class RcmPointFilesMaster
    {
        public int FileSqNo { get; set; }
        public int RcmPointSqNo { get; set; }
        public string? FileName { get; set; }
        public string? FileType { get; set; }
        public long? FileSizeBytes { get; set; }
        public byte[]? FileData { get; set; } // For uploading/downloading
        public string? UploadedBy { get; set; }
        public DateTime UploadedDate { get; set; }
    }
}