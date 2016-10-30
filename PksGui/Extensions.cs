using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PksGui
{
    internal static class Extensions
    {
        internal static void AppendTextAndScroll(this TextBox output, string text)
        {
            var scroll = IsScrolledToEnd(output);
            output.AppendText(text);
            if (!scroll) return;
            //output.CaretIndex = output.Text.Length;
            output.ScrollToEnd();
        }

        private static bool IsScrolledToEnd(this TextBoxBase textBox)
        {
            if (textBox.ViewportHeight > textBox.ExtentHeight) return true;
            return Math.Abs(textBox.VerticalOffset + textBox.ViewportHeight - textBox.ExtentHeight) < 1;
        }
    }
}