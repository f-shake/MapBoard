using CommunityToolkit.Maui.Views;
using FzLib.Program;
using MapBoard.Mapping.Model;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using LayoutAlignment = Microsoft.Maui.Primitives.LayoutAlignment;

namespace MapBoard.Views
{
    public static class PopupMenu
    {
        public static Task<int> PopupMenuAsync(this ListView list, ItemTappedEventArgs e, IEnumerable<PopupMenuItem> items, string title = null)
        {
            var view = list.GetVisualTreeDescendants()
             .OfType<View>()
             .Where(p => p is Grid)
             .Where(p => p.BindingContext == e.Item)
             .FirstOrDefault(list);
            return view.PopupMenuAsync(items, title);
        }

        //https://github.com/CommunityToolkit/Maui/issues/1516
        public static async Task<int> PopupMenuAsync(this View view, IEnumerable<PopupMenuItem> items, string title = null)
        {
            ArgumentNullException.ThrowIfNull(items);
            if(!items.Any())
            {
                throw new ArgumentException("没有提供任何菜单项", nameof(items));
            }
            Popup ppp = new Popup
            {
                Color = Colors.Transparent,
                Anchor = view,
                CanBeDismissedByTappingOutsideOfPopup = true,
            };
            var template = new DataTemplate(() =>
            {
                TextCell tc = new TextCell();
                tc.SetBinding(TextCell.TextProperty, new Binding(nameof(PopupMenuItem.Text)));
                tc.SetBinding(TextCell.DetailProperty, new Binding(nameof(PopupMenuItem.Detail)));
                tc.SetBinding(TextCell.IsEnabledProperty, new Binding(nameof(PopupMenuItem.IsEnabled)));
                tc.SetAppThemeColor(TextCell.TextColorProperty, Colors.Black, Colors.White);
                return tc;
            });
            ListView list = new ListView()
            {
                BackgroundColor = Colors.Transparent,
                SelectionMode = ListViewSelectionMode.None,
                HorizontalOptions = LayoutOptions.Fill,
                ItemsSource = items,
                ItemTemplate = template,
                WidthRequest = 200,
                VerticalOptions = LayoutOptions.Center,
                VerticalScrollBarVisibility = ScrollBarVisibility.Never,
                SeparatorVisibility = SeparatorVisibility.None
            };
            list.ItemTapped += List_ItemTapped;

            void List_ItemTapped(object sender, ItemTappedEventArgs e)
            {
                list.ItemTapped -= List_ItemTapped;
                ppp.Close(e.ItemIndex);
            }

            VerticalStackLayout layout = new VerticalStackLayout()
            {
                Margin = new Thickness(8),
            };
            if (title != null)
            {
                Label titleLabel = new Label()
                {
                    Text = title,
                    FontSize = 24,
                    Margin = new Thickness(8, 0, 0, 0),
                    TextColor = Colors.Gray
                };
                layout.Add(titleLabel);
            }
            layout.Add(list);
            Border bd = new Border()
            {
                Content = layout,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle()
                {
                    CornerRadius = new CornerRadius(4)
                },
                Shadow = new Shadow()
                {
                    Opacity = 0.6f
                },
            };
            bd.SetAppThemeColor(Border.BackgroundColorProperty, Colors.White, Colors.Black);
            ppp.Content = bd;
            try
            {
                var result = await MainPage.Current.ShowPopupAsync(ppp);
                return result == null ? -1 : (int)result;
            }
            catch (ObjectDisposedException)
            {
                // 忽略或记录日志
                return -1;
            }
        }

        public class PopupMenuItem
        {
            public PopupMenuItem()
            {

            }
            public PopupMenuItem(string text)
            {
                Text = text;
            }
            public string Detail { get; set; }
            public bool IsEnabled { get; set; } = true;
            public string Text { get; set; }
            public object Tag { get; set; }
            public static implicit operator PopupMenuItem(string text) => new PopupMenuItem(text);
        }
    }
}
