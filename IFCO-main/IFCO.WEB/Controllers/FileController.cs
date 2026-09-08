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
            return File(file.FileData, file.FileType ?? "application/octet-stream", file.FileName);
        }
    }
}