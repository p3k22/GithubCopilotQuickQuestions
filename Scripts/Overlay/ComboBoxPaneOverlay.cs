namespace GithubCopilotQuickQuestions.Scripts.Overlay
{
   using GithubCopilotQuickQuestions.Scripts.POCO;
   using GithubCopilotQuickQuestions.Scripts.Services;
   using GithubCopilotQuickQuestions.Scripts.Utilities;

   using Microsoft.VisualStudio.Shell;
   using Microsoft.VisualStudio.Shell.Interop;

   using System;
   using System.Linq;
   using System.Threading;
   using System.Threading.Tasks;
   using System.Windows;
   using System.Windows.Automation;
   using System.Windows.Threading;

   using static GithubCopilotQuickQuestions.Scripts.Utilities.Win32NativeMethodsUtils;
   using static GithubCopilotQuickQuestions.Scripts.Utilities.GlobalConstants;

   /// <summary>
   ///    Manages an overlay ComboBox positioned over a Visual Studio tool pane, tracking pane visibility and repositioning
   ///    dynamically.
   /// </summary>
   public sealed class ComboBoxPaneOverlay(AsyncPackage package, string paneCaption, Size size, Thickness margin)
      : IDisposable
   {
      /// <summary>
      ///    Callback delegate for requesting reposition from subclassed parent window messages.
      /// </summary>
      private static Action _repositionRequest;

      /// <summary>
      ///    Timer tick interval for polling updates.
      /// </summary>
      private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(120);

      /// <summary>
      ///    Subclass procedure for parent window message handling.
      /// </summary>
      private static readonly SUBCLASSPROC ParentSubClassProc = ParentSubclassProc;

      /// <summary>
      ///    Unique identifier for the window subclass.
      /// </summary>
      private static readonly UIntPtr _SubclassId = (UIntPtr) 1;

      /// <summary>
      ///    The custom combo box control instance.
      /// </summary>
      private CustomComboBox _comboBox;

      /// <summary>
      ///    Handle to the combo box window.
      /// </summary>
      private IntPtr _comboHwnd;

      /// <summary>
      ///    Flag indicating whether this instance has been disposed.
      /// </summary>
      private bool _disposed;

      /// <summary>
      ///    Flag to prevent reentrant updates during host recreation.
      /// </summary>
      private bool _isRecreating;

      /// <summary>
      ///    Last positioned rectangle for the combo box.
      /// </summary>
      private Rect _lastRect;

      /// <summary>
      ///    Margin around the combo box positioning.
      /// </summary>
      private readonly Thickness _margin = margin;

      /// <summary>
      ///    Reference to the VS async package.
      /// </summary>
      private readonly AsyncPackage _package = package ?? throw new ArgumentNullException(nameof(package));

      /// <summary>
      ///    Flag tracking whether the target pane was open in the last tick.
      /// </summary>
      private bool _paneWasOpen;

      /// <summary>
      ///    Handle to the parent window for the combo box.
      /// </summary>
      private IntPtr _parentHwnd;

      /// <summary>
      ///    Preferred size for the combo box control.
      /// </summary>
      private readonly Size _prefSize = new(size.Width, size.Height);

      /// <summary>
      ///    Handle to the currently subclassed parent window.
      /// </summary>
      private IntPtr _subclassedParent;

      /// <summary>
      ///    Timer for periodic update polling.
      /// </summary>
      private DispatcherTimer _timer;

      /// <summary>
      ///    UI Automation pane name to locate the target tool window.
      /// </summary>
      private readonly string _uiaPaneName = paneCaption ?? throw new ArgumentNullException(nameof(paneCaption));

      /// <summary>
      ///    Handle to the Visual Studio main window.
      /// </summary>
      private IntPtr _vsMainHwnd;

      /// <summary>
      ///    Disposes of the overlay and stops all monitoring.
      /// </summary>
      public void Dispose()
      {
         // Already disposed
         if (_disposed)
         {
            return;
         }

         _disposed = true;

         // Switch to main thread and stop
         ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
               await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
               Stop();
            });
      }

      /// <summary>
      ///    Starts the overlay monitoring and positioning logic.
      /// </summary>
      /// <param name="token">Cancellation token.</param>
      public async Task StartAsync(CancellationToken token)
      {
         await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

         // Setup reposition callback for subclass notifications
         _repositionRequest = () =>
            {
               ThreadHelper.ThrowIfNotOnUIThread();
               Reposition();
            };

         // Start polling timer
         _timer = new DispatcherTimer(DispatcherPriority.Background) {Interval = Tick};
         _timer.Tick += (_, __) => TickUpdate();
         _timer.Start();
      }

      /// <summary>
      ///    Stops the overlay monitoring and cleans up resources.
      /// </summary>
      public void Stop()
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         // Stop and clear timer
         _timer?.Stop();
         _timer = null;

         // Remove window subclass if active
         if (_subclassedParent != IntPtr.Zero && IsWindow(_subclassedParent))
         {
            RemoveWindowSubclass(_subclassedParent, ParentSubClassProc, _SubclassId);
            _subclassedParent = IntPtr.Zero;
         }

         _repositionRequest = null;

         // Clean up host
         DestroyHost();
         _parentHwnd = IntPtr.Zero;
         _vsMainHwnd = IntPtr.Zero;
         _paneWasOpen = false;
      }

      /// <summary>
      ///    Ensures a window is properly parented as a child with safe styles.
      /// </summary>
      /// <param name="hwnd">Handle to the child window.</param>
      /// <param name="parent">Handle to the parent window.</param>
      /// <returns>True if parenting succeeded, false otherwise.</returns>
      private static bool EnsureChildParenting(IntPtr hwnd, IntPtr parent)
      {
         // Validate handles
         if (hwnd == IntPtr.Zero || parent == IntPtr.Zero)
         {
            return false;
         }

         // Set parent relationship
         SetParent(hwnd, parent);

         // Get current styles
         var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
         var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

         // Add child styles, remove popup
         var newStyle = (style | WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN) & ~WS_POPUP;
         if (newStyle != style)
         {
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(newStyle));
         }

         // Remove topmost and window frame styles
         var newEx = ex & ~(WS_EX_TOPMOST | WS_EX_APPWINDOW | WS_EX_TOOLWINDOW);
         if (newEx != ex)
         {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newEx));
         }

         // Apply style changes
         SetWindowPos(
         hwnd,
         HWND_NOTOPMOST,
         0,
         0,
         0,
         0,
         SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER | SWP_FRAMECHANGED);

         return IsProperChild(hwnd, parent);
      }

      /// <summary>
      ///    Walks down the window hierarchy to find the deepest visible child at a screen point.
      /// </summary>
      /// <param name="screenX">Screen X coordinate.</param>
      /// <param name="screenY">Screen Y coordinate.</param>
      /// <returns>Handle to the deepest child window, or IntPtr.Zero if none.</returns>
      private static IntPtr GetDeepestChildAtPoint(int screenX, int screenY)
      {
         var pt = new POINT {X = screenX, Y = screenY};
         var h = WindowFromPoint(pt);
         if (h == IntPtr.Zero)
         {
            return IntPtr.Zero;
         }

         // Walk down child hierarchy
         var last = IntPtr.Zero;
         while (h != IntPtr.Zero && h != last)
         {
            last = h;
            var p = pt;

            // Convert to client coordinates
            ScreenToClient(h, ref p);

            // Find child at point, skipping invisible/disabled
            var child = ChildWindowFromPointEx(h, p, CWP_SKIPINVISIBLE | CWP_SKIPDISABLED | CWP_SKIPSIBLINGS);
            if (child == IntPtr.Zero || child == h)
            {
               break;
            }

            h = child;
         }

         return h;
      }

      /// <summary>
      ///    Checks if a combo box has its dropdown list open.
      /// </summary>
      /// <param name="combo">Handle to the combo box.</param>
      /// <returns>True if dropdown is open, false otherwise.</returns>
      private static bool IsComboDropped(IntPtr combo)
      {
         if (combo == IntPtr.Zero)
         {
            return false;
         }

         return SendMessageW(combo, CB_GETDROPPEDSTATE, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero;
      }

      /// <summary>
      ///    Window subclass procedure for the parent window to detect size and position changes.
      /// </summary>
      /// <param name="hWnd">Window handle.</param>
      /// <param name="msg">Message identifier.</param>
      /// <param name="wParam">Message parameter.</param>
      /// <param name="lParam">Message parameter.</param>
      /// <returns>Message result.</returns>
      private static IntPtr ParentSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
      {
         const uint WM_SIZE = 0x0005;
         const uint WM_WINDOWPOSCHANGED = 0x0047;

         // Trigger reposition on size or position changes
         if (msg == WM_SIZE || msg == WM_WINDOWPOSCHANGED)
         {
            _repositionRequest?.Invoke();
         }

         return DefSubclassProc(hWnd, msg, wParam, lParam);
      }

      /// <summary>
      ///    Creates the combo box host and initializes it.
      /// </summary>
      private void CreateHost()
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         // Validate parent window
         if (_parentHwnd == IntPtr.Zero || !IsWindow(_parentHwnd))
         {
            return;
         }

         // Already created
         if (_comboBox != null)
         {
            return;
         }

         // Create custom combo box
         var c = CustomComboBox.Create(_parentHwnd, 0, 0, (int) _prefSize.Width, (int) _prefSize.Height);
         if (c == null)
         {
            Logger.Log("[ComboBoxPaneOverlay] CustomComboBox.Create failed");
            return;
         }

         _comboBox = c;

         // Set default selection
         _comboBox.SetSelectedIndex(0);

         // Hook selection change event
         _comboBox.SelectionChanged += OnComboBox_OnSelectionChanged;

         _comboHwnd = _comboBox.Handle;
         Logger.Log($"[ComboBoxPaneOverlay] Host created combobox hwnd=0x{_comboHwnd.ToInt64():X}");

         // Apply size and position
         _comboBox.Resize((int) _prefSize.Width, (int) _prefSize.Height);
         Reposition();
      }

      /// <summary>
      ///    Destroys the combo box host and cleans up resources.
      /// </summary>
      private void DestroyHost()
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         // Destroy combo box if exists
         if (_comboBox != null)
         {
            try
            {
               _comboBox.Destroy();
            }
            catch
            {
               // Suppress any destruction errors
            }

            _comboBox = null;
         }

         // Clear state
         _comboHwnd = IntPtr.Zero;
         _lastRect = Rect.Empty;
      }

      /// <summary>
      ///    Ensures the layout parent window is subclassed for resize notifications.
      /// </summary>
      /// <param name="layoutParent">Handle to the layout parent window.</param>
      private void EnsureParentSubclassed(IntPtr layoutParent)
      {
         // Already subclassed
         if (_subclassedParent == layoutParent)
         {
            return;
         }

         // Remove old subclass if present
         if (_subclassedParent != IntPtr.Zero && IsWindow(_subclassedParent))
         {
            RemoveWindowSubclass(_subclassedParent, ParentSubClassProc, _SubclassId);
            _subclassedParent = IntPtr.Zero;
         }

         // Add new subclass
         if (layoutParent != IntPtr.Zero && IsWindow(layoutParent))
         {
            SetWindowSubclass(layoutParent, ParentSubClassProc, _SubclassId, UIntPtr.Zero);
            _subclassedParent = layoutParent;
         }
      }

      /// <summary>
      ///    Converts the pane's right edge from screen to parent-client coordinates.
      /// </summary>
      /// <returns>X coordinate of pane right edge in parent client space, or int.MinValue on failure.</returns>
      private int GetPaneRightInParentClient()
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         // Get pane rectangle in screen coordinates
         if (!UiaControlLocator.TryGetPaneRectScreen(_vsMainHwnd, _uiaPaneName, out var paneRect))
         {
            return int.MinValue;
         }

         // Calculate top-left and bottom-right points
         var tl = new POINT {X = (int) paneRect.X, Y = (int) paneRect.Y};
         var br = new POINT {X = (int) (paneRect.X + paneRect.Width), Y = (int) (paneRect.Y + paneRect.Height)};

         // Validate parent window
         if (_parentHwnd == IntPtr.Zero || !IsWindow(_parentHwnd))
         {
            return int.MinValue;
         }

         // Convert to parent client coordinates
         ScreenToClient(_parentHwnd, ref tl);
         ScreenToClient(_parentHwnd, ref br);

         // Return rightmost X coordinate
         return Math.Max(tl.X, br.X);
      }

      /// <summary>
      ///    Hides the combo box host window.
      /// </summary>
      private void HideHost()
      {
         // Hide window if valid
         if (_comboHwnd != IntPtr.Zero && IsWindow(_comboHwnd))
         {
            SetWindowPos(
            _comboHwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW);
         }
      }

      /// <summary>
      ///    Checks if Visual Studio is the foreground application.
      /// </summary>
      /// <returns>True if VS is foreground and not minimized, false otherwise.</returns>
      private bool IsVsForeground()
      {
         var fg = GetForegroundWindow();
         if (fg == IntPtr.Zero)
         {
            return false;
         }

         // Get root owners for comparison
         var fgRoot = GetAncestor(fg, GA_ROOTOWNER);
         var vsRoot = GetAncestor(_vsMainHwnd, GA_ROOTOWNER);

         // Check if foreground is VS main window
         if (fgRoot == vsRoot)
         {
            return !IsIconic(vsRoot);
         }

         // Also check if parent window is foreground
         if (_parentHwnd != IntPtr.Zero)
         {
            var parentRoot = GetAncestor(_parentHwnd, GA_ROOTOWNER);
            if (fgRoot == parentRoot)
            {
               return !IsIconic(parentRoot);
            }
         }

         return false;
      }

      /// <summary>
      ///    Handles combo box selection change events.
      /// </summary>
      /// <param name="i">Selected index.</param>
      /// <param name="t">Selected text.</param>
      private void OnComboBox_OnSelectionChanged(int i, string t)
      {
         // Fire and forget async handler
         _ = OnComboBox_OnSelectionChangedAsync(i, t);
      }

      /// <summary>
      ///    Asynchronously handles combo box selection changes by injecting text to Copilot.
      /// </summary>
      /// <param name="i">Selected index.</param>
      /// <param name="t">Selected text.</param>
      private async Task OnComboBox_OnSelectionChangedAsync(int i, string t)
      {
         // Index 0 is placeholder, ignore
         if (i == 0)
         {
            return;
         }

         // Get the selected question
         var selectedQuestion = ConfigReader.GetSelectedQuestion(i, t);

         // Brief delay before injecting
         await Task.Delay(300);

         // Inject text to Copilot
         CopilotInjector.PasteTextToCopilot(selectedQuestion);

         // Reset to placeholder
         _comboBox.SetSelectedIndex(0);
      }

      /// <summary>
      ///    Computes the target position and size for the combo box and applies it.
      /// </summary>
      private void Reposition()
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         // Validate handles
         if (_comboHwnd == IntPtr.Zero || _parentHwnd == IntPtr.Zero)
         {
            return;
         }

         if (!IsWindow(_comboHwnd) || !IsWindow(_parentHwnd))
         {
            return;
         }

         // Try to find the anchor control (models combobox)
         Rect anchorRectScreen;
         var haveControl = UiaControlLocator.TryGetControlRectScreen(
         _vsMainHwnd,
         _uiaPaneName,
         ControlType.ComboBox,
         GITHUB_COPILOT_MODELS_COMBOBOX_ID,
         GITHUB_COPILOT_MODELS_COMBOBOX_NAME,
         out anchorRectScreen);

         // Fall back to pane position if control not found
         if (!haveControl)
         {
            if (!UiaControlLocator.TryGetPaneRectScreen(_vsMainHwnd, _uiaPaneName, out anchorRectScreen))
            {
               return;
            }

            // Position at bottom of pane
            anchorRectScreen = new Rect(
            anchorRectScreen.X,
            (anchorRectScreen.Y + anchorRectScreen.Height) - _prefSize.Height,
            _prefSize.Width,
            _prefSize.Height);
         }

         // Convert anchor to parent client coordinates
         var tl = new POINT {X = (int) anchorRectScreen.X, Y = (int) anchorRectScreen.Y};
         ScreenToClient(_parentHwnd, ref tl);

         var desiredW = (int) _prefSize.Width;
         var desiredH = (int) _prefSize.Height;

         // Apply offset from anchor
         var ox = 90;
         var oy = -15;
         var targetX = tl.X + (int) _margin.Left + ox;
         var targetY = tl.Y + (int) _margin.Top + oy;

         // Verify pane is still visible
         if (!UiaControlLocator.TryGetPaneRectScreen(_vsMainHwnd, _uiaPaneName, out _))
         {
            return;
         }

         const int RightPadding = 90;
         const int MinWidth = 30;

         // Calculate maximum allowed width
         var paneRightClient = GetPaneRightInParentClient();
         if (paneRightClient == int.MinValue)
         {
            Logger.Log("[ComboBoxPaneOverlay] Hiding - paneRightClient is MinValue");
            HideHost();
            return;
         }

         Logger.Log(
         $"[ComboBoxPaneOverlay] Reposition: targetX={targetX}, targetY={targetY}, desiredW={desiredW}, desiredH={desiredH}, paneRightClient={paneRightClient}");

         var maxAllowedWidth = paneRightClient - RightPadding - targetX;

         // Hide if not enough space
         if (maxAllowedWidth < MinWidth)
         {
            Logger.Log($"[ComboBoxPaneOverlay] Hiding - maxAllowedWidth={maxAllowedWidth} < MinWidth={MinWidth}");
            HideHost();
            return;
         }

         // Clamp width to available space
         desiredW = Math.Max(MinWidth, Math.Min(desiredW, maxAllowedWidth));

         // Zero size if anchor control not found
         if (!haveControl)
         {
            desiredW = desiredH = 0;
         }

         // Apply new size
         _comboBox?.Resize(desiredW, desiredH);

         // Position and show the window
         var flags = SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSENDCHANGING | SWP_SHOWWINDOW | SWP_NOSIZE;
         SetWindowPos(_comboHwnd, IntPtr.Zero, targetX, targetY, 0, 0, flags);

         // Log visibility status
         var isVisible = IsWindowVisible(_comboHwnd);
         var style = GetWindowLongPtr(_comboHwnd, GWL_STYLE).ToInt64();
         Logger.Log(
         $"[ComboBoxPaneOverlay] After SetWindowPos: IsWindowVisible={isVisible}, WS_VISIBLE={(style & 0x10000000) != 0}");

         _lastRect = new Rect(targetX, targetY, desiredW, desiredH);
      }

      /// <summary>
      ///    Main polling loop that monitors pane state and updates overlay positioning.
      /// </summary>
      private void TickUpdate()
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         // Skip if currently recreating
         if (_isRecreating)
         {
            return;
         }

         // Check if mouse buttons are down or captured
         var mouseDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 || (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
         var hasCapture = GetCapture() != IntPtr.Zero;

         // Skip repositioning during mouse interaction or dropdown
         if (mouseDown || hasCapture || IsComboDropped(_comboHwnd))
         {
            // Just ensure visibility if VS is foreground
            if (_comboHwnd != IntPtr.Zero && IsVsForeground())
            {
               SetWindowPos(
               _comboHwnd,
               IntPtr.Zero,
               0,
               0,
               0,
               0,
               SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            return;
         }

         // Get VS main window
         if (!TryGetVsMain(out var vsMain))
         {
            HideHost();
            DestroyHost();
            _parentHwnd = IntPtr.Zero;
            _paneWasOpen = false;
            return;
         }

         _vsMainHwnd = vsMain;

         // Check if pane is open
         var paneOpenNow = UiaControlLocator.TryGetPaneRectScreen(vsMain, _uiaPaneName, out var paneRectScreen);
         if (!paneOpenNow)
         {
            HideHost();
            DestroyHost();
            _parentHwnd = IntPtr.Zero;
            _paneWasOpen = false;
            return;
         }

         // Detect pane reopening and force recreation
         if (!_paneWasOpen && paneOpenNow)
         {
            _isRecreating = true;
            try
            {
               // Clean up old subclass
               if (_subclassedParent != IntPtr.Zero && IsWindow(_subclassedParent))
               {
                  RemoveWindowSubclass(_subclassedParent, ParentSubClassProc, _SubclassId);
                  _subclassedParent = IntPtr.Zero;
               }

               // Reset parent and destroy host
               _parentHwnd = IntPtr.Zero;
               DestroyHost();
            }
            finally
            {
               _isRecreating = false;
            }
         }

         _paneWasOpen = true;

         // Hide if VS not foreground
         if (!IsVsForeground())
         {
            HideHost();
            return;
         }

         // Find window at pane center
         var cx = (int) (paneRectScreen.X + (paneRectScreen.Width / 2));
         var cy = (int) (paneRectScreen.Y + (paneRectScreen.Height / 2));
         var hwndAtPoint = WindowFromPoint(new POINT {X = cx, Y = cy});
         if (hwndAtPoint == IntPtr.Zero || !IsWindow(hwndAtPoint))
         {
            HideHost();
            DestroyHost();
            _parentHwnd = IntPtr.Zero;
            return;
         }

         // Get deepest child for layout parent
         var deepest = GetDeepestChildAtPoint(cx, cy);
         if (deepest == IntPtr.Zero)
         {
            deepest = hwndAtPoint;
         }

         var layoutParent = deepest;
         EnsureParentSubclassed(layoutParent);

         // Recreate if parent changed
         if (layoutParent != _parentHwnd || _comboHwnd == IntPtr.Zero)
         {
            _isRecreating = true;
            try
            {
               Logger.Log(
               $"[ComboBoxPaneOverlay] Parent change -> recreate. old=0x{_parentHwnd.ToInt64():X} new=0x{layoutParent.ToInt64():X}");
               _parentHwnd = layoutParent;
               DestroyHost();
               CreateHost();
            }
            finally
            {
               _isRecreating = false;
            }
         }

         // Validate combo window still exists
         if (_comboHwnd == IntPtr.Zero || !IsWindow(_comboHwnd))
         {
            return;
         }

         // Ensure proper child parenting, recreate if needed
         if (!EnsureChildParenting(_comboHwnd, _parentHwnd))
         {
            HideHost();
            _isRecreating = true;
            try
            {
               DestroyHost();
               CreateHost();
            }
            finally
            {
               _isRecreating = false;
            }

            if (_comboHwnd == IntPtr.Zero)
            {
               return;
            }
         }

         // Update position
         Reposition();
      }

      /// <summary>
      ///    Attempts to get the Visual Studio main window handle.
      /// </summary>
      /// <param name="vsMain">Output parameter for VS main window handle.</param>
      /// <returns>True if handle was obtained, false otherwise.</returns>
      private bool TryGetVsMain(out IntPtr vsMain)
      {
         vsMain = IntPtr.Zero;

         // Get VS shell service
         var shellObj = _package.GetServiceAsync(typeof(SVsUIShell)).GetAwaiter().GetResult();
         if (shellObj is not IVsUIShell shell)
         {
            return false;
         }

         // Get main window handle
         shell.GetDialogOwnerHwnd(out vsMain);
         return vsMain != IntPtr.Zero;
      }
   }
}