namespace GithubCopilotQuickQuestions.Scripts.POCO
{
   using System;
   using System.Linq;
   using System.Runtime.InteropServices;

   [StructLayout(LayoutKind.Sequential)]
   public struct PAINTSTRUCT
   {
   #region NO REORDER

      public int fErase;

      public int fIncUpdate;

      public int fRestore;

      public IntPtr hdc;

      public RECT rcPaint;

      public int reserved1, reserved2, reserved3, reserved4, reserved5, reserved6, reserved7, reserved8;

   #endregion
   }
}