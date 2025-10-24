namespace GithubCopilotQuickQuestions.Scripts.POCO
{
   using System;
   using System.Linq;
   using System.Runtime.InteropServices;

   [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
   public struct WNDCLASSEX
   {
   #region NO REORDER

      public uint cbSize;

      public uint style;

      public WndProc lpfnWndProc;

      public int cbClsExtra;

      public int cbWndExtra;

      public IntPtr hInstance;

      public IntPtr hIcon;

      public IntPtr hCursor;

      public IntPtr hbrBackground;

      [MarshalAs(UnmanagedType.LPWStr)]
      public string lpszMenuName;

      [MarshalAs(UnmanagedType.LPWStr)]
      public string lpszClassName;

      public IntPtr hIconSm;

      [UnmanagedFunctionPointer(CallingConvention.Winapi)]
      public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

   #endregion
   }
}