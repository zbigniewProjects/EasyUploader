using System.IO.Compression;

namespace EasyUploader
{
    public class FileHandler : IFileHandler
    {
        public void DecompressFile(string source, string destination)
        {
            try
            {
                ZipFile.ExtractToDirectory(source, destination, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not extract received build: {ex.Message}");
                return;
            }
            finally
            {
                //delete zip file, it is no longer needed
                File.Delete(source);
            }
        }
    }
}
