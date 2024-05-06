using System.Diagnostics;

namespace EasyUploader
{

    public class ServerPlayerManager : IServerPlayerManager
    {
        public UploadStatus CurrentUploadStatus { get; set; }
        public string ExecName { get; set; }
        Process _playerProcess;

        public void UpdateUploadStatus(UploadStatus status) 
        {
            CurrentUploadStatus = status;
        }

        public bool RunPlayer(string args, Action<string> outputHandler)
        {
            // Prepare the process to run
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.Arguments = args;
            processInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.PATH_SERVERPLAYER, ExecName);
            processInfo.WindowStyle = ProcessWindowStyle.Normal;
            processInfo.CreateNoWindow = false;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;

            try
            {
                _playerProcess = Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                outputHandler(ex.Message);
                Console.WriteLine(ex.Message.ToString());
                return false;
            }

            _playerProcess.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            _playerProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            
            _playerProcess.BeginOutputReadLine();
            _playerProcess.BeginErrorReadLine();

            return true;

            void OutputHandler(object sendingProcess, DataReceivedEventArgs output)
            {
                if (output.Data != null)
                {
                    outputHandler.Invoke(output.Data);
                }
            }
        }

        public int StopPlayer() 
        {
            if (_playerProcess != null)
            {
                _playerProcess.CancelErrorRead();
                _playerProcess.CancelOutputRead();
                _playerProcess.Kill();
                _playerProcess.WaitForExit();
                return _playerProcess.ExitCode;
            }
            else return 0;
        }
    }
}
