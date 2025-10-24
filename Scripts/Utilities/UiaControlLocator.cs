namespace GithubCopilotQuickQuestions.Scripts.Utilities
{
   using Microsoft.VisualStudio.Shell;

   using System;
   using System.Linq;
   using System.Runtime.InteropServices;
   using System.Windows;
   using System.Windows.Automation;

   using Condition = System.Windows.Automation.Condition;

   /// <summary>
   ///    Utility class for locating UI Automation controls within Visual Studio tool windows.
   /// </summary>
   internal static class UiaControlLocator
   {
      /// <summary>
      ///    Gets the bounding rectangle of a control in screen coordinates.
      /// </summary>
      /// <param name="vsMainHwnd">Handle to the Visual Studio main window.</param>
      /// <param name="paneName">Name of the pane containing the control.</param>
      /// <param name="controlType">Type of control to locate.</param>
      /// <param name="automationId">Automation ID of the control (optional).</param>
      /// <param name="controlName">Name of the control (optional).</param>
      /// <param name="rect">Output rectangle in screen coordinates.</param>
      /// <returns>True if control was found and rectangle obtained, false otherwise.</returns>
      internal static bool TryGetControlRectScreen(
         IntPtr vsMainHwnd,
         string paneName,
         ControlType controlType,
         string automationId,
         string controlName,
         out Rect rect)
      {
         ThreadHelper.ThrowIfNotOnUIThread();
         rect = Rect.Empty;

         // Locate the control within the pane
         if (!TryGetControlInPane(vsMainHwnd, paneName, controlType, automationId, controlName, out var control))
         {
            return false;
         }

         // Get the control's bounding rectangle
         return TryGetElementRect(control, out rect);
      }

      /// <summary>
      ///    Gets the bounding rectangle of a pane in screen coordinates.
      /// </summary>
      /// <param name="vsMainHwnd">Handle to the Visual Studio main window.</param>
      /// <param name="paneName">Name of the pane to locate.</param>
      /// <param name="rect">Output rectangle in screen coordinates.</param>
      /// <returns>True if pane was found and rectangle obtained, false otherwise.</returns>
      internal static bool TryGetPaneRectScreen(IntPtr vsMainHwnd, string paneName, out Rect rect)
      {
         ThreadHelper.ThrowIfNotOnUIThread();
         rect = Rect.Empty;

         // Locate the pane
         if (!TryGetPane(vsMainHwnd, paneName, out var pane))
         {
            return false;
         }

         // Get the pane's bounding rectangle
         return TryGetElementRect(pane, out rect);
      }

      /// <summary>
      ///    Safely attempts to find the first matching element, catching common UI Automation exceptions.
      /// </summary>
      /// <param name="root">Root element to search from.</param>
      /// <param name="scope">Scope of the search.</param>
      /// <param name="cond">Condition to match.</param>
      /// <returns>The first matching element, or null if not found or error occurred.</returns>
      private static AutomationElement SafeFindFirst(AutomationElement root, TreeScope scope, Condition cond)
      {
         try
         {
            return root.FindFirst(scope, cond);
         }
         catch (ElementNotAvailableException)
         {
            // Element became unavailable during search
            return null;
         }
         catch (COMException)
         {
            // COM error during automation
            return null;
         }
         catch
         {
            // Any other unexpected error
            return null;
         }
      }

      /// <summary>
      ///    Finds a child control inside a pane by type, automation ID, and name.
      /// </summary>
      /// <param name="vsMainHwnd">Handle to the Visual Studio main window.</param>
      /// <param name="paneName">Name of the pane containing the control.</param>
      /// <param name="controlType">Type of control to locate.</param>
      /// <param name="automationId">Automation ID of the control (optional).</param>
      /// <param name="controlName">Name of the control (optional).</param>
      /// <param name="control">Output automation element for the control.</param>
      /// <returns>True if control was found, false otherwise.</returns>
      private static bool TryGetControlInPane(
         IntPtr vsMainHwnd,
         string paneName,
         ControlType controlType,
         string automationId,
         string controlName,
         out AutomationElement control)
      {
         ThreadHelper.ThrowIfNotOnUIThread();
         control = null;

         // First locate the containing pane
         if (!TryGetPane(vsMainHwnd, paneName, out var pane))
         {
            return false;
         }

         // Build condition for control search
         // Match control type, and optionally automation ID and name
         var controlCond = new AndCondition(
         new PropertyCondition(AutomationElement.ControlTypeProperty, controlType),
         string.IsNullOrEmpty(automationId) ?
            Condition.TrueCondition :
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId),
         string.IsNullOrEmpty(controlName) ?
            Condition.TrueCondition :
            new PropertyCondition(AutomationElement.NameProperty, controlName));

         try
         {
            // Create cache request for performance
            var cr = new CacheRequest();
            {
               cr.Add(AutomationElement.BoundingRectangleProperty);
               cr.Add(AutomationElement.IsOffscreenProperty);
               cr.Add(AutomationElement.NameProperty);
               cr.Add(AutomationElement.ClassNameProperty);
               cr.Add(AutomationElement.AutomationIdProperty);
               cr.TreeScope = TreeScope.Subtree;

               // Search with caching active
               using (cr.Activate())
               {
                  control = SafeFindFirst(pane, TreeScope.Subtree, controlCond);
                  return control != null;
               }
            }
         }
         catch
         {
            // Any error during search
            control = null;
            return false;
         }
      }

      /// <summary>
      ///    Reads the bounding rectangle of an automation element with offscreen checking.
      /// </summary>
      /// <param name="el">Automation element to query.</param>
      /// <param name="rect">Output bounding rectangle.</param>
      /// <returns>True if rectangle was obtained and element is onscreen, false otherwise.</returns>
      private static bool TryGetElementRect(AutomationElement el, out Rect rect)
      {
         rect = Rect.Empty;

         try
         {
            // Check if element is offscreen
            var isOffObj = el.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty, true);
            if (isOffObj is bool isOff && isOff)
            {
               return false;
            }
         }
         catch
         {
            // Error reading offscreen property
            return false;
         }

         try
         {
            // Get bounding rectangle
            var rObj = el.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty, true);
            if (rObj is Rect r && r.Width > 0 && r.Height > 0)
            {
               rect = r;
               return true;
            }

            // Invalid rectangle
            return false;
         }
         catch
         {
            // Error reading rectangle property
            return false;
         }
      }

      /// <summary>
      ///    Finds a pane by name within the Visual Studio main window.
      /// </summary>
      /// <param name="vsMainHwnd">Handle to the Visual Studio main window.</param>
      /// <param name="paneName">Name of the pane to locate.</param>
      /// <param name="pane">Output automation element for the pane.</param>
      /// <returns>True if pane was found, false otherwise.</returns>
      private static bool TryGetPane(IntPtr vsMainHwnd, string paneName, out AutomationElement pane)
      {
         ThreadHelper.ThrowIfNotOnUIThread();
         pane = null;

         // Validate inputs
         if (vsMainHwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(paneName))
         {
            return false;
         }

         // Get automation element for main window
         AutomationElement root;
         try
         {
            root = AutomationElement.FromHandle(vsMainHwnd);
         }
         catch
         {
            return false;
         }

         if (root == null)
         {
            return false;
         }

         // Build condition to find pane by name
         var paneCond = new AndCondition(
         new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
         new PropertyCondition(AutomationElement.NameProperty, paneName));

         try
         {
            // Create cache request for performance
            var cr = new CacheRequest();
            {
               cr.Add(AutomationElement.BoundingRectangleProperty);
               cr.Add(AutomationElement.IsOffscreenProperty);
               cr.Add(AutomationElement.NameProperty);
               cr.Add(AutomationElement.ClassNameProperty);
               cr.TreeScope = TreeScope.Subtree;

               // Search with caching active
               using (cr.Activate())
               {
                  pane = SafeFindFirst(root, TreeScope.Subtree, paneCond);
                  return pane != null;
               }
            }
         }
         catch
         {
            // Any error during search
            pane = null;
            return false;
         }
      }
   }
}