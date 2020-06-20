using System;
using System.Windows;
using System.Windows.Media.Animation;

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
    }
}
