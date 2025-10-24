// File: Scripts/Services/CopilotInjector.cs
// Single entry: PasteTextToCopilot(text).
// Safe UIA: process-scoped, top-window scoped, CSE-guarded FindFirst.

namespace GithubCopilotQuickQuestions.Scripts.Services
{
   using Microsoft.VisualStudio;
   using Microsoft.VisualStudio.Shell;
   using Microsoft.VisualStudio.Shell.Interop;

   using System;
   using System.Linq;
   using System.Runtime.ExceptionServices;
   using System.Runtime.InteropServices;
   using System.Security;
   using System.Threading;
   using System.Windows;
   using System.Windows.Automation;

   using static GithubCopilotQuickQuestions.Scripts.Utilities.Win32NativeMethodsUtils;

   using Condition = System.Windows.Automation.Condition;

   public static class CopilotInjector
   {
      public static bool PasteTextToCopilot(string text, int uiSettleDelayMs = 120, int findTimeoutMs = 800)
      {
         if (string.IsNullOrWhiteSpace(text))
         {
            return false;
         }

         try
         {
            Clipboard.SetDataObject(text, true);
         }
         catch
         {
            return false;
         }

         try
         {
            var executed = false;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
               {
                  await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                  var ui = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
                  if (ui == null)
                  {
                     throw new InvalidOperationException("SVsUIShell unavailable.");
                  }

                  ui.GetDialogOwnerHwnd(out var mainHwnd);
                  if (mainHwnd == IntPtr.Zero)
                  {
                     throw new InvalidOperationException("Main window HWND not found.");
                  }

                  SetForegroundWindow(mainHwnd);
                  GetWindowThreadProcessId(mainHwnd, out var devenvPid);

                  var prompt = FindCopilotPromptScopedByProcessSafe(devenvPid, findTimeoutMs);
                  if (prompt == null)
                  {
                     throw new InvalidOperationException("Copilot prompt not found.");
                  }

                  SafeSetFocus(prompt);

                  if (uiSettleDelayMs > 0)
                  {
                     Thread.Sleep(uiSettleDelayMs);
                  }

                  ui.PostExecCommand(
                  VSConstants.GUID_VSStandardCommandSet97,
                  (uint) VSConstants.VSStd97CmdID.Paste,
                  0,
                  null);

                  executed = true;
               });

            return executed;
         }
         catch
         {
            return false;
         }
      }

      private static AutomationElement FindCopilotPromptScopedByProcessSafe(int pid, int timeoutMs)
      {
         var deadline = Environment.TickCount + Math.Max(0, timeoutMs);

         while (true)
         {
            var pane = FindCopilotPaneByProcessSafe(pid);
            if (pane != null)
            {
               var prompt = FindPromptInPaneSafe(pane);
               if (prompt != null)
               {
                  return prompt;
               }
            }

            if (timeoutMs <= 0 || Environment.TickCount >= deadline)
            {
               return null;
            }

            Thread.Sleep(100);
         }
      }

      private static AutomationElement FindCopilotPaneByProcessSafe(int pid)
      {
         var desktop = AutomationElement.RootElement;
         if (desktop == null)
         {
            return null;
         }

         // Enumerate top-level windows once per probe. Avoid giant Subtree+proc filters which often crash providers.
         var tops = SafeFindAll(desktop, TreeScope.Children, Condition.TrueCondition);
         if (tops == null)
         {
            return null;
         }

         foreach (AutomationElement top in tops)
         {
            if (!SafeGetPid(top, out var p) || p != pid)
            {
               continue;
            }

            // 1) By AutomationId inside this top window
            var pane = SafeFindFirst(
            top,
            TreeScope.Subtree,
            new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
            new PropertyCondition(
            AutomationElement.AutomationIdProperty,
            "CopilotCommentsPane",
            PropertyConditionFlags.IgnoreCase)));

            if (pane != null)
            {
               return pane;
            }

            // 2) By common names
            var paneByName = SafeFindFirst(
            top,
            TreeScope.Subtree,
            new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
            new OrCondition(
            new PropertyCondition(
            AutomationElement.NameProperty,
            "GitHub Copilot Chat",
            PropertyConditionFlags.IgnoreCase),
            new PropertyCondition(AutomationElement.NameProperty, "Copilot", PropertyConditionFlags.IgnoreCase))));

            if (paneByName != null)
            {
               return paneByName;
            }

            // 3) Fallback: find prompt then parent
            var prompt = SafeFindFirst(
            top,
            TreeScope.Subtree,
            new PropertyCondition(
            AutomationElement.AutomationIdProperty,
            "CopilotPrompt",
            PropertyConditionFlags.IgnoreCase));
            if (prompt != null)
            {
               try
               {
                  return TreeWalker.ControlViewWalker.GetParent(prompt);
               }
               catch
               {
               }
            }
         }

         return null;
      }

      private static AutomationElement FindPromptInPaneSafe(AutomationElement pane)
      {
         // Prefer AutomationId
         var byId = SafeFindFirst(
         pane,
         TreeScope.Descendants,
         new PropertyCondition(
         AutomationElement.AutomationIdProperty,
         "CopilotPrompt",
         PropertyConditionFlags.IgnoreCase));
         if (byId != null)
         {
            return byId;
         }

         // Fallback to any enabled, focusable Edit
         return SafeFindFirst(
         pane,
         TreeScope.Descendants,
         new AndCondition(
         new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
         new PropertyCondition(AutomationElement.IsEnabledProperty, true),
         new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true)));
      }

      private static bool SafeGetPid(AutomationElement el, out int pid)
      {
         pid = 0;
         try
         {
            var obj = el.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty, true);
            if (obj is int v)
            {
               pid = v;
               return true;
            }
         }
         catch
         {
         }

         return false;
      }

      [HandleProcessCorruptedStateExceptions, SecurityCritical]
      private static AutomationElement SafeFindFirst(AutomationElement root, TreeScope scope, Condition cond)
      {
         try
         {
            return root.FindFirst(scope, cond);
         }
         catch (AccessViolationException)
         {
            return null;
         } // UIA provider CSE
         catch (ElementNotAvailableException)
         {
            return null;
         }
         catch (COMException)
         {
            return null;
         }
         catch
         {
            return null;
         }
      }

      [HandleProcessCorruptedStateExceptions, SecurityCritical]
      private static AutomationElementCollection SafeFindAll(AutomationElement root, TreeScope scope, Condition cond)
      {
         try
         {
            return root.FindAll(scope, cond);
         }
         catch (AccessViolationException)
         {
            return null;
         }
         catch (ElementNotAvailableException)
         {
            return null;
         }
         catch (COMException)
         {
            return null;
         }
         catch
         {
            return null;
         }
      }

      [HandleProcessCorruptedStateExceptions, SecurityCritical]
      private static void SafeSetFocus(AutomationElement el)
      {
         try
         {
            el.SetFocus();
         }
         catch
         {
         }
      }
   }
}
