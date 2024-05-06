using static System.Net.WebRequestMethods;

namespace EasyUploaderClient
{
    public static class Constants
    {
        public static readonly string PATH_COMPRESSED = "Temp/player.zip";

        //public static string PATH_PLAYERFOLDER { set; get; } = "build"; //what user will choose in our unity editor window
        //public static string PlayerExecName = "game"; //must be with extension, since we will send it to server so he knows what to launch

        public static string DomainName { get; set; } = "http://localhost:5000";
    }
}
