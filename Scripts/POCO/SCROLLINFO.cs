namespace GithubCopilotQuickQuestions.Scripts.POCO
{
   using System.Linq;
   using System.Runtime.InteropServices;

   [StructLayout(LayoutKind.Sequential)]
   public struct SCROLLINFO
   {
   #region NO REORDER

      public uint cbSize;

      public uint fMask;

      public int nMin;

      public int nMax;

      public uint nPage;

      public int nPos;

      public int nTrackPos;

   #endregion
   }
}