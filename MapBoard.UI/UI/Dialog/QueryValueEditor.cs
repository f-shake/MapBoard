using System;
using System.Windows;
using System.Windows.Controls;

namespace MapBoard.UI.Dialog
{
    public class QueryValueEditor : ContentControl
    {
        static QueryValueEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QueryValueEditor),
                new FrameworkPropertyMetadata(typeof(QueryValueEditor)));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(object), typeof(QueryValueEditor));

        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty OperatorProperty =
            DependencyProperty.Register("Operator", typeof(Enum), typeof(QueryValueEditor));

        public Enum Operator
        {
            get => (Enum)GetValue(OperatorProperty);
            set => SetValue(OperatorProperty, value);
        }
    }
}