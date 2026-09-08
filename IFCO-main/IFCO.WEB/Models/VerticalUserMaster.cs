using System.ComponentModel.DataAnnotations;

namespace IFCO.WEB.Models
{
    public class VerticalUserMaster
    {
        public int SqNo { get; set; }

        [Required(ErrorMessage = "User ID is required.")]
        [StringLength(100, ErrorMessage = "User ID cannot be longer than 100 characters.")]
        public string? UserId { get; set; }

        [Required(ErrorMessage = "User Name is required.")]
        [StringLength(255, ErrorMessage = "User Name cannot be longer than 255 characters.")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "User Type is required.")]
        public string UserType { get; set; } = "Vertical"; // Default value

        // A user might not be assigned to a vertical (e.g., an Admin)
        public string? VerticalId { get; set; }

        public string Status { get; set; } = "Active";

        // For display purposes from a JOIN
        public string? VerticalName { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}