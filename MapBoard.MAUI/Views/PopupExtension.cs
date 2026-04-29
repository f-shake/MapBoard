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
        public static void TryClose(this Popup popup)
        {
            try
            {
                popup.CloseAsync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        public static void TryClose<T>(this Popup<T> popup, T result = default)
        {
            try
            {
                popup.CloseAsync(result);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
    }
}
