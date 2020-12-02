using System;
using System.Windows.Input;

namespace Artify.ViewModels
{
    public class RelayCommand : ICommand
    {
        public event EventHandler CanExecuteChanged = (sender, e) => { };
        public bool CanExecute(object parameter) => true;

        private readonly Action _action;

        public RelayCommand(Action action)
        {
            _action = action;
        }
        public void Execute(object parameter)
        {
            _action?.Invoke();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged = (sender, e) => { };
        public bool CanExecute(object parameter) => true;
        private readonly Action<T> _action;
        public RelayCommand(Action<T> action)
        {
            _action = action;
        }
        public void Execute(object parameter)
        {
            _action?.Invoke((T)parameter);
        }
    }
}
