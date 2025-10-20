namespace GithubCopilotQuickQuestions.Scripts
{
   using System.Drawing;
   using System.Linq;
   using System.Windows.Forms;

   public sealed class CustomComboBox : ComboBox
   {
      private const int WM_MOUSEWHEEL = 0x020A;

      private const int WM_MOUSEHWHEEL = 0x020E;

      private Rectangle ButtonRect()
      {
         var w = SystemInformation.VerticalScrollBarWidth;
         if (w < 12)
         {
            w = 12;
         }

         return new Rectangle(Width - w, 0, w, Height);
      }

      protected override void WndProc(ref Message m)
      {
         if (m.Msg == WM_MOUSEWHEEL || m.Msg == WM_MOUSEHWHEEL)
         {
            return;
         }

         base.WndProc(ref m);
      }

      protected override void OnMouseWheel(MouseEventArgs e)
      {
         return;
      }

      protected override void OnMouseDown(MouseEventArgs e)
      {
         if (e.Button == MouseButtons.Left && ButtonRect().Contains(e.Location))
         {
            base.OnMouseDown(e);
            return;
         }

         Focus();
      }

      protected override void OnMouseDoubleClick(MouseEventArgs e)
      {
         if (e.Button == MouseButtons.Left && ButtonRect().Contains(e.Location))
         {
            base.OnMouseDoubleClick(e);
            return;
         }

         Focus();
      }
   }
}