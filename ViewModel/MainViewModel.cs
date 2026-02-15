using System.Windows.Input;
using MoodScanAnalyzer.Service;

namespace MoodScanAnalyzer.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        private BaseViewModel _currentChildView;
        private readonly VideoChartService _videoChartService;
        private readonly VideoLoaderService _videoLoaderService;

        private bool _isScrollEnabled = true;
        public bool IsScrollEnabled
        {
            get => _isScrollEnabled;
            set
            {
                _isScrollEnabled = value;
                OnPropertyChanged(nameof(IsScrollEnabled));
            }
        }

        public BaseViewModel CurrentChildView
        {
            get
            {
                return _currentChildView;
            }

            set
            {
                _currentChildView = value;
                OnPropertyChanged(nameof(CurrentChildView));
            }
        }
        public ICommand ShowStartViewCommand { get; }
        public ICommand ShowVideoChartViewCommand { get; }

        public MainViewModel()
        {
            _videoChartService = new VideoChartService();
            _videoLoaderService = new VideoLoaderService();
            ShowStartViewCommand = new CommandViewModel(ExecuteShowStartViewCommand);
            ShowVideoChartViewCommand = new CommandViewModel(ExecuteShowVideoChartViewCommand);

            ExecuteShowStartViewCommand(null);
        }

        private void ExecuteShowStartViewCommand(object obj)
        {
            CurrentChildView = new StartViewModel(this);
            IsScrollEnabled = true;
        }

        public void ExecuteShowVideoChartViewCommand(object obj)
        {
            CurrentChildView = new VideoChartViewModel(this, _videoChartService, _videoLoaderService);

            IsScrollEnabled = true;
        }
    }
}
