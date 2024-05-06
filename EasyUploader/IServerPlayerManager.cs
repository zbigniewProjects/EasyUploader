
namespace EasyUploader
{
    public interface IServerPlayerManager
    {
        public UploadStatus CurrentUploadStatus { get; set; }
        public bool RunPlayer(string args, Action<string> outputHandler);
        int StopPlayer();

        public string ExecName { get; set; }
    }
}
public enum UploadStatus
{
    NotUploaded,
    Uploading,
    Uploaded,
    BootFailed,
    Running
}
