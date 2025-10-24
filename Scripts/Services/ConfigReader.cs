namespace GithubCopilotQuickQuestions.Scripts.Services
{
   using GithubCopilotQuickQuestions.Scripts.Utilities;

   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;

   /// <summary>
   ///    Manages reading and parsing of the quick questions configuration file.
   /// </summary>
   public static class ConfigReader
   {
      /// <summary>
      ///    Path to the configuration file.
      /// </summary>
      private static string _configPath;

      /// <summary>
      ///    Retrieves the available quick questions from the configuration file.
      /// </summary>
      /// <returns>Dictionary of question titles and their corresponding prompt text.</returns>
      public static Dictionary<string, string> GetQuestions()
      {
         EnsureConfigFileExists();
         var content = File.ReadAllText(_configPath);
         return ParseQuestions(content);
      }

      /// <summary>
      ///    Handles the selection of a quick question from the ComboBox and injects it into Copilot.
      /// </summary>
      /// <param name="sender">The ComboBox control that triggered the event.</param>
      /// <param name="e">Event arguments.</param>
      public static string GetSelectedQuestion(int selectedIndex, string questionKey)
      {
         if (selectedIndex < 0 || string.IsNullOrEmpty(questionKey))
         {
            return string.Empty;
         }

         var selections = GetQuestions();
         if (!selections.TryGetValue(questionKey, out var fullQuestion))
         {
            return string.Empty;
         }

         Logger.Log("Updated Index; " + selectedIndex + ", " + fullQuestion);

         return fullQuestion;
      }

      /// <summary>
      ///    Ensures the configuration file exists. Creates it with default content if missing.
      /// </summary>
      private static void EnsureConfigFileExists()
      {
         _configPath = PathUtils.GetFilePath("copilot-quick-questions", fileExtension: ".ini");

         if (File.Exists(_configPath))
         {
            return;
         }

         // Default configuration with sample questions
         const string DefaultContent = """
                                       [Settings]
                                       autoLoadCopilotChatOnVsStart=true
                                       logging=false

                                       [Questions]
                                       ~ Select Quick Question ~=
                                       Add XML Summaries=Add / Edit xml summaries for all functions and classes. Use inline comments above fields and properties
                                       Refactor Code=Refactor this code to improve readability and performance. Follow SOLID principles.
                                       """;

         File.WriteAllText(_configPath, DefaultContent);
      }

      /// <summary>
      ///    Parses the INI configuration file and extracts questions from the [Questions] section.
      /// </summary>
      /// <param name="content">Raw content of the configuration file.</param>
      /// <returns>Dictionary of question titles mapped to their prompt text.</returns>
      private static Dictionary<string, string> ParseQuestions(string content)
      {
         if (string.IsNullOrEmpty(content))
         {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         }

         var questions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         var currentSection = "";

         foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
         {
            var trimmed = line.Trim();

            // Parse section headers
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
               currentSection = trimmed.Substring(1, trimmed.Length - 2);
               continue;
            }

            // Parse key=value pairs within the Questions section
            if (trimmed.Contains("=") && currentSection.Equals("Questions", StringComparison.OrdinalIgnoreCase))
            {
               var parts = trimmed.Split(['='], 2);
               var key = parts[0].Trim();
               var value = parts.Length > 1 ? parts[1].Trim() : "";
               questions[key] = value;
            }
         }

         return questions;
      }

      /// <summary>
      ///    Retrieves a specific setting value from the configuration file.
      /// </summary>
      /// <param name="key">The setting key to retrieve.</param>
      /// <param name="defaultValue">Default value if setting is not found.</param>
      /// <returns>The setting value, or default value if not found.</returns>
      public static string GetSetting(string key, string defaultValue = "")
      {
         EnsureConfigFileExists();
         var content = File.ReadAllText(_configPath);
         var settings = ParseSettings(content);
         return settings.TryGetValue(key, out var value) ? value : defaultValue;
      }

      /// <summary>
      ///    Retrieves all settings from the configuration file.
      /// </summary>
      /// <returns>Dictionary of all settings.</returns>
      public static Dictionary<string, string> GetAllSettings()
      {
         EnsureConfigFileExists();
         var content = File.ReadAllText(_configPath);
         return ParseSettings(content);
      }

      /// <summary>
      ///    Parses the INI configuration file and extracts settings from the [Settings] section.
      /// </summary>
      /// <param name="content">Raw content of the configuration file.</param>
      /// <returns>Dictionary of setting keys mapped to their values.</returns>
      private static Dictionary<string, string> ParseSettings(string content)
      {
         if (string.IsNullOrEmpty(content))
         {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         }

         var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         var currentSection = "";

         foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
         {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
               currentSection = trimmed.Substring(1, trimmed.Length - 2);
               continue;
            }

            if (trimmed.Contains("=") && currentSection.Equals("Settings", StringComparison.OrdinalIgnoreCase))
            {
               var parts = trimmed.Split(['='], 2);
               var key = parts[0].Trim();
               var value = parts.Length > 1 ? parts[1].Trim() : "";
               settings[key] = value;
            }
         }

         return settings;
      }
   }
}