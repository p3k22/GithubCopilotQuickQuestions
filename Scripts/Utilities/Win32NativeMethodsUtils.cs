namespace GithubCopilotQuickQuestions.Scripts.Utilities
{
   using GithubCopilotQuickQuestions.Scripts.POCO;

   using System;
   using System.Linq;
   using System.Runtime.InteropServices;

   using static GlobalConstants;

   internal static class Win32NativeMethodsUtils
   {
      [DllImport("user32.dll")]
      internal static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

      [DllImport("user32.dll")]
      internal static extern IntPtr ChildWindowFromPointEx(IntPtr parent, POINT pt, uint flags);

      [DllImport("gdi32.dll")]
      internal static extern IntPtr CreateFontIndirect(ref LOGFONT lplf);

      [DllImport("gdi32.dll")]
      internal static extern IntPtr CreateSolidBrush(uint crColor);

      [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
      internal static extern IntPtr CreateWindowExW(
         int dwExStyle,
         string lpClassName,
         string lpWindowName,
         int dwStyle,
         int X,
         int Y,
         int nWidth,
         int nHeight,
         IntPtr hWndParent,
         IntPtr hMenu,
         IntPtr hInstance,
         IntPtr lpParam);

      [DllImport("comctl32.dll", SetLastError = true)]
      internal static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

      [DllImport("user32.dll")]
      internal static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

      [DllImport("gdi32.dll")]
      internal static extern bool DeleteObject(IntPtr hObject);

      [DllImport("user32.dll")]
      internal static extern bool DestroyWindow(IntPtr hWnd);

      [DllImport("user32.dll", EntryPoint = "DrawTextW", CharSet = CharSet.Unicode, SetLastError = true)]
      internal static extern int DrawTextW(IntPtr hdc, string lpchText, int cchText, ref RECT lprc, uint dwDTFormat);

      [DllImport("user32.dll")]
      internal static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

      [DllImport("user32.dll")]
      internal static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

      [DllImport("user32.dll")]
      internal static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

      [DllImport("user32.dll")]
      internal static extern short GetAsyncKeyState(int vKey);

      [DllImport("user32.dll")]
      internal static extern IntPtr GetCapture();

      [DllImport("user32.dll")]
      internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

      [DllImport("user32.dll")]
      internal static extern bool GetCursorPos(out POINT lpPoint);

      [DllImport("user32.dll")]
      internal static extern IntPtr GetForegroundWindow();

      [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
      internal static extern IntPtr GetModuleHandle(string lpModuleName);

      [DllImport("user32.dll")]
      internal static extern IntPtr GetParent(IntPtr hWnd);

      [DllImport("user32.dll", SetLastError = true)]
      internal static extern bool GetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO lpsi);

      internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
      {
         return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
      }

      [DllImport("user32.dll")]
      internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

      [DllImport("user32.dll")]
      internal static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

      [DllImport("user32.dll")]
      internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

      [DllImport("user32.dll")]
      internal static extern bool IsIconic(IntPtr hWnd);

      internal static bool IsProperChild(IntPtr hwnd, IntPtr parent)
      {
         if (hwnd == IntPtr.Zero || parent == IntPtr.Zero)
         {
            return false;
         }

         var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
         var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

         if ((style & WS_CHILD) == 0)
         {
            return false;
         }

         if ((style & WS_POPUP) != 0)
         {
            return false;
         }

         if ((ex & WS_EX_TOPMOST) != 0)
         {
            return false;
         }

         if ((ex & WS_EX_APPWINDOW) != 0)
         {
            return false;
         }

         if (GetParent(hwnd) != parent)
         {
            return false;
         }

         var rootSelf = GetAncestor(hwnd, GA_ROOTOWNER);
         var rootParent = GetAncestor(parent, GA_ROOTOWNER);
         return rootSelf == rootParent;
      }

      [DllImport("user32.dll")]
      internal static extern bool IsWindow(IntPtr hWnd);

      [DllImport("user32.dll", SetLastError = true)]
      internal static extern bool IsWindowVisible(IntPtr hWnd);

      [DllImport("user32.dll")]
      internal static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

      [DllImport("user32.dll")]
      internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref POINT lpPoints, int cPoints);

      [DllImport("user32.dll")]
      internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

      [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
      internal static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

      [DllImport("user32.dll")]
      internal static extern bool ReleaseCapture();

      [DllImport("comctl32.dll", SetLastError = true)]
      internal static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass);

      [DllImport("user32.dll")]
      internal static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

      [DllImport("gdi32.dll")]
      internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

      [DllImport("user32.dll")]
      internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

      [DllImport("user32.dll", CharSet = CharSet.Unicode)]
      internal static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

      [DllImport("gdi32.dll")]
      internal static extern int SetBkMode(IntPtr hdc, int mode);

      [DllImport("user32.dll")]
      internal static extern IntPtr SetCapture(IntPtr hWnd);

      [DllImport("user32.dll")]
      internal static extern IntPtr SetFocus(IntPtr hWnd);

      [DllImport("user32.dll")]
      internal static extern bool SetForegroundWindow(IntPtr hWnd);

      [DllImport("user32.dll")]
      internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);

      [DllImport("gdi32.dll")]
      internal static extern uint SetPixel(IntPtr hdc, int X, int Y, uint crColor);

      [DllImport("user32.dll", SetLastError = true)]
      internal static extern int SetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO lpsi, bool redraw);

      [DllImport("gdi32.dll")]
      internal static extern uint SetTextColor(IntPtr hdc, uint crColor);

      internal static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
      {
         return IntPtr.Size == 8 ?
                   SetWindowLongPtr64(hWnd, nIndex, value) :
                   new IntPtr(SetWindowLong32(hWnd, nIndex, value.ToInt32()));
      }

      [DllImport("user32.dll", SetLastError = true)]
      internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

      [DllImport("comctl32.dll", SetLastError = true)]
      internal static extern bool SetWindowSubclass(
         IntPtr hWnd,
         SUBCLASSPROC pfnSubclass,
         UIntPtr uIdSubclass,
         UIntPtr dwRefData);

      [DllImport("user32.dll")]
      internal static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

      [DllImport("user32.dll")]
      internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

      [DllImport("user32.dll")]
      internal static extern IntPtr WindowFromPoint(POINT pt);

      [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
      private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

      [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
      private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

      [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
      private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

      [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
      private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

      internal delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
   }
}