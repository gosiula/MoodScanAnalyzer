using System.Windows.Input;

namespace MoodScanAnalyzer.ViewModel
{
    public class StartViewModel : BaseViewModel
    {
        private readonly MainViewModel _mainViewModel;

        public ICommand StartCommand { get; }

        public StartViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            StartCommand = new CommandViewModel(ExecuteStartCommand);
        }

        // Start button logic - switch to video with chart view
        private void ExecuteStartCommand(object obj)
        {
            _mainViewModel.ShowVideoChartViewCommand.Execute(null);
        }
    }
}
