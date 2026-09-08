using System.ComponentModel.DataAnnotations;

namespace IFCO.WEB.Models
{
    public class RcmMaster
    {
        public int SqNo { get; set; }

        [Required(ErrorMessage = "RCM ID is required.")]
        [StringLength(50, ErrorMessage = "RCM ID cannot be longer than 50 characters.")]
        public string? RcmId { get; set; }

        [Required(ErrorMessage = "RCM Name is required.")]
        [StringLength(255, ErrorMessage = "RCM Name cannot be longer than 255 characters.")]
        public string? RcmName { get; set; }
        public string Status { get; set; } = "Active";
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}