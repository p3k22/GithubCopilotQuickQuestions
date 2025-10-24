namespace GithubCopilotQuickQuestions.Scripts.Utilities
{
   using GithubCopilotQuickQuestions.Scripts.Services;

   using System;
   using System.IO;
   using System.Linq;

   internal static class Logger
   {
      private static readonly bool LoggingEnabled;

      static Logger()
      {
         LoggingEnabled = IsLoggingEnabled();
      }

      internal static void Log(string msg)
      {
         if (!LoggingEnabled)
         {
            return;
         }

         try
         {
            var path = PathUtils.GetFilePath("Log");
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
         }
         catch (Exception)
         {
            // Logging must not propagate exceptions.
         }
      }

      private static bool IsLoggingEnabled()
      {
         var s = ConfigReader.GetSetting(GlobalConstants.CONFIG_KEY_ENABLE_LOGGING);
         return s != null && s.Equals("true", StringComparison.OrdinalIgnoreCase);
      }
   }
}