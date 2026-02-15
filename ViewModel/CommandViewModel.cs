using System.Windows.Input;

namespace MoodScanAnalyzer.ViewModel
{
    public class CommandViewModel : ICommand
    {
        private readonly Action<object> _executeAction;
        private readonly Predicate<object> _canExecuteAction;

        public CommandViewModel(Action<object> executeAction)
        {
            _executeAction = executeAction;
            _canExecuteAction = null;
        }

        public CommandViewModel(Action<object> executeAction, Predicate<object> canExecuteAction)
        {
            _executeAction = executeAction;
            _canExecuteAction = canExecuteAction;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecuteAction == null ? true : _canExecuteAction(parameter);
        }

        public void Execute(object? parameter)
        {
            _executeAction(parameter);
        }
    }
}
