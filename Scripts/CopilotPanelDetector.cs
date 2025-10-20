namespace GithubCopilotQuickQuestions.Scripts
{
   using EnvDTE;

   using Microsoft.VisualStudio.Shell;

   using System.Linq;
   using System.Threading.Tasks;

   internal static class CopilotPanelDetector
   {
      public static async Task HookAsync(AsyncPackage package)
      {
         await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

         if (!(await package.GetServiceAsync(typeof(DTE)) is DTE dte))
         {
            return;
         }

         Logger.Log("Hooked!");
         var windowEvents = dte.Events.WindowEvents;
         var firstLoad = true;
         var justOpened = false;

         windowEvents.WindowActivated += (gotFocus, lostFocus) =>
            {
               ThreadHelper.ThrowIfNotOnUIThread();

               // First load - execute command and show overlay
               if (firstLoad)
               {
                  firstLoad = false;
                  justOpened = true;

                  try
                  {
                     dte.ExecuteCommand("view.github.copilot.chat");
                  }
                  catch
                  {
                     // Not implemented
                  }

                  Task.Delay(100).Wait();

                  StartCopilot();

                  return;
               }

               // Skip the immediate Copilot open event after first load
               if (justOpened && gotFocus?.Caption != null && gotFocus.Caption.Contains("Copilot"))
               {
                  justOpened = false;
                  return;
               }

               // Monitor copilot window after first load
               if (gotFocus?.Caption != null && gotFocus.Caption.Contains("Copilot"))
               {
                  if (!CopilotOverlay.IsOpen)
                  {
                     StartCopilot();
                  }
               }
               else if (lostFocus?.Caption != null && lostFocus.Caption.Contains("Copilot"))
               {
                  if (CopilotOverlay.IsOpen)
                  {
                     CloseCopilot();
                     justOpened = false;
                  }
               }
            };

         windowEvents.WindowClosing += (window) =>
            {
               ThreadHelper.ThrowIfNotOnUIThread();

               if (window?.Caption != null && window.Caption.Contains("Copilot"))
               {
                  CloseCopilot();
                  justOpened = false;
               }
            };
      }

      private static void CloseCopilot()
      {
         Logger.Log("Copilot lost focus - hiding overlay");
         CopilotOverlay.Stop();
      }

      private static void StartCopilot()
      {
         Logger.Log("Copilot opened - showing overlay");
         CopilotOverlay.Start(
         new[] {"Option A", "Option B", "Option C"},
         width: 150,
         height: 24,
         pad: 8,
         offsetX: 190,
         offsetY: -40,
         darkMode: true);
      }
   }
}