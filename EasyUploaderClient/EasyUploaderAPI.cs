using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;

namespace EasyUploaderClient
{
    public class EasyUploaderAPI
    {
        public Action<float> Callback_OnUploadProgressUpdated;
        public Action<UploadStatus, Target> Callback_OnReceivedPlayerStatus;
        public Action<string> Callback_Log;
        public Action<int> Callback_PlayerStoppedConfirmation;
        public Action<int> Callback_PlayerUploadedConfirmation;
        public Action<string, float> Callback_OnPlayerUploadTotalProgressUpdated;
        public Action<string> Callback_OnReceivedPlayerOutput;

        HttpClient _httpClient;
        HttpResponseMessage _uploadStatusResponse;
        bool _readServerPlayerOutput;

        Target _serverPlayerOs;

        string _launchCommands;

        public void Init() 
        {
            HttpClientHandler _httpClientHandler = new HttpClientHandler();
            _httpClientHandler.AllowAutoRedirect = true;    

            _httpClient = new HttpClient(_httpClientHandler);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
        }

        /// <summary>
        /// executable name = (buildName+executable extension)
        /// </summary>
        /// <param name="fileLocation"></param>
        /// <param name="buildName"></param>
        /// <param name="executableName"></param>
        public async void SendFile(string fileLocation, string buildName, string executableName)
        {
            string compressedPath = Constants.PATH_COMPRESSED;

            if (File.Exists(compressedPath))
            {
                File.Delete(compressedPath);
                Console.WriteLine("Deleting old dnPacks");
            }

            if (!Directory.Exists(fileLocation))
                Directory.CreateDirectory(fileLocation);

            string burstInfoDoNotShipPath = Path.Combine(fileLocation, $"{buildName}_BurstDebugInformation_DoNotShip");

            if (Directory.Exists(burstInfoDoNotShipPath))
                Directory.Delete(burstInfoDoNotShipPath, true);

            ZipFile.CreateFromDirectory(fileLocation, compressedPath);


            var serverUrl = $"{Constants.DomainName}/Uploader/UploadFile";
            Callback_Log?.Invoke($"Started sending file on {serverUrl}");

            long fileSize = new FileInfo(compressedPath).Length;

            var chunkSize = 24000000; //~24mb, default limit for post req is 30mb 
            var buffer = new byte[chunkSize];

            using (var fileStream = File.OpenRead(compressedPath))
            {
                long bytesRead = 0;
                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {                    
                    var contentRange = $"bytes {fileStream.Position - bytesRead}-{fileStream.Position - 1}/{fileSize}";

                    var byteContent = new ByteArrayContent(buffer, 0, (int)bytesRead);
                    byteContent.Headers.TryAddWithoutValidation("Content-Range", contentRange);
                    byteContent.Headers.TryAddWithoutValidation("Execname", executableName);

                    byteContent.Headers.TryAddWithoutValidation("X-Filename", Path.GetFileName(compressedPath));

                    var response = await _httpClient.PostAsync(serverUrl, byteContent);
                    response.EnsureSuccessStatusCode();
                 
                    Callback_OnPlayerUploadTotalProgressUpdated?.Invoke("Uploading server player", (float)fileStream.Position / fileSize);
                }
            }

            Console.WriteLine("Upload complete.");

            Callback_PlayerUploadedConfirmation?.Invoke(200);
        }


        public async void RunServerPlayer()
        {
            var requestBody = string.Format("{{\"args\": \"{0}\"}}", _launchCommands);
            Callback_Log?.Invoke($"Req body: {requestBody}");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{Constants.DomainName}/Uploader/RunPlayer");
            request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            _uploadStatusResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (_uploadStatusResponse.IsSuccessStatusCode)
            {
                _readServerPlayerOutput = true;

                var stream = await _uploadStatusResponse.Content.ReadAsStreamAsync();
                var reader = new System.IO.StreamReader(stream);
                try
                {
                    while (_readServerPlayerOutput && !reader.EndOfStream)
                    {
                        string line = await reader.ReadLineAsync();
                        Console.WriteLine(line);
                        Callback_OnReceivedPlayerOutput?.Invoke(line);
                    }
                }
                catch { }
            }
            else
            {
                Console.WriteLine($"Failed to connect. Status code: {_uploadStatusResponse.StatusCode}");
            }


            Console.WriteLine("Reading output ended");
        }

        public async void StopServerPlayer() 
        {
            _readServerPlayerOutput = false;
            _uploadStatusResponse.Dispose();

            var response = await _httpClient.GetAsync($"{Constants.DomainName}/Uploader/StopPlayer");

            response.EnsureSuccessStatusCode();

            // Read the response content as a string
            string responseBody = await response.Content.ReadAsStringAsync();

            Callback_PlayerStoppedConfirmation?.Invoke(System.Convert.ToInt32(responseBody));
        }

        public async void GetServerStatus()
        {
            string endpoint = $"{Constants.DomainName}/Uploader/GetPlayerStatus";
            Callback_Log?.Invoke($"getting server status from {endpoint}");
            HttpResponseMessage response;

            try
            {
               response = await _httpClient.GetAsync(endpoint);
            }
            catch
            {
                Callback_OnReceivedPlayerStatus?.Invoke(UploadStatus.NotConnected, Target.Unknown);
                return;
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                string body = await response.Content.ReadAsStringAsync();
                string[] elements = body.Split('*');
                _serverPlayerOs = (Target)System.Convert.ToInt32(elements[2]);

                Callback_OnReceivedPlayerStatus?.Invoke((UploadStatus)System.Convert.ToInt32(elements[0]), _serverPlayerOs);
                Console.WriteLine($"{elements[0]} {elements[1]}");
            }
            else 
            {
                Callback_OnReceivedPlayerStatus?.Invoke(UploadStatus.NotConnected, Target.Unknown);
            }
        }

        public void SetUrl(string url) => Constants.DomainName = url;
        public void SetServerPlayerLaunchCommands(string args) => _launchCommands = args;

    }
    public enum UploadStatus
    {
        NotUploaded = 0,
        Uploading = 1,
        Uploaded = 2,
        BootFailed = 3,
        Running = 4,
        NotConnected = 5,
    }
    public enum Target
    {
        Unknown = -1,
        Linux = 0,
        Windows =1
    }
}
