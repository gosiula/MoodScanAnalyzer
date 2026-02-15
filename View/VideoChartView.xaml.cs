using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MoodScanAnalyzer.ViewModel;

namespace MoodScanAnalyzer.View
{
    public partial class VideoChartView : UserControl
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private bool _isSeeking = false;

        public VideoChartView()
        {
            InitializeComponent();

            EmotionPlotView.PreviewMouseDown += PlotView_MouseDown_Wpf;

            Loaded += UserControl_Loaded;
            Unloaded += UserControl_Unloaded;

            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is VideoChartViewModel oldVm)
                {
                    oldVm.PropertyChanged -= Vm_PropertyChanged;
                }

                if (e.NewValue is VideoChartViewModel newVm)
                {
                    newVm.PropertyChanged += Vm_PropertyChanged;
                }
            };
        }


        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Position = TimeSpan.Zero;
            VideoPlayer.Play();
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement player && DataContext is VideoChartViewModel vm)
            {
                VideoPlayer.Play();
            }
        }

        // Method to handle clicking on the graph and moving the video time
        private void PlotView_MouseDown_Wpf(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as VideoChartViewModel;
            if (vm == null) return;

            var position = e.GetPosition(EmotionPlotView);
            double clickedTime = vm.EmotionPlotModel.DefaultXAxis.InverseTransform(position.X);
            clickedTime = Math.Max(0, Math.Min(clickedTime, vm.SelectedVideo.Length));

            _isSeeking = true;

            vm.CurrentTime = clickedTime;
            VideoPlayer.Position = TimeSpan.FromSeconds(clickedTime);

            _isSeeking = false;

            e.Handled = true;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VideoChartViewModel vm)
            {
                vm.SegmentAccuracyTextBlock = SegmentAccuracyTextBlock;

                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;

                VideoPlayer.Source = vm.VideoSource;
                VideoPlayer.LoadedBehavior = MediaState.Manual;
                VideoPlayer.UnloadedBehavior = MediaState.Manual;

                vm.IsPlaying = true;
                VideoPlayer.Play();

                _timer.Interval = TimeSpan.FromMilliseconds(200);

                _timer.Tick += (s, args) =>
                {
                    if (VideoPlayer == null || DataContext is not VideoChartViewModel vm)
                        return;

                    if (_isSeeking) return;

                    double currentSeconds = VideoPlayer.Position.TotalSeconds;

                    double totalSeconds = vm.SelectedVideo.Length;

                    if (totalSeconds > 0)
                    {
                        vm.CurrentTime = Math.Min(currentSeconds, totalSeconds);
                    }
                };

                _timer.Start();
            }
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoChartViewModel.IsPlaying))
            {
                if (DataContext is VideoChartViewModel vm)
                {
                    if (vm.IsPlaying)
                    {
                        VideoPlayer.Play();
                    }
                    else
                    {
                        VideoPlayer.Pause();
                    }
                }
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= null;

            if (VideoPlayer != null)
            {
                VideoPlayer.Stop();
                VideoPlayer.Close();
                VideoPlayer.Source = null;
            }

            if (DataContext is VideoChartViewModel vm)
            {
                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.IsPlaying = false;
                vm.SegmentAccuracyTextBlock = null;
            }

            EmotionPlotView.PreviewMouseDown -= PlotView_MouseDown_Wpf;
        }
    }
}
