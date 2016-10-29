using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

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
        internal static bool IsScrolledToEnd(this TextBox textBox)
        {
            if (textBox.ViewportHeight > textBox.ExtentHeight) return true;
            return Math.Abs(textBox.VerticalOffset + textBox.ViewportHeight - textBox.ExtentHeight) < 1;
        }
    }
}
