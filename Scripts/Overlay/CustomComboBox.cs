namespace GithubCopilotQuickQuestions.Scripts.Overlay
{
   using GithubCopilotQuickQuestions.Scripts.POCO;
   using GithubCopilotQuickQuestions.Scripts.Services;
   using GithubCopilotQuickQuestions.Scripts.Utilities;

   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Runtime.InteropServices;

   using static GithubCopilotQuickQuestions.Scripts.Utilities.Win32NativeMethodsUtils;
   using static GithubCopilotQuickQuestions.Scripts.Utilities.GlobalConstants;

   /// <summary>
   ///    Custom Win32 ComboBox control with upward-opening dropdown list and scrollable items.
   /// </summary>
   internal sealed class CustomComboBox : IDisposable
   {
      /// <summary>
      ///    Flag indicating whether window classes have been registered.
      /// </summary>
      private static bool _clsRegistered;

      /// <summary>
      ///    Window class name for the combo box face (button).
      /// </summary>
      private static readonly string FaceCls = "P3kComboFace";

      /// <summary>
      ///    Handle to the current module instance.
      /// </summary>
      private static readonly IntPtr HInstance = GetModuleHandle(null);

      /// <summary>
      ///    Maps window handles to their associated CustomComboBox instances.
      /// </summary>
      private static readonly Dictionary<IntPtr, CustomComboBox> Instances = new();

      /// <summary>
      ///    Window class name for the dropdown list.
      /// </summary>
      private static readonly string ListCls = "P3kComboList";

      /// <summary>
      ///    Window procedure delegate for the face window.
      /// </summary>
      private static readonly WNDCLASSEX.WndProc FaceProc = FaceWndProc;

      /// <summary>
      ///    Window procedure delegate for the list window.
      /// </summary>
      private static readonly WNDCLASSEX.WndProc ListProc = ListWndProc;

      /// <summary>
      ///    Handle to the font used for rendering text.
      /// </summary>
      private IntPtr _hFont;

      /// <summary>
      ///    Index of the currently hovered item in the list (-1 if none).
      /// </summary>
      private int _hoverIndex = -1;

      /// <summary>
      ///    Height of each item in pixels.
      /// </summary>
      private readonly int _itemHeight = 22;

      /// <summary>
      ///    List of items displayed in the combo box.
      /// </summary>
      private readonly List<string> _items = [];

      /// <summary>
      ///    Handle to the dropdown list window.
      /// </summary>
      private IntPtr _listHwnd;

      /// <summary>
      ///    Flag indicating whether the dropdown list is currently visible.
      /// </summary>
      private bool _listVisible;

      /// <summary>
      ///    Handle to the parent window.
      /// </summary>
      private readonly IntPtr _parent;

      /// <summary>
      ///    Placeholder text displayed when no item is selected.
      /// </summary>
      private readonly string _placeholder = "Select…";

      /// <summary>
      ///    Current scroll position of the list (top visible item index).
      /// </summary>
      private int _scrollPos;

      /// <summary>
      ///    Index of the currently selected item (-1 if none).
      /// </summary>
      private int _selectedIndex;

      /// <summary>
      ///    Number of rows visible in the dropdown list at once.
      /// </summary>
      private int _visibleRows;

      /// <summary>
      ///    Position (x, y) and dimensions (width, height) of the combo box face.
      /// </summary>
      private int _x, _y, _w, _h;

      /// <summary>
      ///    Gets the window handle of the combo box face.
      /// </summary>
      public IntPtr Handle { get; private set; }

      /// <summary>
      ///    Event raised when the selection changes, providing the index and text of the new selection.
      /// </summary>
      public event Action<int, string> SelectionChanged;

      /// <summary>
      ///    Initializes a new instance of the CustomComboBox.
      /// </summary>
      /// <param name="parent">Handle to the parent window.</param>
      private CustomComboBox(IntPtr parent)
      {
         _parent = parent;
      }

      /// <summary>
      ///    Creates and initializes a new CustomComboBox instance.
      /// </summary>
      /// <param name="parent">Handle to the parent window.</param>
      /// <param name="x">X-coordinate of the combo box.</param>
      /// <param name="y">Y-coordinate of the combo box.</param>
      /// <param name="w">Width of the combo box.</param>
      /// <param name="h">Height of the combo box.</param>
      /// <returns>A new CustomComboBox instance, or null if creation failed.</returns>
      public static CustomComboBox Create(IntPtr parent, int x, int y, int w, int h)
      {
         // Validate parent handle
         if (parent == IntPtr.Zero)
         {
            return null;
         }

         var c = new CustomComboBox(parent);

         // Initialize and clean up on failure
         if (!c.Initialize(x, y, w, h))
         {
            c.Dispose();
            return null;
         }

         return c;
      }

      /// <summary>
      ///    Destroys all windows and frees resources associated with this combo box.
      /// </summary>
      public void Destroy()
      {
         var face = Handle;
         var list = _listHwnd;

         // Hide dropdown first
         HideList();

         // Destroy face window
         if (face != IntPtr.Zero)
         {
            DestroyWindow(face);
         }

         // Destroy list window
         if (list != IntPtr.Zero)
         {
            DestroyWindow(list);
         }

         // Delete font object
         if (_hFont != IntPtr.Zero)
         {
            DeleteObject(_hFont);
            _hFont = IntPtr.Zero;
         }

         // Remove from instance tracking
         Instances.Remove(face);
         Instances.Remove(list);

         // Clear handles
         Handle = IntPtr.Zero;
         _listHwnd = IntPtr.Zero;
      }

      /// <summary>
      ///    Disposes of resources by calling Destroy.
      /// </summary>
      public void Dispose()
      {
         Destroy();
      }

      /// <summary>
      ///    Gets the index of the currently selected item.
      /// </summary>
      /// <returns>The selected index, or -1 if nothing is selected.</returns>
      public int GetSelectedIndex()
      {
         return _selectedIndex;
      }

      /// <summary>
      ///    Gets the text of the currently selected item.
      /// </summary>
      /// <returns>The selected item's text, or an empty string if nothing is selected.</returns>
      public string GetSelectedText()
      {
         return _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : string.Empty;
      }

      /// <summary>
      ///    Hides the combo box and its dropdown list.
      /// </summary>
      public void Hide()
      {
         HideList();
         ShowWindow(Handle, SW_HIDE);
      }

      /// <summary>
      ///    Moves the combo box to a new position.
      /// </summary>
      /// <param name="x">New X-coordinate.</param>
      /// <param name="y">New Y-coordinate.</param>
      public void Move(int x, int y)
      {
         _x = x;
         _y = y;

         // Move face window without resizing
         SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

         // Reposition list if visible
         if (_listVisible)
         {
            PositionList();
         }
      }

      /// <summary>
      ///    Resizes the combo box to new dimensions.
      /// </summary>
      /// <param name="w">New width.</param>
      /// <param name="h">New height.</param>
      public void Resize(int w, int h)
      {
         _w = w;
         _h = h;

         // Resize and repaint face
         MoveWindow(Handle, _x, _y, _w, _h, true);

         // Reposition list if visible
         if (_listVisible)
         {
            PositionList();
         }
      }

      /// <summary>
      ///    Sets the items to be displayed in the combo box.
      /// </summary>
      /// <param name="items">Collection of item strings.</param>
      public void SetItems(IReadOnlyList<string> items)
      {
         _items.Clear();

         // Add new items if provided
         if (items != null)
         {
            _items.AddRange(items);
         }

         // Adjust selected index if out of range
         if (_selectedIndex >= _items.Count)
         {
            _selectedIndex = _items.Count - 1;
         }

         // Reset scroll and update metrics
         _scrollPos = 0;
         UpdateScrollMetrics();

         // Repaint both windows
         InvalidateRect(Handle, IntPtr.Zero, true);
         InvalidateRect(_listHwnd, IntPtr.Zero, true);
      }

      /// <summary>
      ///    Sets the currently selected item by index.
      /// </summary>
      /// <param name="index">Index of the item to select.</param>
      public void SetSelectedIndex(int index)
      {
         // Validate index
         if (index < -1 || index >= _items.Count)
         {
            return;
         }

         _selectedIndex = index;

         // Repaint face
         InvalidateRect(Handle, IntPtr.Zero, true);

         // Notify subscribers
         SelectionChanged?.Invoke(_selectedIndex, GetSelectedText());
      }

      /// <summary>
      ///    Shows the combo box without activating it.
      /// </summary>
      public void Show()
      {
         ShowWindow(Handle, SW_SHOWNOACTIVATE);
      }

      /// <summary>
      ///    Creates a default font for the combo box.
      /// </summary>
      /// <returns>Handle to the created font.</returns>
      private static IntPtr CreateDefaultFont()
      {
         var lf = new LOGFONT {lfHeight = -14, lfWeight = 400, lfQuality = 5};
         return CreateFontIndirect(ref lf);
      }

      /// <summary>
      ///    Draws left-aligned text with ellipsis if truncated.
      /// </summary>
      /// <param name="hdc">Device context handle.</param>
      /// <param name="rc">Rectangle for text drawing.</param>
      /// <param name="text">Text to draw.</param>
      private static void DrawTextLeft(IntPtr hdc, RECT rc, string text)
      {
         // Set transparent background
         SetBkMode(hdc, 1);

         // Set text color
         SetTextColor(hdc, COLOR_TEXT);

         // Draw text with ellipsis truncation
         DrawTextW(hdc, text, text.Length, ref rc, DT_SINGLELINE | DT_VCENTER | DT_LEFT | DT_END_ELLIPSIS);
      }

      /// <summary>
      ///    Ensures that the window classes for face and list are registered.
      /// </summary>
      /// <returns>True if classes are registered successfully, false otherwise.</returns>
      private static bool EnsureWindowClasses()
      {
         // Already registered
         if (_clsRegistered)
         {
            return true;
         }

         // Register face class
         if (!TryRegister(FaceCls, FaceProc))
         {
            return false;
         }

         // Register list class
         if (!TryRegister(ListCls, ListProc))
         {
            return false;
         }

         _clsRegistered = true;
         return true;
      }

      /// <summary>
      ///    Window procedure for the combo box face (button).
      /// </summary>
      /// <param name="hwnd">Window handle.</param>
      /// <param name="msg">Message identifier.</param>
      /// <param name="wParam">Message parameter.</param>
      /// <param name="lParam">Message parameter.</param>
      /// <returns>Message result.</returns>
      private static IntPtr FaceWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
      {
         // Look up instance for this window
         if (!Instances.TryGetValue(hwnd, out var self))
         {
            return DefWindowProcW(hwnd, msg, wParam, lParam);
         }

         switch (msg)
         {
            case WM_PAINT:
               self.DrawFace(hwnd);
               return IntPtr.Zero;
            case WM_LBUTTONDOWN:
               // Set focus and bring to front
               SetFocus(hwnd);
               SetWindowPos(hwnd, (IntPtr) (-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
               self.ToggleList();
               return IntPtr.Zero;
            case WM_MOUSEWHEEL:
               self.MouseWheel(GET_WHEEL_DELTA_WPARAM(wParam));
               return IntPtr.Zero;
            case WM_KEYDOWN:
               self.OnKeyDown((int) wParam);
               return IntPtr.Zero;
            case WM_SETFONT:
               return IntPtr.Zero;
            case WM_DESTROY:
               self.Destroy();
               break;
         }

         return DefWindowProcW(hwnd, msg, wParam, lParam);
      }

      /// <summary>
      ///    Fills a rectangle with a solid color.
      /// </summary>
      /// <param name="hdc">Device context handle.</param>
      /// <param name="rc">Rectangle to fill.</param>
      /// <param name="rgb">Color as RGB value.</param>
      private static void FillSolidRect(IntPtr hdc, RECT rc, uint rgb)
      {
         var hBrush = CreateSolidBrush(rgb);
         FillRect(hdc, ref rc, hBrush);
         DeleteObject(hBrush);
      }

      /// <summary>
      ///    Extracts the wheel delta value from wParam.
      /// </summary>
      /// <param name="wParam">wParam containing wheel delta in high word.</param>
      /// <returns>Wheel delta as signed short.</returns>
      private static int GET_WHEEL_DELTA_WPARAM(IntPtr wParam)
      {
         return (short) (((long) wParam >> 16) & 0xFFFF);
      }

      /// <summary>
      ///    Extracts the Y coordinate from lParam.
      /// </summary>
      /// <param name="lParam">lParam containing Y coordinate in high word.</param>
      /// <returns>Y coordinate as signed short.</returns>
      private static int GET_Y_LPARAM(IntPtr lParam)
      {
         return (short) ((long) lParam >> 16);
      }

      /// <summary>
      ///    Window procedure for the dropdown list window.
      /// </summary>
      /// <param name="hwnd">Window handle.</param>
      /// <param name="msg">Message identifier.</param>
      /// <param name="wParam">Message parameter.</param>
      /// <param name="lParam">Message parameter.</param>
      /// <returns>Message result.</returns>
      private static IntPtr ListWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
      {
         // Look up instance for this window
         if (!Instances.TryGetValue(hwnd, out var self))
         {
            return DefWindowProcW(hwnd, msg, wParam, lParam);
         }

         switch (msg)
         {
            case WM_PAINT:
               self.DrawList(hwnd);
               return IntPtr.Zero;
            case WM_MOUSEMOVE:
               self.OnListMouseMove(GET_Y_LPARAM(lParam));
               return IntPtr.Zero;
            case WM_LBUTTONDOWN:
               self.OnListClick(GET_Y_LPARAM(lParam));
               return IntPtr.Zero;
            case WM_MOUSEWHEEL:
               self.OnListMouseWheel(GET_WHEEL_DELTA_WPARAM(wParam));
               return IntPtr.Zero;
            case WM_VSCROLL:
               self.OnVScroll((int) wParam);
               return IntPtr.Zero;
            case WM_KEYDOWN:
               self.OnKeyDown((int) wParam);
               return IntPtr.Zero;
            case WM_CAPTURECHANGED:
               self.HideList();
               return IntPtr.Zero;
            case WM_DESTROY:
               self.HideList();
               break;
         }

         return DefWindowProcW(hwnd, msg, wParam, lParam);
      }

      /// <summary>
      ///    Attempts to register a window class.
      /// </summary>
      /// <param name="cls">Name of the window class.</param>
      /// <param name="proc">Window procedure.</param>
      /// <returns>True if registration succeeded, false otherwise.</returns>
      private static bool TryRegister(string cls, WNDCLASSEX.WndProc proc)
      {
         var wc = new WNDCLASSEX
                     {
                        cbSize = (uint) Marshal.SizeOf<WNDCLASSEX>(),
                        style = CS_DBLCLKS,
                        lpfnWndProc = proc,
                        hInstance = HInstance,
                        hCursor = LoadCursor(IntPtr.Zero, (IntPtr) IDC_ARROW),
                        hbrBackground = IntPtr.Zero,
                        lpszClassName = cls
                     };

         // Attempt to register the class
         var atom = RegisterClassEx(ref wc);
         if (atom != 0)
         {
            return true;
         }

         // Check if class already exists
         var err = Marshal.GetLastWin32Error();
         if (err == ERROR_CLASS_ALREADY_EXISTS)
         {
            return true;
         }

         Logger.Log($"[UpwardCombo] RegisterClassEx '{cls}' failed. GetLastError={err}");
         return false;
      }

      /// <summary>
      ///    Draws a downward-pointing chevron (^) indicator.
      /// </summary>
      /// <param name="hdc">Device context handle.</param>
      /// <param name="rc">Rectangle area for drawing.</param>
      private void DrawChevron(IntPtr hdc, RECT rc)
      {
         // Calculate center position
         var cx = (rc.Left + rc.Right) / 2;
         var cy = ((rc.Top + rc.Bottom) / 2) + 1;

         // Calculate chevron size based on height
         var s = Math.Max(3, _h / 8);

         // Draw three dots forming a chevron
         SetPixel(hdc, cx - s, cy - s, COLOR_TEXT);
         SetPixel(hdc, cx, cy, COLOR_TEXT);
         SetPixel(hdc, cx + s, cy - s, COLOR_TEXT);
      }

      /// <summary>
      ///    Draws the combo box face (button) with selected text and chevron.
      /// </summary>
      /// <param name="hwnd">Window handle.</param>
      private void DrawFace(IntPtr hwnd)
      {
         PAINTSTRUCT ps;
         var hdc = BeginPaint(hwnd, out ps);
         GetClientRect(hwnd, out var rc);

         // Setup GDI scope and font
         using var _ = new GdiScope();
         var old = SelectObject(hdc, _hFont);

         // Fill background
         FillSolidRect(hdc, rc, COLOR_FACE);

         // Calculate text area (leave space for glyph)
         var glyphW = Math.Min(_h / 2, 18);
         var textRc = new RECT {Left = rc.Left + 8, Top = rc.Top, Right = rc.Right - glyphW - 4, Bottom = rc.Bottom};

         // Get display text (selected item or placeholder)
         var txt = _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : _placeholder;
         DrawTextLeft(hdc, textRc, txt);

         // Draw glyph area with chevron
         var gr = new RECT {Left = rc.Right - glyphW, Top = rc.Top, Right = rc.Right, Bottom = rc.Bottom};
         FillSolidRect(hdc, gr, COLOR_FACE_DARK);
         DrawChevron(hdc, gr);

         // Restore and cleanup
         SelectObject(hdc, old);
         EndPaint(hwnd, ref ps);
      }

      /// <summary>
      ///    Draws the dropdown list with visible items.
      /// </summary>
      /// <param name="hwnd">Window handle.</param>
      private void DrawList(IntPtr hwnd)
      {
         PAINTSTRUCT ps;
         var hdc = BeginPaint(hwnd, out ps);
         GetClientRect(hwnd, out var rc);

         // Setup GDI scope and font
         using var _ = new GdiScope();
         var old = SelectObject(hdc, _hFont);

         // Fill list background
         FillSolidRect(hdc, rc, COLOR_LIST_BG);

         // Calculate visible item range
         var start = Math.Max(0, Math.Min(_scrollPos, Math.Max(0, _items.Count - _visibleRows)));
         var end = Math.Min(_items.Count, start + _visibleRows);

         // Draw each visible item
         var y = 0;
         for (var i = start; i < end; i++)
         {
            var row = new RECT {Left = rc.Left, Top = rc.Top + y, Right = rc.Right, Bottom = rc.Top + y + _itemHeight};
            var isHover = i == _hoverIndex;
            var isSel = i == _selectedIndex;

            // Highlight hovered or selected item
            if (isHover || isSel)
            {
               FillSolidRect(hdc, row, isHover ? COLOR_HOVER_BG : COLOR_SEL_BG);
            }

            // Draw item text with padding
            var text = new RECT {Left = rc.Left + 8, Top = row.Top, Right = rc.Right - 8, Bottom = row.Bottom};
            DrawTextLeft(hdc, text, _items[i]);

            y += _itemHeight;
         }

         // Restore and cleanup
         SelectObject(hdc, old);
         EndPaint(hwnd, ref ps);
      }

      /// <summary>
      ///    Hides the dropdown list and releases mouse capture.
      /// </summary>
      private void HideList()
      {
         // Already hidden
         if (!_listVisible)
         {
            return;
         }

         // Reset hover state
         _hoverIndex = -1;

         // Release mouse capture
         ReleaseCapture();

         // Hide list window
         ShowWindow(_listHwnd, SW_HIDE);
         _listVisible = false;
      }

      /// <summary>
      ///    Initializes the combo box windows and resources.
      /// </summary>
      /// <param name="x">X-coordinate.</param>
      /// <param name="y">Y-coordinate.</param>
      /// <param name="w">Width.</param>
      /// <param name="h">Height.</param>
      /// <returns>True if initialization succeeded, false otherwise.</returns>
      private bool Initialize(int x, int y, int w, int h)
      {
         _x = x;
         _y = y;
         _w = w;
         _h = h;

         // Ensure window classes are registered
         if (!EnsureWindowClasses())
         {
            return false;
         }

         // Create default font
         _hFont = CreateDefaultFont();

         // Create face (button) window
         Handle = CreateWindowExW(
         0,
         FaceCls,
         null,
         WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_CLIPSIBLINGS,
         x,
         y,
         w,
         h,
         _parent,
         IntPtr.Zero,
         HInstance,
         IntPtr.Zero);

         // Check face creation
         if (Handle == IntPtr.Zero)
         {
            Logger.Log($"[UpwardCombo] Create FACE failed. GetLastError={Marshal.GetLastWin32Error()}");
            return false;
         }

         // Load initial items
         SetItems(ConfigReader.GetQuestions().Keys.ToList().AsReadOnly());

         // Create dropdown list window
         _listHwnd = CreateWindowExW(
         0,
         ListCls,
         null,
         WS_CHILD | WS_CLIPSIBLINGS | WS_VSCROLL,
         x,
         y - ListHeightPixels(),
         w,
         ListHeightPixels(),
         _parent,
         IntPtr.Zero,
         HInstance,
         IntPtr.Zero);

         // Check list creation
         if (_listHwnd == IntPtr.Zero)
         {
            Logger.Log($"[UpwardCombo] Create LIST failed. GetLastWin32Error={Marshal.GetLastWin32Error()}");
            return false;
         }

         // Bring face to topmost
         SetWindowPos(Handle, (IntPtr) (-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

         // Register instances for message routing
         Instances[Handle] = this;
         Instances[_listHwnd] = this;

         // Set fonts for both windows
         SendMessage(Handle, WM_SETFONT, _hFont, IntPtr.Zero);
         SendMessage(_listHwnd, WM_SETFONT, _hFont, IntPtr.Zero);

         // Initialize scroll metrics
         UpdateScrollMetrics();

         return true;
      }

      /// <summary>
      ///    Calculates the appropriate height for the dropdown list based on item count.
      /// </summary>
      /// <returns>List height in pixels.</returns>
      private int ListHeightPixels()
      {
         var thresholdItems = MIN_LIST_HEIGHT / SCROLL_THRESHOLD_PER_ITEM;

         // Use natural height for few items
         if (_items.Count <= thresholdItems)
         {
            var h = _items.Count * _itemHeight;
            return Math.Max(h, Math.Min(MIN_LIST_HEIGHT, h));
         }

         // Use minimum height for many items
         return MIN_LIST_HEIGHT;
      }

      /// <summary>
      ///    Handles mouse wheel scrolling when dropdown is closed.
      /// </summary>
      /// <param name="delta">Wheel delta value (positive = up, negative = down).</param>
      private void MouseWheel(int delta)
      {
         // No items to scroll
         if (_items.Count == 0)
         {
            return;
         }

         // Delegate to list if visible
         if (_listVisible)
         {
            OnListMouseWheel(delta);
            return;
         }

         // Change selection based on wheel direction
         var dir = delta > 0 ? -1 : 1;
         var next = _selectedIndex < 0 ? 0 : Math.Max(0, Math.Min(_selectedIndex + dir, _items.Count - 1));

         // No change needed
         if (next == _selectedIndex)
         {
            return;
         }

         // Update selection
         _selectedIndex = next;
         InvalidateRect(Handle, IntPtr.Zero, true);
         SelectionChanged?.Invoke(_selectedIndex, GetSelectedText());
      }

      /// <summary>
      ///    Handles keyboard navigation (Up/Down arrows, Enter, Escape).
      /// </summary>
      /// <param name="vk">Virtual key code.</param>
      private void OnKeyDown(int vk)
      {
         switch (vk)
         {
            case VK_DOWN:
               // Show list or scroll down
               if (!_listVisible)
               {
                  ShowList();
               }
               else
               {
                  OnListMouseWheel(-120);
               }

               break;
            case VK_UP:
               // Show list or scroll up
               if (!_listVisible)
               {
                  ShowList();
               }
               else
               {
                  OnListMouseWheel(+120);
               }

               break;
            case VK_RETURN:
            case VK_ESCAPE:
               // Close list
               HideList();
               break;
         }
      }

      /// <summary>
      ///    Handles clicking on an item in the dropdown list.
      /// </summary>
      /// <param name="y">Y-coordinate of the click.</param>
      private void OnListClick(int y)
      {
         // Get cursor position in screen coordinates
         GetCursorPos(out var pt);

         // Convert to list window coordinates
         MapWindowPoints(IntPtr.Zero, _listHwnd, ref pt, 1);

         // Get list client area
         GetClientRect(_listHwnd, out var rc);

         // Check if click is outside list
         if (pt.X < rc.Left || pt.X >= rc.Right || pt.Y < rc.Top || pt.Y >= rc.Bottom)
         {
            HideList();
            return;
         }

         // Calculate clicked item index
         var idx = _scrollPos + (y / _itemHeight);

         // Update selection if valid
         if (idx >= 0 && idx < _items.Count)
         {
            _selectedIndex = idx;
            InvalidateRect(Handle, IntPtr.Zero, true);
            SelectionChanged?.Invoke(_selectedIndex, GetSelectedText());
         }

         // Close list after selection
         HideList();
      }

      /// <summary>
      ///    Handles mouse movement over the dropdown list for hover effects.
      /// </summary>
      /// <param name="y">Y-coordinate of the mouse.</param>
      private void OnListMouseMove(int y)
      {
         // Calculate item index at mouse position
         var idx = _scrollPos + (y / _itemHeight);

         // Ignore if out of bounds
         if (idx < 0 || idx >= _items.Count)
         {
            return;
         }

         // Already hovering this item
         if (idx == _hoverIndex)
         {
            return;
         }

         // Update hover and repaint
         _hoverIndex = idx;
         InvalidateRect(_listHwnd, IntPtr.Zero, true);
      }

      /// <summary>
      ///    Handles mouse wheel scrolling in the dropdown list.
      /// </summary>
      /// <param name="delta">Wheel delta value (positive = up, negative = down).</param>
      private void OnListMouseWheel(int delta)
      {
         // No items to scroll
         if (_items.Count == 0)
         {
            return;
         }

         // Calculate scroll step (1/3 of visible rows)
         var lines = Math.Max(1, _visibleRows / 3);
         var step = delta > 0 ? -lines : lines;

         // Apply scroll
         SetScrollPosition(_scrollPos + step);
      }

      /// <summary>
      ///    Handles vertical scrollbar events (line up/down, page up/down, thumb tracking).
      /// </summary>
      /// <param name="wParam">Scroll event parameters.</param>
      private void OnVScroll(int wParam)
      {
         // Extract scroll request code
         var request = wParam & 0xFFFF;

         // Get current scroll info
         var si = new SCROLLINFO {cbSize = (uint) Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_ALL};
         GetScrollInfo(_listHwnd, 1, ref si);

         var pos = _scrollPos;

         // Process scroll request
         switch (request)
         {
            case SB_LINEUP:
               pos -= 1;
               break;
            case SB_LINEDOWN:
               pos += 1;
               break;
            case SB_PAGEUP:
               pos -= _visibleRows;
               break;
            case SB_PAGEDOWN:
               pos += _visibleRows;
               break;
            case SB_THUMBPOSITION:
            case SB_THUMBTRACK:
               pos = si.nTrackPos;
               break;
            case SB_ENDSCROLL:
               return;
         }

         // Apply new position
         SetScrollPosition(pos);
      }

      /// <summary>
      ///    Positions the dropdown list relative to the face, preferring upward placement.
      /// </summary>
      private void PositionList()
      {
         // Get face window bounds in screen coordinates
         GetWindowRect(Handle, out var wr);
         var tl = new POINT {X = wr.Left, Y = wr.Top};
         var br = new POINT {X = wr.Right, Y = wr.Bottom};

         // Convert to parent coordinates
         MapWindowPoints(IntPtr.Zero, _parent, ref tl, 1);
         MapWindowPoints(IntPtr.Zero, _parent, ref br, 1);

         var faceX = tl.X;
         var faceY = tl.Y;
         var faceW = Math.Max(1, br.X - tl.X);
         var faceH = Math.Max(1, br.Y - tl.Y);

         var lh = ListHeightPixels();

         // Try positioning above face
         var wx = faceX;
         var wy = faceY - lh;

         // Get parent client area
         GetClientRect(_parent, out var pr);

         // If doesn't fit above, position below
         if (wy < pr.Top)
         {
            wy = faceY + faceH;

            // If doesn't fit below either, clamp to parent bounds
            if (wy + lh > pr.Bottom)
            {
               wy = Math.Max(pr.Top, pr.Bottom - lh);
            }
         }

         // Position list window
         SetWindowPos(_listHwnd, IntPtr.Zero, wx, wy, faceW, lh, SWP_NOZORDER | SWP_NOACTIVATE);

         // Update scroll for new size
         UpdateScrollMetrics();
      }

      /// <summary>
      ///    Sets the scroll position and updates the scrollbar.
      /// </summary>
      /// <param name="newPos">Desired scroll position.</param>
      private void SetScrollPosition(int newPos)
      {
         // Clamp to valid range
         var maxPos = Math.Max(0, _items.Count - _visibleRows);
         _scrollPos = Math.Max(0, Math.Min(newPos, maxPos));

         // Update scrollbar
         var si = new SCROLLINFO {cbSize = (uint) Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_POS, nPos = _scrollPos};
         SetScrollInfo(_listHwnd, 1, ref si, true);

         // Repaint list
         InvalidateRect(_listHwnd, IntPtr.Zero, true);
      }

      /// <summary>
      ///    Shows the dropdown list and captures mouse input.
      /// </summary>
      private void ShowList()
      {
         // Position list relative to face
         PositionList();

         // Show list window
         ShowWindow(_listHwnd, SW_SHOWNOACTIVATE);

         // Capture mouse for tracking clicks outside
         SetCapture(_listHwnd);
         _listVisible = true;

         // Repaint list
         InvalidateRect(_listHwnd, IntPtr.Zero, true);
      }

      /// <summary>
      ///    Toggles the dropdown list visibility.
      /// </summary>
      private void ToggleList()
      {
         if (_listVisible)
         {
            HideList();
         }
         else
         {
            ShowList();
         }
      }

      /// <summary>
      ///    Updates scroll metrics when list size or item count changes, and ensures selected item is visible.
      /// </summary>
      private void UpdateScrollMetrics()
      {
         var thresholdItems = MIN_LIST_HEIGHT / SCROLL_THRESHOLD_PER_ITEM;
         var listHeight = ListHeightPixels();

         // Calculate visible rows
         _visibleRows = Math.Max(1, listHeight / _itemHeight);

         // Determine if scrollbar is needed
         var needScroll = _items.Count > thresholdItems;
         ShowScrollBar(_listHwnd, 1, needScroll);

         // Configure scrollbar
         var si = new SCROLLINFO
                     {
                        cbSize = (uint) Marshal.SizeOf<SCROLLINFO>(),
                        fMask = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_DISABLENOSCROLL,
                        nMin = 0,
                        nMax = Math.Max(0, _items.Count - 1),
                        nPage = (uint) _visibleRows,
                        nPos = Math.Max(0, Math.Min(_scrollPos, Math.Max(0, _items.Count - _visibleRows)))
                     };

         SetScrollInfo(_listHwnd, 1, ref si, true);
         _scrollPos = (int) si.nPos;

         // Ensure selected item is visible
         if (_selectedIndex >= 0 && _items.Count > 0)
         {
            // Scroll up if selected is above visible area
            if (_selectedIndex < _scrollPos)
            {
               _scrollPos = _selectedIndex;
            }

            // Scroll down if selected is below visible area
            var lastVisible = (_scrollPos + _visibleRows) - 1;
            if (_selectedIndex > lastVisible)
            {
               _scrollPos = (_selectedIndex - _visibleRows) + 1;
            }

            // Apply adjusted scroll position
            si.nPos = _scrollPos;
            SetScrollInfo(_listHwnd, 1, ref si, true);
         }
      }

      /// <summary>
      ///    Nested class for managing GDI object lifetimes (disposable scope).
      /// </summary>
      private sealed class GdiScope : IDisposable
      {
         /// <summary>
         ///    Disposes of GDI resources (currently no cleanup needed).
         /// </summary>
         public void Dispose()
         {
         }
      }
   }
}