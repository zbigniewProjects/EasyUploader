namespace EasyUploader
{
    public interface IFileHandler
    {
        public void DecompressFile(string source, string destination);
    }
}