namespace Artify.ViewModels.misc
{
    /// <summary>
    /// helper interface which allows to close the view from the viewmodel without violating the mvvm pattern
    /// </summary>
    public interface IShutDown
    {
        void AppShutDown();
    }
}
