using System.IO.Compression;
using System.Linq;
using System.Text;

namespace IFCO.WEB.Helpers
{
    /// <summary>
    /// Shared file-upload validation used by every file-upload point in the app
    /// (RCM Point attachments, Quarterly Report / Close Quarter certificate, and any
    /// future upload feature) so the rules only need to be maintained in one place.
    ///
    /// Validation is done in two steps:
    ///   1. TryValidate()        - before reading the file: file name, extension allow-list, size.
    ///   2. TryValidateContent() - after reading the bytes: real file type (magic bytes) and a
    ///                             scan for active / executable content (PDF JavaScript, Office macros,
    ///                             script payloads hidden in images).
    /// Files are only ever stored in the database (BLOB) - never on disk or under the web root.
    /// </summary>
    public static class FileUploadValidator
    {
        public const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB per file

        /// <summary>Maximum number of attachments allowed on one RCM point.</summary>
        public const int MaxFilesPerPoint = 10;

        private const int MaxFileNameLength = 150;

        // Keep this in sync with AcceptAttributeValue below - that's just the
        // browser file-picker hint, this is the actual enforced list.
        // (.doc, .xls and .zip were removed - macro-capable / opaque containers.)
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg"
        };

        // Extensions that must never appear anywhere in a file name (blocks "shell.aspx.jpg" style names).
        private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".php", ".php3", ".php5", ".phtml", ".jsp", ".jspx", ".asp", ".aspx", ".ashx", ".asmx", ".asa",
            ".cshtml", ".razor", ".cgi", ".pl", ".py", ".rb", ".sh", ".exe", ".dll", ".bat", ".cmd", ".com",
            ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".msi", ".scr", ".jar",
            ".config", ".htm", ".html", ".svg", ".xml", ".cer", ".lnk", ".reg", ".iso"
        };

        // PDF names that indicate active / embedded content. (/OpenAction alone is not listed: it is also
        // used by harmless "open at page 1" PDFs; a JavaScript or Launch action always carries one of these.)
        private static readonly HashSet<string> PdfBlockedNames = new(StringComparer.Ordinal)
        {
            "JavaScript", "JS", "Launch", "EmbeddedFile", "RichMedia", "SubmitForm", "ImportData", "XFA"
        };

        // Script / web-shell markers that must not be present inside an image (lower-case, compared case-insensitively).
        private static readonly byte[][] ImageScriptMarkers = new[]
        {
            "<?php", "<script", "<%@ page", "<%@page", "<%@ language", "<asp:", "runat=\"server\"", "powershell"
        }.Select(s => Encoding.ASCII.GetBytes(s)).ToArray();

        private static readonly byte[] EndStreamBytes = Encoding.ASCII.GetBytes("endstream");

        // For <InputFile accept="..."> - filters the OS file picker's default
        // view as a convenience. NOT a security control by itself: a user can
        // still pick "All files" or drag-and-drop anything, which is exactly
        // why the checks below are the real, server-side enforced ones.
        public static string AcceptAttributeValue => string.Join(",", AllowedExtensions);

        // Human-readable list for error messages, e.g. "PDF, DOCX, XLSX, PNG, JPG, JPEG"
        public static string AllowedExtensionsDisplay =>
            string.Join(", ", AllowedExtensions.Select(e => e.TrimStart('.').ToUpperInvariant()));

        // ------------------------------------------------------------------
        // Step 1: name / extension / size
        // ------------------------------------------------------------------

        /// <summary>
        /// Validates file name, type (extension) and size. Returns false with a clear,
        /// user-facing errorMessage if the file should be rejected.
        /// </summary>
        public static bool TryValidate(string fileName, long fileSizeBytes, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || fileName.IndexOfAny(new[] { '/', '\\' }) >= 0
                || fileName.Contains("..")
                || fileName.Any(char.IsControl))
            {
                errorMessage = "The file name contains invalid characters. Please rename the file and try again.";
                return false;
            }

            var extension = Path.GetExtension(fileName);

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                errorMessage = $"'{SanitizeFileName(fileName)}' has an unsupported file type. Allowed file types: {AllowedExtensionsDisplay}.";
                return false;
            }

            // Double-extension check: "shell.aspx.jpg", "payload.php.pdf", "setup.exe.png" ...
            var parts = fileName.Split('.');
            for (int i = 1; i < parts.Length - 1; i++)
            {
                if (BlockedExtensions.Contains("." + parts[i].Trim()))
                {
                    errorMessage = $"'{SanitizeFileName(fileName)}' has a blocked extension in its name. Please rename the file and try again.";
                    return false;
                }
            }

            if (fileSizeBytes <= 0)
            {
                errorMessage = $"'{SanitizeFileName(fileName)}' is empty.";
                return false;
            }

            if (fileSizeBytes > MaxFileSizeBytes)
            {
                errorMessage = $"'{SanitizeFileName(fileName)}' is {fileSizeBytes / 1024.0 / 1024.0:F1} MB, which exceeds the {MaxFileSizeBytes / 1024 / 1024} MB limit per file.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        // ------------------------------------------------------------------
        // Step 2: content
        // ------------------------------------------------------------------

        /// <summary>
        /// Verifies the real file type from the file's own bytes (magic bytes) and rejects files that
        /// carry active content. Call after TryValidate() and after the bytes have been read.
        /// </summary>
        public static bool TryValidateContent(string fileName, byte[] data, out string? errorMessage)
        {
            var safeName = SanitizeFileName(fileName);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (data == null || data.Length == 0)
            {
                errorMessage = $"'{safeName}' is empty.";
                return false;
            }

            if (!HasValidSignature(data, ext))
            {
                errorMessage = $"'{safeName}' is not a valid {ext.TrimStart('.').ToUpperInvariant()} file (the file content does not match its extension).";
                return false;
            }

            string? problem = null;
            bool clean = ext switch
            {
                ".pdf" => !PdfHasActiveContent(data, 0, out problem),
                ".docx" or ".xlsx" => IsCleanOoxml(data, ext, out problem),
                ".png" or ".jpg" or ".jpeg" => !ImageHasScriptContent(data, out problem),
                _ => false
            };

            if (!clean)
            {
                errorMessage = $"'{safeName}' was rejected because it contains potentially unsafe content ({problem ?? "unsupported content"}).";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool HasValidSignature(byte[] d, string ext)
        {
            switch (ext)
            {
                case ".pdf": // %PDF-
                    return d.Length >= 5 && d[0] == 0x25 && d[1] == 0x50 && d[2] == 0x44 && d[3] == 0x46 && d[4] == 0x2D;
                case ".png": // 89 50 4E 47 0D 0A 1A 0A
                    return d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47
                           && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A;
                case ".jpg":
                case ".jpeg": // FF D8 FF
                    return d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF;
                case ".docx":
                case ".xlsx": // ZIP container: PK 03 04
                    return d.Length >= 4 && d[0] == 0x50 && d[1] == 0x4B && d[2] == 0x03 && d[3] == 0x04;
                default:
                    return false;
            }
        }

        // ---- Images -------------------------------------------------------

        private static bool ImageHasScriptContent(byte[] data, out string? problem)
        {
            problem = null;
            // Lower-case copy so the marker search is case-insensitive (images are small compared to the 50 MB cap).
            var lower = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                lower[i] = (b >= 0x41 && b <= 0x5A) ? (byte)(b + 32) : b;
            }

            foreach (var marker in ImageScriptMarkers)
            {
                if (new ReadOnlySpan<byte>(lower).IndexOf(new ReadOnlySpan<byte>(marker)) >= 0)
                {
                    problem = "script content embedded in image";
                    return true;
                }
            }
            return false;
        }

        // ---- Office Open XML (.docx / .xlsx) ------------------------------

        private static bool IsCleanOoxml(byte[] data, string ext, out string? problem)
        {
            problem = null;
            try
            {
                using var ms = new MemoryStream(data, writable: false);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

                if (zip.Entries.Count == 0 || zip.Entries.Count > 5000)
                {
                    problem = "unexpected file structure";
                    return false;
                }

                string requiredPrefix = ext == ".docx" ? "word/" : "xl/";
                string requiredContentType = ext == ".docx" ? "wordprocessingml" : "spreadsheetml";
                bool hasContentTypes = false;
                bool hasMainPart = false;
                string contentTypesText = string.Empty;

                foreach (var entry in zip.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    var lower = name.ToLowerInvariant();

                    if (lower.StartsWith("/") || lower.Contains("../"))
                    {
                        problem = "invalid file structure";
                        return false;
                    }
                    if (lower.EndsWith("vbaproject.bin") || lower.Contains("activex/"))
                    {
                        problem = "macros / ActiveX controls are not allowed";
                        return false;
                    }

                    var entryExt = Path.GetExtension(lower);
                    if (BlockedExtensions.Contains(entryExt) && entryExt != ".xml" && entryExt != ".svg")
                    {
                        problem = "executable or script file inside the document";
                        return false;
                    }
                    // Embedded objects: only embedded workbooks/documents (e.g. chart data) are tolerated.
                    if (lower.Contains("/embeddings/") && entryExt != ".xlsx" && entryExt != ".docx")
                    {
                        problem = "embedded object inside the document";
                        return false;
                    }

                    if (name == "[Content_Types].xml")
                    {
                        hasContentTypes = true;
                        contentTypesText = ReadEntryText(entry, 1024 * 1024);
                    }
                    if (lower.StartsWith(requiredPrefix)) hasMainPart = true;
                }

                if (!hasContentTypes || !hasMainPart
                    || contentTypesText.IndexOf(requiredContentType, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    problem = "not a valid Office document";
                    return false;
                }
                if (contentTypesText.IndexOf("macroEnabled", StringComparison.OrdinalIgnoreCase) >= 0
                    || contentTypesText.IndexOf("vbaProject", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    problem = "macro-enabled document";
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                problem = "corrupt or unreadable file";
                return false;
            }
        }

        private static string ReadEntryText(ZipArchiveEntry entry, int maxBytes)
        {
            using var s = entry.Open();
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (ms.Length + read > maxBytes) break;   // bounded read - protects against zip bombs
                ms.Write(buffer, 0, read);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        // ---- PDF ----------------------------------------------------------

        /// <summary>
        /// Scans the PDF's object syntax for names that mean JavaScript / launch actions / embedded files.
        /// Binary stream bodies are skipped (they would only produce false hits); compressed object streams
        /// (where PDF 1.5+ writers hide ordinary dictionaries) are inflated and scanned too.
        /// </summary>
        private static bool PdfHasActiveContent(byte[] d, int depth, out string? found)
        {
            found = null;
            int n = d.Length;
            var nameBuf = new char[32];
            bool sawObjStm = false, sawFlate = false;
            int i = 0;

            while (i < n)
            {
                byte b = d[i];

                if (b == (byte)'/')
                {
                    int len = ReadPdfName(d, i + 1, nameBuf, out int next);
                    if (len > 0 && len <= 16)
                    {
                        var name = new string(nameBuf, 0, len);
                        if (PdfBlockedNames.Contains(name))
                        {
                            found = "PDF contains /" + name;
                            return true;
                        }
                        if (name == "ObjStm") sawObjStm = true;
                        else if (name == "FlateDecode" || name == "Fl") sawFlate = true;
                    }
                    i = next > i ? next : i + 1;
                    continue;
                }

                // "stream" keyword (but not the "stream" inside "endstream")
                if (b == (byte)'s' && i + 7 < n && IsKeyword(d, i, "stream") && (i == 0 || d[i - 1] != (byte)'d'))
                {
                    int dataStart = i + 6;
                    if (d[dataStart] == 0x0D && dataStart + 1 < n && d[dataStart + 1] == 0x0A) dataStart += 2;
                    else if (d[dataStart] == 0x0A || d[dataStart] == 0x0D) dataStart += 1;
                    else { i++; continue; }

                    int rel = new ReadOnlySpan<byte>(d, dataStart, n - dataStart).IndexOf(new ReadOnlySpan<byte>(EndStreamBytes));
                    int dataEnd = rel < 0 ? n : dataStart + rel;

                    if (sawObjStm && sawFlate && depth < 1 && dataEnd > dataStart)
                    {
                        var inflated = TryInflate(d, dataStart, dataEnd - dataStart, 32 * 1024 * 1024);
                        if (inflated != null && PdfHasActiveContent(inflated, depth + 1, out found))
                            return true;
                    }
                    sawObjStm = false; sawFlate = false;
                    i = dataEnd;           // skip the binary body
                    continue;
                }

                i++;
            }
            return false;
        }

        private static bool IsKeyword(byte[] d, int pos, string word)
        {
            for (int k = 0; k < word.Length; k++)
                if (d[pos + k] != (byte)word[k]) return false;
            return true;
        }

        private static bool IsPdfDelimiter(byte c) =>
            c == 0 || c == 9 || c == 10 || c == 12 || c == 13 || c == 32 ||
            c == (byte)'(' || c == (byte)')' || c == (byte)'<' || c == (byte)'>' ||
            c == (byte)'[' || c == (byte)']' || c == (byte)'{' || c == (byte)'}' ||
            c == (byte)'/' || c == (byte)'%';

        private static int HexVal(byte c) =>
            c >= (byte)'0' && c <= (byte)'9' ? c - '0' :
            c >= (byte)'a' && c <= (byte)'f' ? c - 'a' + 10 :
            c >= (byte)'A' && c <= (byte)'F' ? c - 'A' + 10 : -1;

        /// <summary>Reads a PDF name starting after the '/', decoding #XX escapes (e.g. /J#61vaScript). Returns its length, or 0 if too long.</summary>
        private static int ReadPdfName(byte[] d, int start, char[] buf, out int next)
        {
            int j = start, len = 0;
            bool tooLong = false;
            while (j < d.Length && !IsPdfDelimiter(d[j]))
            {
                byte c = d[j];
                if (c == (byte)'#' && j + 2 < d.Length && HexVal(d[j + 1]) >= 0 && HexVal(d[j + 2]) >= 0)
                {
                    c = (byte)(HexVal(d[j + 1]) * 16 + HexVal(d[j + 2]));
                    j += 3;
                }
                else
                {
                    j++;
                }
                if (len < buf.Length) buf[len++] = (char)c; else tooLong = true;
            }
            next = j;
            return tooLong ? 0 : len;
        }

        private static byte[]? TryInflate(byte[] d, int offset, int count, int maxOutput)
        {
            try
            {
                using var input = new MemoryStream(d, offset, count, writable: false);
                using var z = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                var buffer = new byte[16384];
                int read;
                while ((read = z.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > maxOutput) return null;
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
            catch (Exception)
            {
                return null;   // not a deflate stream / encrypted - nothing to scan
            }
        }

        // ------------------------------------------------------------------
        // Helpers used by the upload handlers and the download endpoint
        // ------------------------------------------------------------------

        /// <summary>Removes path parts and characters that are unsafe in file names / headers / logs and limits the length.</summary>
        public static string SanitizeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "file";

            var name = fileName;
            int slash = name.LastIndexOfAny(new[] { '/', '\\' });
            if (slash >= 0) name = name.Substring(slash + 1);

            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (char.IsControl(ch) || "<>:\"|?*$&'`%;".IndexOf(ch) >= 0) sb.Append('_');
                else sb.Append(ch);
            }

            var cleaned = sb.ToString().Replace("..", "_").Trim().TrimEnd('.', ' ');
            if (cleaned.Length == 0) return "file";

            if (cleaned.Length > MaxFileNameLength)
            {
                var ext = Path.GetExtension(cleaned);
                if (ext.Length > 10) ext = string.Empty;
                cleaned = cleaned.Substring(0, MaxFileNameLength - ext.Length) + ext;
            }
            return cleaned;
        }

        /// <summary>Content type derived from the validated extension - never from the client-supplied header.</summary>
        public static string GetContentType(string? fileName)
        {
            return Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                // Legacy types: no longer accepted for upload, but older records may still be downloaded.
                ".doc" => "application/msword",
                ".xls" => "application/vnd.ms-excel",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        /// <summary>Writes a rejected upload to the application log (Serilog) for monitoring.</summary>
        public static void LogRejected(string? userId, string? fileName, string? reason)
        {
            Serilog.Log.Warning("Upload rejected. User={UserId} File={FileName} Reason={Reason}",
                SanitizeFileName(userId), SanitizeFileName(fileName), reason);
        }
    }
}
