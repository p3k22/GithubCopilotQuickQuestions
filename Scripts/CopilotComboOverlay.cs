namespace GithubCopilotQuickQuestions.Scripts
{
   using Microsoft.Win32;

   using System;
   using System.Collections.Generic;
   using System.Drawing;
   using System.Linq;
   using System.Runtime.InteropServices;
   using System.Windows.Automation;
   using System.Windows.Forms;
   using System.Windows.Threading;

   using VSOnEventAction.Scripts;

   public static class CopilotOverlay
   {
      private const int GWL_EXSTYLE = -20;

      private const int WS_EX_TOOLWINDOW = 0x00000080;

      private const int CB_GETDROPPEDCONTROLRECT = 0x0152;

      private const int CB_SETDROPPEDWIDTH = 0x0160;

      private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

      // Win32 constants
      private const uint SWP_NOSIZE = 0x0001,
                         SWP_NOMOVE = 0x0002,
                         SWP_NOZORDER = 0x0004,
                         SWP_NOACTIVATE = 0x0010,
                         SWP_SHOWWINDOW = 0x0040;

      private static AutomationElement _cachedPanel;

      private static ComboBox _combo = null;

      // State
      private static Form _form = null;

      private static bool _isOpen;

      private static bool _isOwnerMaximized = false;

      private static List<string> _items = new List<string>();

      private static long _itemsVersion = 0, _appliedVersion = -1;

      private static RECT _lastComboRect;

      private static DateTime _lastPanelSearch = DateTime.MinValue;

      private static DateTime _lastZOrderUpdate = DateTime.MinValue;

      private static int _offsetX = 0, _offsetY = 0;

      private static IntPtr _ownerWindow = IntPtr.Zero;

      private static int _pad = 8, _width = 200, _height = 32;

      private static RECT _panelRect;

      private static DispatcherTimer _timer;

      private static bool _useDarkMode = false;

      private static IntPtr _vsMainWindow = IntPtr.Zero;

      private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

      private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

      public static bool IsOpen { get; private set; }

      public static bool Start(
         IEnumerable<string> items,
         int width = 200,
         int height = 32,
         int pad = 8,
         int offsetX = 0,
         int offsetY = 0,
         bool darkMode = false,
         int intervalMs = 50)
      {
         Stop();

         IsOpen = true;

         _width = width;
         _height = height;
         _pad = pad;
         _offsetX = offsetX;
         _offsetY = offsetY;
         _useDarkMode = darkMode;

         _ownerWindow = GetForegroundWindow();
         _vsMainWindow = _ownerWindow;
         if (_ownerWindow == IntPtr.Zero)
         {
            return false;
         }

         _items = new List<string>(ConfigReader.GetSelections().Keys.ToList());

         CreateComboBox();
         if (_combo == null)
         {
            return false;
         }

         _timer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(intervalMs)};
         _timer.Tick += OnTick;
         _timer.Start();

         return true;
      }

      public static void SetItems(IEnumerable<string> items)
      {
         _items.Clear();
         if (items != null)
         {
            _items.AddRange(items);
         }

         _itemsVersion++;
         UpdateItems();
      }

      public static void Stop()
      {
         if (_timer != null)
         {
            _timer.Stop();
            _timer = null;
         }

         if (_combo != null)
         {
            _combo.Dispose();
            _combo = null;
         }

         if (_form != null)
         {
            _form.Close();
            _form.Dispose();
            _form = null;
         }

         _ownerWindow = IntPtr.Zero;
         _vsMainWindow = IntPtr.Zero;
         _appliedVersion = -1;
         IsOpen = false;
      }

      private static void ApplyDarkMode(IntPtr hwnd)
      {
         if (!_useDarkMode)
         {
            return;
         }

         try
         {
            // Detect if Windows is in dark mode
            var isDarkMode = false;
            using (var key = Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
               if (key?.GetValue("AppsUseLightTheme") != null)
               {
                  var value = (int) key.GetValue("AppsUseLightTheme");
                  isDarkMode = value == 0;
               }
            }

            // Apply dark title bar if in dark mode
            if (isDarkMode && Environment.OSVersion.Version.Major >= 10)
            {
               var darkModeValue = 1;
               DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));
            }
         }
         catch
         {
            // Ignore if dark mode detection fails
         }
      }

      private static void CreateComboBox()
      {
         if (_ownerWindow == IntPtr.Zero)
         {
            return;
         }

         if (!FindCopilotPanel())
         {
            return;
         }

         // Position relative to panel - bottom with padding + offsets
         var x = _panelRect.Left + _pad + _offsetX;
         var y = (_panelRect.Bottom - _height - _pad) + _offsetY;

         // Create a borderless form to host the combo
         _form = new Form
                    {
                       FormBorderStyle = FormBorderStyle.None,
                       StartPosition = FormStartPosition.Manual,
                       Location = new Point(x, y),
                       Size = new Size(_width, _height),
                       TopMost = false,
                       ShowInTaskbar = false,
                       AllowTransparency = true,
                       BackColor = Color.Lime, // Use a color we'll make transparent
                       TransparencyKey = Color.Lime // Make this color fully transparent
                    };

         // Create the combo box
         _combo = new CustomComboBox
                     {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Location = new Point(0, 0),
                        Size = new Size(_width, _height),
                        FlatStyle = FlatStyle.Flat
                     };

         // Hook dropdown event to make it open upward
         _combo.DropDown += OnComboDropDown;
         Logger.Log("Initial Index; " + _combo.SelectedIndex);
         _combo.SelectionChangeCommitted += ConfigReader.SelectQuestion;

         // Apply dark mode colors if requested
         if (_useDarkMode)
         {
            _combo.BackColor = Color.FromArgb(60, 60, 60);
            _combo.ForeColor = Color.White;
         }

         _form.Controls.Add(_combo);
         _form.Show();

         var exStyle = GetWindowLong(_form.Handle, GWL_EXSTYLE);
         SetWindowLong(_form.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

         // Apply dark title bar to form
         if (_useDarkMode)
         {
            ApplyDarkMode(_form.Handle);
         }

         UpdateItems();
         UpdateZOrder();

         // Track initial position
         _lastComboRect = new RECT {Left = x, Top = y, Right = x + _width, Bottom = y + _height};
      }

      private static bool FindCopilotPanel()
      {
         _lastPanelSearch = DateTime.UtcNow;

         // Try cached panel first
         if (_cachedPanel != null)
         {
            try
            {
               var rect = _cachedPanel.Current.BoundingRectangle;
               if (!_cachedPanel.Current.IsOffscreen && rect.Width > 0 && rect.Height > 0)
               {
                  _panelRect = new RECT
                                  {
                                     Left = (int) rect.Left,
                                     Top = (int) rect.Top,
                                     Right = (int) rect.Right,
                                     Bottom = (int) rect.Bottom
                                  };

                  // Update owner window
                  try
                  {
                     var panelHwnd = new IntPtr(_cachedPanel.Current.NativeWindowHandle);
                     if (panelHwnd != IntPtr.Zero)
                     {
                        _ownerWindow = panelHwnd;
                     }
                  }
                  catch
                  {
                  }

                  return true;
               }
            }
            catch
            {
               _cachedPanel = null;
            }
         }

         // Check if foreground window IS the copilot window (undocked case)
         var foreground = GetForegroundWindow();
         if (foreground != IntPtr.Zero)
         {
            var title = new System.Text.StringBuilder(256);
            GetWindowText(foreground, title, 256);
            var windowTitle = title.ToString();

            // If window title contains "Copilot", it's the undocked panel window
            if (windowTitle.Contains("Copilot"))
            {
               RECT wndRect;
               if (GetWindowRect(foreground, out wndRect))
               {
                  _panelRect = wndRect;
                  _ownerWindow = foreground;

                  // Try to cache the automation element
                  try
                  {
                     _cachedPanel = AutomationElement.FromHandle(foreground);
                  }
                  catch
                  {
                  }

                  return true;
               }
            }
         }

         // Search in main VS window for docked panel
         try
         {
            var root = AutomationElement.FromHandle(_vsMainWindow);
            if (root != null)
            {
               var panel = root.FindFirst(
               TreeScope.Descendants,
               new AndCondition(
               new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
               new PropertyCondition(AutomationElement.NameProperty, "GitHub Copilot Chat")));

               if (panel == null)
               {
                  panel = root.FindFirst(
                  TreeScope.Descendants,
                  new OrCondition(
                  new PropertyCondition(AutomationElement.AutomationIdProperty, "CopilotCommentsPane"),
                  new PropertyCondition(AutomationElement.AutomationIdProperty, "CopilotPrompt")));
               }

               if (panel != null)
               {
                  var rect = panel.Current.BoundingRectangle;
                  if (!panel.Current.IsOffscreen && rect.Width >= 2 && rect.Height >= 2)
                  {
                     _panelRect = new RECT
                                     {
                                        Left = (int) rect.Left,
                                        Top = (int) rect.Top,
                                        Right = (int) rect.Right,
                                        Bottom = (int) rect.Bottom
                                     };

                     try
                     {
                        var panelHwnd = new IntPtr(panel.Current.NativeWindowHandle);
                        if (panelHwnd != IntPtr.Zero)
                        {
                           _ownerWindow = panelHwnd;
                        }
                     }
                     catch
                     {
                     }

                     _cachedPanel = panel;
                     return true;
                  }
               }
            }
         }
         catch
         {
         }

         return false;
      }

      private static void OnComboDropDown(object sender, EventArgs e)
      {
         if (_combo == null || _form == null)
         {
            return;
         }

         _combo.Items.Clear();
         _combo.Items.AddRange(ConfigReader.GetSelections().Keys.Cast<object>().ToArray());
         _combo.Refresh();

         // Use BeginInvoke to reposition after dropdown is created
         _form.BeginInvoke(
         new Action(() =>
            {
               try
               {
                  // Find the dropdown listbox window (it's a popup window of class "ComboLBox")
                  var listBoxHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "ComboLBox", null);

                  if (listBoxHandle != IntPtr.Zero)
                  {
                     // Get dropdown size
                     RECT dropRect;
                     GetWindowRect(listBoxHandle, out dropRect);
                     var dropWidth = dropRect.Right - dropRect.Left;
                     var dropHeight = dropRect.Bottom - dropRect.Top;

                     // Get combo position
                     RECT comboRect;
                     GetWindowRect(_combo.Handle, out comboRect);

                     // Position dropdown ABOVE the combo instead of below
                     var newX = comboRect.Left;
                     var newY = comboRect.Top - dropHeight;

                     // Move the dropdown
                     SetWindowPos(
                     listBoxHandle,
                     IntPtr.Zero,
                     newX,
                     newY,
                     dropWidth,
                     dropHeight,
                     SWP_NOZORDER | SWP_NOACTIVATE);
                  }
               }
               catch
               {
                  // Ignore errors
               }
            }));
      }

      private static void OnTick(object sender, EventArgs e)
      {
         if (_combo == null || _form == null || _vsMainWindow == IntPtr.Zero)
         {
            return;
         }

         // Hide combo if VS main window is minimized
         if (IsIconic(_vsMainWindow))
         {
            if (_form.Visible)
            {
               _form.Hide();
            }

            return;
         }

         // Show combo if it was hidden
         if (!_form.Visible)
         {
            _form.Show();
         }

         // Check if combo is currently active (dropdown might be open)
         var foreground = GetForegroundWindow();
         if (foreground == _form.Handle)
         {
            // Don't update anything while user is interacting with combo
            return;
         }

         // Update panel position every 200ms
         if ((DateTime.UtcNow - _lastPanelSearch).TotalMilliseconds > 200)
         {
            FindCopilotPanel();
         }

         // Check if VS main window is the foreground window (not the undocked panel)
         var isVSActive = foreground == _vsMainWindow || foreground == _ownerWindow;

         // Check if VS MAIN window maximized state changed (not undocked panel)
         var isMaximized = IsZoomed(_vsMainWindow);
         if (isMaximized != _isOwnerMaximized)
         {
            _isOwnerMaximized = isMaximized;
            if (isVSActive)
            {
               UpdateZOrder();
               _lastZOrderUpdate = DateTime.UtcNow;
            }
         }
         else if (isVSActive && (DateTime.UtcNow - _lastZOrderUpdate).TotalMilliseconds > 10)
         {
            // Refresh z-order periodically to keep combo on top (only when VS is active)
            UpdateZOrder();
            _lastZOrderUpdate = DateTime.UtcNow;
         }

         // Update position to follow panel - only if position actually changed
         if (_panelRect.Width > 0 && _panelRect.Height > 0)
         {
            var x = _panelRect.Left + _pad + _offsetX;
            var y = (_panelRect.Bottom - _height - _pad) + _offsetY;

            // Check if position changed
            var newRect = new RECT {Left = x, Top = y, Right = x + _width, Bottom = y + _height};
            if (_lastComboRect.Left != newRect.Left || _lastComboRect.Top != newRect.Top
                || _lastComboRect.Right != newRect.Right || _lastComboRect.Bottom != newRect.Bottom)
            {
               // Position changed - update it
               _form.Location = new Point(x, y);
               _form.Size = new Size(_width, _height);
               _lastComboRect = newRect;
               UpdateZOrder();
            }
         }
      }

      private static void UpdateItems()
      {
         if (_combo == null || _appliedVersion == _itemsVersion)
         {
            return;
         }

         _combo.Items.Clear();

         foreach (var item in _items)
         {
            // Skip null or empty items
            if (string.IsNullOrEmpty(item))
            {
               continue;
            }

            _combo.Items.Add(item);
         }

         if (_items.Count > 0)
         {
            _combo.SelectedIndex = 0;
         }

         _appliedVersion = _itemsVersion;
      }

      private static void UpdateZOrder()
      {
         if (_form == null)
         {
            return;
         }

         // Always do the z-order boost to keep combo above the copilot panel
         SetWindowPos(_form.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
         SetWindowPos(
         _form.Handle,
         HWND_NOTOPMOST,
         0,
         0,
         0,
         0,
         SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
      }

      private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

      [StructLayout(LayoutKind.Sequential)]
      private struct RECT
      {
         public int Left, Top, Right, Bottom;

         public int Height => Bottom - Top;

         public int Width => Right - Left;
      }

   #region Externs

      [DllImport("user32.dll")]
      private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

      [DllImport("user32.dll")]
      private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

      [DllImport("dwmapi.dll")]
      private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

      [DllImport("user32.dll")]
      private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

      [DllImport("user32.dll")]
      private static extern IntPtr FindWindowEx(
         IntPtr hwndParent,
         IntPtr hwndChildAfter,
         string lpszClass,
         string lpszWindow);

      [DllImport("user32.dll")]
      private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

      [DllImport("user32.dll")]
      private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

      [DllImport("user32.dll")]
      private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

      // Win32 imports
      [DllImport("user32.dll")]
      private static extern IntPtr GetForegroundWindow();

      [DllImport("user32.dll")]
      private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

      [DllImport("user32.dll", CharSet = CharSet.Unicode)]
      private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

      [DllImport("user32.dll")]
      private static extern bool IsIconic(IntPtr hWnd);

      [DllImport("user32.dll")]
      private static extern bool IsZoomed(IntPtr hWnd);

   #endregion
   }
}