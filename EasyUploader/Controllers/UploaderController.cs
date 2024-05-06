using Microsoft.AspNetCore.Mvc;
using EasyUploader.Contracts;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Text;
using DNUploader_Server;

namespace EasyUploader.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UploaderController : ControllerBase
    {
        private readonly IFileHandler _fileHandler;
        private readonly IServerPlayerManager _playerManager;

        public UploaderController(IFileHandler fileHandler, IServerPlayerManager serverPlayerManager)
        {
            _fileHandler = fileHandler;
            _playerManager = serverPlayerManager;
        }

        [HttpPost("RunPlayer")]
        public async Task RunGame(RunGameRequest req)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            StringBuilder stringBuilder = new StringBuilder();
            Console.WriteLine(req.args);
            //concurrent queue since two threads will be accessing and modyfing it, this req thread and game monitor thread
            ConcurrentQueue<string> outputs = new ConcurrentQueue<string>();
            if (!_playerManager.RunPlayer(req.args, BatchPlayerOutput)) 
            {
                //if player could not start then Queue should contain one string that describes error that occured, send it to client and close stream.
                if (outputs.TryDequeue(out string msg)){
                    await Response.WriteAsync($"ERROR WHILE BOOTING GAME BUILD: \n {msg}");
                    await Response.Body.FlushAsync();
                }
                return;
            }

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                while (outputs.TryDequeue(out string output))
                {
                    stringBuilder.Append($"{output}\n");
                }
                await Response.WriteAsync(stringBuilder.ToString());
                await Response.Body.FlushAsync();
                stringBuilder.Clear();
            }

            Console.WriteLine("Process cancelled");

            void BatchPlayerOutput(string output)
            {
                outputs.Enqueue(output);
            }
        }

        [HttpPost("UploadFile")]
        public async Task<IActionResult> UploadFile()
        {
            if (!Request.ContentLength.HasValue || Request.ContentLength <= 0)
            {
                return BadRequest("No content found in the request.");
            }

            _playerManager.ExecName = Request.Headers["Execname"].ToString();
            _playerManager.CurrentUploadStatus = UploadStatus.Uploading;

            var playerTargetDestination = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.PATH_SERVERPLAYER);
            var uploadFileName = Path.Combine(playerTargetDestination, Request.Headers["X-Filename"]);
            var contentRange = Request.Headers["Content-Range"].ToString();

            // Extract start and end byte positions from the Content-Range header
            var rangeParts = contentRange.Replace("bytes ", "").Split('-');
            var startByte = long.Parse(rangeParts[0]);
            var endByte = long.Parse(rangeParts[1].Split('/')[0]);

            // Write the received chunk to the file
            using (var fileStream = new FileStream(uploadFileName, FileMode.OpenOrCreate, FileAccess.Write))
            {
                fileStream.Seek(startByte, SeekOrigin.Begin);
                await Request.Body.CopyToAsync(fileStream);
            }

            // Check if this is the last chunk
            if (endByte == (Request.ContentLength - 1))
            {
                // File upload is complete
                Console.WriteLine("completed");
                _fileHandler.DecompressFile(uploadFileName, playerTargetDestination);

                //if linux => give build launcher executable rights
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string cmd = $"chmod +x ./build/{_playerManager.ExecName}";
                    Terminal.ExecuteLinuxCommand(cmd);
                }

                return Ok();
            }

            _playerManager.CurrentUploadStatus = UploadStatus.Uploaded;
            // File upload is not complete yet, return a 200 response with the byte range expected in the next request
            return StatusCode(208, $"Upload successful, continue from byte {endByte + 1}");
        }


        [HttpGet("StopPlayer")]
        public IActionResult StopPlayer() 
        {
            return Ok(_playerManager.StopPlayer());
        }

        [HttpGet("GetPlayerStatus")]
        public IActionResult GetPlayerStatus()
        {
            int os = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? 0 : 1;
            Console.WriteLine("received status req");
            return Ok($"{(int)_playerManager.CurrentUploadStatus}*{_playerManager.ExecName}*{os}");
        }
    }
}
