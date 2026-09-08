using System.ComponentModel.DataAnnotations;

namespace IFCO.WEB.Models
{
    public class VerticalMaster
    {
        public int SqNo { get; set; }

        [Required(ErrorMessage = "Vertical ID is required.")]
        [StringLength(50, ErrorMessage = "Vertical ID cannot be longer than 50 characters.")]
        public string? VerticalId { get; set; }

        [Required(ErrorMessage = "Vertical Name is required.")]
        [StringLength(255, ErrorMessage = "Vertical Name cannot be longer than 255 characters.")]
        public string? VerticalName { get; set; }

        public string Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}