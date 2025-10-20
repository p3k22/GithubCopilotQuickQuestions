namespace GithubCopilotQuickQuestions.Scripts
{
   using Microsoft.VisualStudio;
   using Microsoft.VisualStudio.Shell;
   using Microsoft.VisualStudio.Shell.Interop;

   using System;
   using System.Linq;
   using System.Runtime.InteropServices;
   using System.Threading;
   using System.Windows;
   using System.Windows.Automation;

   public static class CopilotInjector
   {
      private const uint GA_ROOT = 2;

      private const int SW_SHOW = 5;

      public static bool InjectText(string text, int delayMs = 300)
      {
         Logger.Log($"[CopilotInjector] InjectText start len={(text ?? "").Length}");
         if (string.IsNullOrWhiteSpace(text))
         {
            return false;
         }

         try
         {
            Clipboard.SetDataObject(text, true);
         }
         catch (Exception ex)
         {
            Logger.Log("[CopilotInjector] Clipboard failed: " + ex.Message);
            return false;
         }

         if (!GetCursorPos(out var pt))
         {
            return false;
         }

         var hwnd = WindowFromPoint(pt);
         if (!IsWindow(hwnd))
         {
            return false;
         }

         var root = GetAncestor(hwnd, GA_ROOT);
         if (!IsWindow(root))
         {
            root = hwnd;
         }

         BringToForegroundAndFocus(root, hwnd);

         if (delayMs > 0)
         {
            Thread.Sleep(delayMs);
         }

         var ok = ExecVsPaste();
         Logger.Log(ok ? "[CopilotInjector] Edit.Paste executed" : "[CopilotInjector] Edit.Paste failed");
         return ok;
      }

      [DllImport("user32.dll")]
      private static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);

      // Brings the VS main window to the foreground, finds the Copilot chat input by AutomationId,
      // and sets keyboard focus to it so VS's Edit.Paste targets Copilot.
      public static bool FocusCopilotChatInput()
      {
         try
         {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
               {
                  await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                  var ui = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
                  if (ui == null)
                  {
                     throw new InvalidOperationException("SVsUIShell unavailable.");
                  }

                  IntPtr mainHwnd;
                  ui.GetDialogOwnerHwnd(out mainHwnd);
                  if (mainHwnd == IntPtr.Zero)
                  {
                     throw new InvalidOperationException("Main window HWND not found.");
                  }

                  SetForegroundWindow(mainHwnd);

                  var root = AutomationElement.FromHandle(mainHwnd);
                  if (root == null)
                  {
                     throw new InvalidOperationException("Automation root not found.");
                  }

                  var condPromptId = new PropertyCondition(
                  AutomationElement.AutomationIdProperty,
                  "CopilotPrompt",
                  PropertyConditionFlags.IgnoreCase);
                  var condEditType = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                  var condPaneId = new PropertyCondition(
                  AutomationElement.AutomationIdProperty,
                  "CopilotCommentsPane",
                  PropertyConditionFlags.IgnoreCase);
                  var condInPane = new AndCondition(
                  condEditType,
                  new PropertyCondition(AutomationElement.IsEnabledProperty, true));

                  var prompt = root.FindFirst(
                  TreeScope.Descendants,
                  new OrCondition(condPromptId, new AndCondition(condInPane, condPaneId)));

                  if (prompt == null)
                  {
                     throw new InvalidOperationException("Copilot chat input not found.");
                  }

                  prompt.SetFocus();
               });

            return true;
         }
         catch
         {
            return false;
         }
      }

      private static void BringToForegroundAndFocus(IntPtr root, IntPtr target)
      {
         try
         {
            ShowWindow(root, SW_SHOW);
         }
         catch
         {
         }

         var vsTid = GetWindowThreadProcessId(root, out _);
         var curTid = GetCurrentThreadId();
         var attached = false;
         try
         {
            if (vsTid != 0 && vsTid != curTid)
            {
               attached = AttachThreadInput(curTid, vsTid, true);
            }

            SetForegroundWindow(root);
            SetFocus(target);
            Thread.Sleep(30);
         }
         finally
         {
            if (attached)
            {
               AttachThreadInput(curTid, vsTid, false);
            }
         }
      }

      private static bool ExecVsPaste()
      {
         try
         {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
               {
                  await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                  var ui = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
                  ui?.PostExecCommand(
                  VSConstants.GUID_VSStandardCommandSet97,
                  (uint) VSConstants.VSStd97CmdID.Paste,
                  0,
                  null);
               });
            return true;
         }
         catch (Exception ex)
         {
            Logger.Log("[CopilotInjector] ExecVsPaste error: " + ex.Message);
            return false;
         }
      }

      [DllImport("user32.dll")]
      private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

      [DllImport("kernel32.dll")]
      private static extern int GetCurrentThreadId();

      [DllImport("user32.dll")]
      private static extern bool GetCursorPos(out POINT lpPoint);

      [DllImport("user32.dll")]
      private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

      [DllImport("user32.dll")]
      private static extern bool IsWindow(IntPtr hWnd);

      [DllImport("user32.dll")]
      private static extern IntPtr SetFocus(IntPtr hWnd);

      [DllImport("user32.dll")]
      private static extern bool SetForegroundWindow(IntPtr hWnd);

      [DllImport("user32.dll")]
      private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

      [DllImport("user32.dll")]
      private static extern IntPtr WindowFromPoint(POINT pt);

      [StructLayout(LayoutKind.Sequential)]
      public struct POINT
      {
         public int X, Y;
      }

      [StructLayout(LayoutKind.Sequential)]
      public struct RECT
      {
         public int Left, Top, Right, Bottom;
      }
   }
}