namespace GithubCopilotQuickQuestions.Scripts
{
   using Microsoft.VisualStudio.Shell;
   using Microsoft.VisualStudio.Shell.Interop;

   using System;
   using System.IO;
   using System.Linq;

   internal static class Logger
   {
      internal static string LogPath
      {
         get
         {
            var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GithubCopilotQuickQuestions");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "log.txt");
         }
      }

      internal static void Log(string msg)
      {
         try
         {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
         }
         catch
         {
         }

         try
         {
            var al = Package.GetGlobalService(typeof(SVsActivityLog)) as IVsActivityLog;
            al?.LogEntry((uint) __ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, "GithubCopilotQuickQuestions", msg);
         }
         catch
         {
         }
      }
   }
}