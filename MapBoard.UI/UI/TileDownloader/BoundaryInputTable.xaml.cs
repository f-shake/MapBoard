using MapBoard.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MapBoard.UI.TileDownloader
{
    /// <summary>
    /// 边界范围输入控件
    /// </summary>
    public partial class BoundaryInputTable : UserControlBase
    {
        public BoundaryInputTable()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 获取<see cref="double"/>类型的值
        /// </summary>
        /// <returns></returns>
        public GeoRect<double> GetDoubleValue()
        {
            if (!IsDoubleLegal(out double left, out double right, out double top, out double bottom))
            {
                return null;
            }
            return new GeoRect<double>(left, right, bottom, top);
        }

        /// <summary>
        /// 获取<see cref="int"/>类型的值
        /// </summary>
        /// <returns></returns>
        public GeoRect<int> GetIntValue()
        {
            if (!IsIntLegal(out int left, out int right, out int top, out int bottom))
            {
                return null;
            }
            return new GeoRect<int>(left, right, bottom, top);
        }

        /// <summary>
        /// 设置<see cref="double"/>类型的值
        /// </summary>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="right"></param>
        /// <param name="bottom"></param>
        public void SetDoubleValue(double left, double top, double right, double bottom)
        {
            txtLeft.Text = Math.Round(left,6).ToString();
            txtTop.Text = Math.Round(top, 6).ToString();
            txtRight.Text = Math.Round(right, 6).ToString();
            txtBottom.Text = Math.Round(bottom, 6).ToString();
        }

        /// <summary>
        /// 设置<see cref="int"/>类型的值
        /// </summary>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="right"></param>
        /// <param name="bottom"></param>
        public void SetIntValue(int left, int top, int right, int bottom)
        {
            txtLeft.Text = left.ToString();
            txtTop.Text = top.ToString();
            txtRight.Text = right.ToString();
            txtBottom.Text = bottom.ToString();
        }

        /// <summary>
        /// 判断输入的是否为合法的<see cref="double"/>值
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <returns></returns>
        private bool IsDoubleLegal(out double left, out double right, out double top, out double bottom)
        {
            left = right = top = bottom = 0;
            return (double.TryParse(txtTop.Text, out top)
                && double.TryParse(txtLeft.Text, out left)
                && double.TryParse(txtRight.Text, out right)
                && double.TryParse(txtBottom.Text, out bottom) &&
                !(left > right || bottom > top));
        }

        /// <summary>
        /// 判断输入的是否为合法的<see cref="int"/>值
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <returns></returns>
        private bool IsIntLegal(out int left, out int right, out int top, out int bottom)
        {
            left = right = top = bottom = 0;
            return (int.TryParse(txtTop.Text, out top)
                && int.TryParse(txtLeft.Text, out left)
                && int.TryParse(txtRight.Text, out right)
                && int.TryParse(txtBottom.Text, out bottom) &&
                !(left < 0 || right < 0 || top < 0 || bottom < 0
                || left > right || bottom < top));
        }
    }
}