namespace GithubCopilotQuickQuestions.Scripts.POCO
{
   using System.Linq;
   using System.Runtime.InteropServices;

   [StructLayout(LayoutKind.Sequential)]
   public struct LOGFONT
   {
   #region NO REORDER

      public byte lfCharSet;

      public byte lfClipPrecision;

      public int lfEscapement;

      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
      public string lfFaceName;

      public int lfHeight;

      public byte lfItalic;

      public int lfOrientation;

      public byte lfOutPrecision;

      public byte lfPitchAndFamily;

      public byte lfQuality;

      public byte lfStrikeOut;

      public byte lfUnderline;

      public int lfWeight;

      public int lfWidth;

   #endregion
   }
}