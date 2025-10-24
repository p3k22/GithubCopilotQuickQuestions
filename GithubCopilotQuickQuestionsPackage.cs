namespace GithubCopilotQuickQuestions
{
   using EnvDTE;

   using GithubCopilotQuickQuestions.Scripts.Overlay;
   using GithubCopilotQuickQuestions.Scripts.Services;
   using GithubCopilotQuickQuestions.Scripts.Utilities;

   using Microsoft.VisualStudio.Shell;
   using Microsoft.VisualStudio.Shell.Interop;

   using System;
   using System.Linq;
   using System.Runtime.InteropServices;
   using System.Threading;
   using System.Threading.Tasks;
   using System.Windows;

   using Window = EnvDTE.Window;

   [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
   [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
   [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
   [Guid(PACKAGE_GUID_STRING)]
   public sealed class GithubCopilotQuickQuestionsPackage : AsyncPackage
   {
      // GithubCopilotQuickQuestionsPackage GUID string.
      private const string PACKAGE_GUID_STRING = "06da98f4-5d14-4371-a5b5-15188ea37bf5";

      // Flag to let us know if we've already tried auto-opening the pane on start up.
      private bool _triedAutoOpen;

      protected override async Task InitializeAsync(
         CancellationToken cancellationToken,
         IProgress<ServiceProgressData> progress)
      {
         await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

         // Start monitoring in background, don't block initialization
         _ = Task.Run(async () => await MonitorAndStartOverlayAsync(cancellationToken), cancellationToken);
      }

      private static bool IsAutoLoadingCopilot()
      {
         var s = ConfigReader.GetSetting(GlobalConstants.CONFIG_KEY_OVERLAY_AUTOLOAD);
         return s != null && s.Equals("true", StringComparison.OrdinalIgnoreCase);
      }

      private async Task<bool> IsCopilotChatVisibleAsync()
      {
         await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

         try
         {
            var dte = await GetServiceAsync(typeof(DTE)) as DTE;
            if (dte == null)
            {
               return false;
            }

            // Check if main window is actually ready
            if (dte.MainWindow?.Visible != true) // <--- THIS FUCKIN LINE!!!!
            {
               return false;
            }

            foreach (Window window in dte.Windows)
            {
               if (window.Caption.Contains(GlobalConstants.GITHUB_COPILOT_PANE_NAME))
               {
                  return true;
               }
            }
         }
         catch
         {
            // Swallow by design
         }

         return false;
      }

      private async Task MonitorAndStartOverlayAsync(CancellationToken cancellationToken)
      {
         var isAutoLoading = IsAutoLoadingCopilot();
         while (!cancellationToken.IsCancellationRequested)
         {
            if (isAutoLoading && !_triedAutoOpen)
            {
               _triedAutoOpen = true;
               try
               {
                  await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                  var dte = await GetServiceAsync(typeof(DTE)) as DTE;
                  if (dte == null)
                  {
                     return;
                  }

                  Logger.Log("Copilot pane not open on start. Opening!");
                  dte.ExecuteCommand("view.github.copilot.chat");
               }
               catch
               {
                  // Swallow by design
               }
            }

            if (await IsCopilotChatVisibleAsync())
            {
               await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
               var overlay = new ComboBoxPaneOverlay(
               this,
               GlobalConstants.GITHUB_COPILOT_PANE_NAME,
               new Size(280, 28),
               new Thickness(12, 12, 12, 12));

               Logger.Log("Started Combo Overlay");

               _ = overlay.StartAsync(CancellationToken.None);
               break;
            }

            await Task.Delay(500, cancellationToken);
         }
      }
   }
}