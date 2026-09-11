using System.Linq;

namespace IFCO.WEB.Helpers
{
    /// <summary>
    /// Shared file-upload validation used by every file-upload point in the app
    /// (RCM Point attachments, Quarterly Report uploads, and any future upload
    /// feature) so the allowed file types and size limit only need to be
    /// maintained in one place instead of drifting apart per page.
    /// </summary>
    public static class FileUploadValidator
    {
        public const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB per file

        // Keep this in sync with AcceptAttributeValue below - that's just the
        // browser file-picker hint, this is the actual enforced list.
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".xlsx", ".xls", ".doc", ".docx", ".png", ".jpg", ".jpeg", ".zip"
        };

        // For <InputFile accept="..."> - filters the OS file picker's default
        // view as a convenience. NOT a security control by itself: a user can
        // still pick "All files" or drag-and-drop anything, which is exactly
        // why TryValidate() below is the real, server-side enforced check.
        public static string AcceptAttributeValue => string.Join(",", AllowedExtensions);

        // Human-readable list for error messages, e.g. "PDF, XLSX, XLS, DOC, DOCX, PNG, JPG, JPEG, ZIP"
        public static string AllowedExtensionsDisplay =>
            string.Join(", ", AllowedExtensions.Select(e => e.TrimStart('.').ToUpperInvariant()));

        /// <summary>
        /// Validates both file type and size. Returns false with a clear,
        /// user-facing errorMessage if the file should be rejected.
        /// </summary>
        public static bool TryValidate(string fileName, long fileSizeBytes, out string? errorMessage)
        {
            var extension = Path.GetExtension(fileName);

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                errorMessage = $"'{fileName}' has an unsupported file type. Allowed file types: {AllowedExtensionsDisplay}.";
                return false;
            }

            if (fileSizeBytes > MaxFileSizeBytes)
            {
                errorMessage = $"'{fileName}' is {fileSizeBytes / 1024.0 / 1024.0:F1} MB, which exceeds the {MaxFileSizeBytes / 1024 / 1024} MB limit per file.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
