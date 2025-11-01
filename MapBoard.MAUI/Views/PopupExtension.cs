using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.Views
{
    public static class PopupExtension
    {
        public static void TryClose(this Popup popup, object result = null)
        {
            try
            {
                popup.Close(result);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
    }
}
