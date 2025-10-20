namespace VSOnEventAction.Scripts
{
   using GithubCopilotQuickQuestions.Scripts;

   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Windows.Forms;

   public static class ConfigReader
   {
      private static Dictionary<string, string> _questionSelections;

      internal static string ConfigPath
      {
         get
         {
            var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GithubCopilotQuickQuestions");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "copilot-quick-questions.txt");
         }
      }

      public static Dictionary<string, string> GetSelections()
      {
         _questionSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         EnsureConfigFileExists();

         var allLines = File.ReadAllLines(ConfigPath);
         string currentTitle = null;

         foreach (var raw in allLines)
         {
            if (string.IsNullOrWhiteSpace(raw))
            {
               // blank line separates entries
               currentTitle = null;
               continue;
            }

            var line = raw.Trim();

            // text lines start with '##'
            if (line.StartsWith("##", StringComparison.Ordinal))
            {
               var text = line.Length > 2 ? line.Substring(2).Trim() : string.Empty;
               if (currentTitle != null)
               {
                  if (string.IsNullOrEmpty(_questionSelections[currentTitle]))
                  {
                     _questionSelections[currentTitle] = text;
                  }
                  else
                  {
                     // append additional text lines
                     _questionSelections[currentTitle] = _questionSelections[currentTitle] + Environment.NewLine + text;
                  }
               }

               continue;
            }

            // title lines start with single '#'
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
               // ignore lines that start with more than one '#' already handled above
               if (line.StartsWith("##", StringComparison.Ordinal))
               {
                  continue;
               }

               var title = line.Length > 1 ? line.Substring(1).Trim() : string.Empty;
               if (string.IsNullOrEmpty(title))
               {
                  currentTitle = null;
                  continue;
               }

               if (!_questionSelections.ContainsKey(title))
               {
                  _questionSelections[title] = string.Empty;
               }

               currentTitle = title;
               continue;
            }

            // non-prefixed lines: treat as additional text for current title if present
            if (currentTitle != null)
            {
               if (string.IsNullOrEmpty(_questionSelections[currentTitle]))
               {
                  _questionSelections[currentTitle] = line;
               }
               else
               {
                  _questionSelections[currentTitle] = _questionSelections[currentTitle] + Environment.NewLine + line;
               }
            }
         }

         return _questionSelections;
      }

      public static void SelectQuestion(object sender, EventArgs e)
      {
         if (!(sender is ComboBox cbx))
         {
            return;
         }

         if (cbx.SelectedIndex < 0)
         {
            return;
         }

         Logger.Log("Updated Index; " + cbx.SelectedIndex);

         var selections = GetSelections();
         var selectionIndex = cbx.SelectedIndex;

         var key = selections.Keys.ToArray()[selectionIndex];

         if (_questionSelections.TryGetValue(key, out var value))
         {
            CopilotInjector.FocusCopilotChatInput();
            CopilotInjector.FocusCopilotChatInput();
            CopilotInjector.InjectText(value);
            selectionIndex = 0;
            cbx.SelectedIndex = selectionIndex;
            cbx.Refresh();
         }
      }

      /// <summary>
      ///    Ensures that the configuration file exists.
      ///    If it does not, creates it with default quick question entries.
      /// </summary>
      private static void EnsureConfigFileExists()
      {
         var path = ConfigPath;

         if (File.Exists(path))
         {
            return;
         }

         const string DefaultContent = @"#~ Select Quick Question ~
##

# Add XML Summaries
## Add / Edit xml summaries for all functions and classes. Use inline comments above fields and properties
";

         File.WriteAllText(path, DefaultContent);
      }
   }
}