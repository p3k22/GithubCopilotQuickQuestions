namespace GithubCopilotQuickQuestions.Scripts.Utilities
{
   using System;
   using System.IO;
   using System.Linq;

   public static class PathUtils
   {
      /// <summary>
      ///    Builds a file path in the user's LocalApplicationData folder under the specified project directory,
      ///    optionally creating that directory, and returns the full path to <paramref name="fileName" /> with the given
      ///    extension.
      /// </summary>
      /// <param name="fileName">Base name of the file without extension.</param>
      /// <param name="projectName">
      ///    Subdirectory under LocalApplicationData to contain the file. Default is
      ///    "GithubCopilotQuickQuestions".
      /// </param>
      /// <param name="fileExtension">File extension including the leading dot. Default is ".txt".</param>
      /// <param name="createDirectoryIfNotFound">
      ///    When true, creates the project directory if it does not exist. When false, the directory is not created.
      /// </param>
      /// <returns>The full file system path for the requested file.</returns>
      public static string GetFilePath(
         string fileName,
         string projectName = "GithubCopilotQuickQuestions",
         string fileExtension = ".txt",
         bool createDirectoryIfNotFound = true)
      {
         var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), projectName);

         if (createDirectoryIfNotFound)
         {
            Directory.CreateDirectory(dir);
         }

         return Path.Combine(dir, fileName + fileExtension);
      }
   }
}