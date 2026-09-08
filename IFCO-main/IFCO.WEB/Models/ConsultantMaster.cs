using System.ComponentModel.DataAnnotations;

namespace IFCO.WEB.Models
{
    public class ConsultantMaster
    {
        public int SqNo { get; set; }

        [Required(ErrorMessage = "Consultant ID is required.")]
        [StringLength(50, ErrorMessage = "Consultant ID cannot be longer than 50 characters.")]
        public string? ConsultantId { get; set; }

        [Required(ErrorMessage = "Consultant Name is required.")]
        [StringLength(255, ErrorMessage = "Consultant Name cannot be longer than 255 characters.")]
        public string? ConsultantName { get; set; }

        [Required(ErrorMessage = "Consultant Type is required.")]
        public string ConsultantType { get; set; } = "Maker"; // Default value

        [Required(ErrorMessage = "Mobile Number is required.")]
        // The database stores up to 20 chars, but the input should be 10 digits
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile number must be exactly 10 digits.")]
        public string? MobileNumber { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email Address format.")]
        [StringLength(100, ErrorMessage = "Email ID cannot be longer than 100 characters.")]
        public string? EmailId { get; set; }

        // --- NEW PROPERTIES START ---

        [Range(1, int.MaxValue, ErrorMessage = "An assigned checker is required for a Maker.")]
        public int? CheckerSqNo { get; set; }

        // This property is for display only, populated by the database join
        public string? CheckerName { get; set; }

        // --- NEW PROPERTIES END ---

        public string Status { get; set; } = "Active"; // Default value

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}