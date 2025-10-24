namespace GithubCopilotQuickQuestions.Scripts.Utilities
{
   using System;
   using System.Linq;

   public static class GlobalConstants
   {
      public const int WM_KILLFOCUS = 0x0008;

      public const int WM_ACTIVATE = 0x0006;

      public const int WA_INACTIVE = 0x0000;

      public const int CB_GETDROPPEDSTATE = 0x0157;

      public const uint COLOR_FACE = 0x002A2A2A;

      public const uint COLOR_FACE_DARK = 0x00202020;

      public const uint COLOR_HOVER_BG = 0x00303030;

      public const uint COLOR_LIST_BG = 0x00222222;

      public const uint COLOR_SEL_BG = 0x003A3A3A;

      public const uint COLOR_TEXT = 0x00E0E0E0;

      public const string CONFIG_KEY_ENABLE_LOGGING = "logging";

      public const string CONFIG_KEY_OVERLAY_AUTOLOAD = "autoLoadCopilotChatOnVsStart";

      public const int CS_DBLCLKS = 0x0008;

      public const uint CWP_SKIPDISABLED = 0x0002;

      public const uint CWP_SKIPINVISIBLE = 0x0001;

      public const uint CWP_SKIPSIBLINGS = 0x0004;

      public const int DT_END_ELLIPSIS = 0x00008000;

      public const int DT_LEFT = 0x00000000;

      public const int DT_SINGLELINE = 0x00000020;

      public const int DT_VCENTER = 0x00000004;

      public const int ERROR_CLASS_ALREADY_EXISTS = 1410;

      public const uint GA_ROOTOWNER = 3;

      public const string GITHUB_COPILOT_MODELS_COMBOBOX_ID = "PART_Models";

      public const string GITHUB_COPILOT_MODELS_COMBOBOX_NAME = "Models";

      public const string GITHUB_COPILOT_PANE_CLASS = "ViewPresenter";

      public const string GITHUB_COPILOT_PANE_NAME = "GitHub Copilot Chat";

      public const int GWL_EXSTYLE = -20;

      public const int GWL_STYLE = -16;

      public const int IDC_ARROW = 32512;

      public const int MIN_LIST_HEIGHT = 100;

      public const int SB_ENDSCROLL = 8;

      public const int SB_LINEDOWN = 1;

      public const int SB_LINEUP = 0;

      public const int SB_PAGEDOWN = 3;

      public const int SB_PAGEUP = 2;

      public const int SB_THUMBPOSITION = 4;

      public const int SB_THUMBTRACK = 5;

      public const int SCROLL_THRESHOLD_PER_ITEM = 20;

      public const int SIF_ALL = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS;

      public const int SIF_DISABLENOSCROLL = 0x8;

      public const int SIF_PAGE = 0x2;

      public const int SIF_POS = 0x4;

      public const int SIF_RANGE = 0x1;

      public const int SW_HIDE = 0;

      public const int SW_SHOW = 5;

      public const int SW_SHOWNOACTIVATE = 4;

      public const uint SWP_FRAMECHANGED = 0x0020;

      public const uint SWP_HIDEWINDOW = 0x0080;

      public const uint SWP_NOACTIVATE = 0x0010;

      public const uint SWP_NOMOVE = 0x0002;

      public const uint SWP_NOSENDCHANGING = 0x0400;

      public const uint SWP_NOSIZE = 0x0001;

      public const uint SWP_NOZORDER = 0x0004;

      public const uint SWP_SHOWWINDOW = 0x0040;

      public const int VK_DOWN = 0x28;

      public const int VK_ESCAPE = 0x1B;

      public const int VK_LBUTTON = 0x01;

      public const int VK_RBUTTON = 0x02;

      public const int VK_RETURN = 0x0D;

      public const int VK_UP = 0x26;

      public const int WM_CAPTURECHANGED = 0x0215;

      public const int WM_DESTROY = 0x0002;

      public const int WM_KEYDOWN = 0x0100;

      public const int WM_LBUTTONDOWN = 0x0201;

      public const int WM_LBUTTONUP = 0x0202;

      public const int WM_MOUSEMOVE = 0x0200;

      public const int WM_MOUSEWHEEL = 0x020A;

      public const int WM_PAINT = 0x000F;

      public const int WM_SETFONT = 0x0030;

      public const int WM_VSCROLL = 0x0115;

      public const int WS_CHILD = 0x40000000;

      public const int WS_CLIPCHILDREN = 0x02000000;

      public const int WS_CLIPSIBLINGS = 0x04000000;

      public const int WS_EX_APPWINDOW = 0x00040000;

      public const int WS_EX_TOOLWINDOW = 0x00000080;

      public const int WS_EX_TOPMOST = 0x00000008;

      public const int WS_POPUP = unchecked((int) 0x80000000);

      public const int WS_TABSTOP = 0x00010000;

      public const int WS_VISIBLE = 0x10000000;

      public const int WS_VSCROLL = 0x00200000;

      private const int SIF_TRACKPOS = 0x10;

      public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
   }
}