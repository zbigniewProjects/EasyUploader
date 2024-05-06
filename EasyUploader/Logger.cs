using System.Text;

namespace EasyUploader
{
    public class Logger
    {
        StringBuilder _batchedMessages = new StringBuilder();

        public void SendLog(string log) 
        {
           _batchedMessages.Append($"{DateTime.Now}: {log}");
        }

        public string BatchLogs() 
        {
            string batchedLogs = _batchedMessages.ToString();
            _batchedMessages.Clear();
            return batchedLogs;
        }
    }
}
