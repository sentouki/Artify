using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Artify.Views.misc;

namespace Artify
{
    public partial class App : Application
    {
        // remove the storyboards to free memory
        private void Animation_OnCompleted(object sender, EventArgs e)
        {
            (sender as Storyboard)?.Remove();
            GC.Collect();
        }

        private void InputField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var inputfield = sender as TextBox;
            AttachedProperties.SetIsEmpty(inputfield, string.IsNullOrEmpty(inputfield.Text));
        }
    }
}
