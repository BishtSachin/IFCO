using IFCO.WEB.Helpers;
using IFCO.WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IFCO.WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly OracleService _dbService;

        public FileController(OracleService dbService)
        {
            _dbService = dbService;
        }

        [HttpGet("download/{fileSqNo}")]
        public async Task<IActionResult> DownloadFile(int fileSqNo)
        {
            var file = await _dbService.GetFileByIdAsync(fileSqNo);

            if (file == null || file.FileData == null)
            {
                return NotFound();
            }
            // Content type comes from the file's extension (not from whatever the browser sent at upload time),
            // and passing a file name makes ASP.NET send "Content-Disposition: attachment" (forced download).
            return File(file.FileData, FileUploadValidator.GetContentType(file.FileName), FileUploadValidator.SanitizeFileName(file.FileName));
        }

        // NEW (Point 4): download a file that was carried into RCM_POINT_FILES_ARCHIVE
        // when its point was archived at quarter close.
        [HttpGet("download-archived/{archiveFileSqNo}")]
        public async Task<IActionResult> DownloadArchivedFile(int archiveFileSqNo)
        {
            var file = await _dbService.GetArchivedFileByIdAsync(archiveFileSqNo);

            if (file == null || file.FileData == null)
            {
                return NotFound();
            }
            // Content type comes from the file's extension (not from whatever the browser sent at upload time),
            // and passing a file name makes ASP.NET send "Content-Disposition: attachment" (forced download).
            return File(file.FileData, FileUploadValidator.GetContentType(file.FileName), FileUploadValidator.SanitizeFileName(file.FileName));
        }
    }
}