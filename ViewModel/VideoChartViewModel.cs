using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MoodScanAnalyzer.Model;
using MoodScanAnalyzer.Service;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace MoodScanAnalyzer.ViewModel
{
    public class VideoChartViewModel : BaseViewModel
    {
        // Services
        private readonly VideoChartService _videoChartService;
        private readonly VideoLoaderService _videoLoaderService;
        private readonly MainViewModel _mainViewModel;

        // Bool - is window maximized
        private bool _isMaximized;

        // Selected video
        private VideoModel _selectedVideo;
        public VideoModel SelectedVideo
        {
            get => _selectedVideo;
            set
            {
                _selectedVideo = value;
                OnPropertyChanged(nameof(SelectedVideo));

                if (_selectedVideo != null && !string.IsNullOrEmpty(_selectedVideo.FilePath))
                {
                    string fullPath = GetFullVideoPath(_selectedVideo.FilePath);

                    if (fullPath != null)
                    {
                        VideoSource = new Uri(fullPath, UriKind.Absolute);
                    }
                    else
                    {
                        VideoSource = null;
                    }
                }
            }
        }

        // Emoji folder
        readonly string emojiFolder = Path.Combine("Images");

        // Fulfillment of the current segment
        public ObservableCollection<Inline> CurrentSegmentAccuracy { get; set; }
        public TextBlock SegmentAccuracyTextBlock { get; set; }

        // Bool - is the video playing
        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                }
            }
        }

        // Video source
        private Uri _videoSource;
        public Uri VideoSource
        {
            get => _videoSource;
            set
            {
                _videoSource = value;
                OnPropertyChanged(nameof(VideoSource));
            }
        }

        // Current video time
        private double _currentTime = 0.0;
        public double CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
                UpdateTimeLine(_currentTime);
            }
        }

        // Timers
        private DispatcherTimer _timer;
        private LineAnnotation _timeLine;

        // Predicted emotions
        private List<(LineAnnotation annotation, double Start, double End, string Emotion)> _predictedAnnotations = new List<(LineAnnotation, double, double, string)>();

        // Users who watched the video
        public ObservableCollection<UserItemModel> Users { get; } = new ObservableCollection<UserItemModel>();

        // Plot
        public PlotModel EmotionPlotModel { get; private set; }
        private Dictionary<(string userName, double time), string> _emotionMap = new Dictionary<(string, double), string>();
        private readonly OxyColor[] _emotionColors = new[]
        {
            OxyColor.Parse("#FF6DC7"),
            OxyColor.Parse("#B4A7FF"),
            OxyColor.Parse("#84F5FF"),
            OxyColor.Parse("#ACFF9C"),
            OxyColor.Parse("#FCFF9A"),
            OxyColor.Parse("#FFCC8E"),
            OxyColor.Parse("#FF9191")
        };
        private Dictionary<(double Start, double End), OxyColor> _rangeColorMap = new Dictionary<(double, double), OxyColor>();

        // Commands
        public ICommand TogglePlayPauseCommand { get; }

        public VideoChartViewModel(MainViewModel mainViewModel, VideoChartService videoChartService, VideoLoaderService videoLoaderService)
        {
            _mainViewModel = mainViewModel;
            _videoChartService = videoChartService;
            _videoLoaderService = videoLoaderService;

            // Loading .csv file with video info
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string csvPath = Path.Combine(basePath, "Video", "VideoLabels.csv");
            if (!File.Exists(csvPath))
            {
                string devRoot = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\.."));
                csvPath = Path.Combine(devRoot, "Video", "VideoLabels.csv");
            }

            try
            {
                SelectedVideo = _videoLoaderService.LoadSingleVideoFromCsv(csvPath);
            }
            catch
            {
                SelectedVideo = null;
                VideoSource = null;
                return;
            }

            // Analyzing the emotions
            AnalyzeVideoEmotions(SelectedVideo, "Users");

            var window = (MainWindow)Application.Current.MainWindow;
            window.WindowStateChangedEvent += OnWindowStateChanged;

            CurrentSegmentAccuracy = new ObservableCollection<Inline>();

            TogglePlayPauseCommand = new CommandViewModel(TogglePlayPause);

            // Users who watched the video
            var usersFromService = _videoChartService.GetUsersWhoWatchedVideo(SelectedVideo.DisplayName);

            // Selecting first 5 users to display their emotions on the plot
            for (int i = 0; i < usersFromService.Count; i++)
            {
                Users.Add(new UserItemModel
                {
                    FolderName = usersFromService[i],
                    Name = $"Użytkownik {usersFromService[i].Replace("User", "")}",
                    IsSelected = i < 5
                });
            }

            // Building the plot
            foreach (var user in Users)
            {
                user.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(UserItemModel.IsSelected))
                    {
                        if (Users.Count(u => u.IsSelected) > 5)
                        {
                            user.IsSelected = false;
                            return;
                        }
                        BuildEmotionPlot();
                    }
                };
            }

            BuildEmotionPlot();
        }

        // Method for analyzing data and emotions for video
        public void AnalyzeVideoEmotions(VideoModel video, string usersFolderPath)
        {
            if (video == null)
                return;

            if (string.IsNullOrWhiteSpace(usersFolderPath) || !Directory.Exists(usersFolderPath))
            {
                video.EmotionAnalyze = "Brak danych.";
                return;
            }

            var userFiles = Directory.GetDirectories(usersFolderPath)
                                     .Select(d => Directory.GetFiles(d, "*.csv").FirstOrDefault())
                                     .Where(f => f != null)
                                     .ToList();

            var predictedRanges = ParsePredictedEmotions(video.PredictedEmotions);

            if (!userFiles.Any())
            {
                video.EmotionAnalyze = "Brak danych.";
                return;
            }

            if (!userFiles.Any())
            {
                video.EmotionAnalyze = "Brak danych.";
            }

            var allUsersResults = new List<double>();
            var userLikes = new List<int>();
            var userWatchPercents = new List<double>();

            foreach (var file in userFiles)
            {
                string userName = Path.GetFileName(Path.GetDirectoryName(file));
                var userLines = File.ReadAllLines(file).Skip(1).ToList();

                var userLinesForVideo = userLines.Where(l => l.Split(';')[2].Trim() == Path.GetFileName(video.FilePath)).ToList();
                if (!userLinesForVideo.Any())
                    continue;

                var userEvents = new List<(double Start, double End, string Emotion)>();
                double lastTime = 0;
                string lastEmotion = null;

                double watchTime = 0;

                foreach (var line in userLines)
                {
                    var parts = line.Split(';');
                    if (parts.Length < 5) continue;

                    string emotion = parts[1].Trim().ToLower();
                    string videoFile = parts[2].Trim();
                    if (videoFile != Path.GetFileName(video.FilePath)) continue;

                    if (double.TryParse(parts[3].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double videoElapsed))
                    {
                        if (lastEmotion != null)
                            userEvents.Add((lastTime, videoElapsed, lastEmotion));

                        lastTime = videoElapsed;
                        lastEmotion = emotion;
                        watchTime = videoElapsed;
                    }

                }

                if (lastEmotion != null)
                {
                    double endTime = Math.Min(video.Length, watchTime);
                    userEvents.Add((lastTime, endTime, lastEmotion));
                }

                watchTime = video.Length;

                var groups = GroupOverlappingRanges(predictedRanges);
                int matchedGroups = 0;

                foreach (var group in groups)
                {
                    bool groupMatched = group.Any(predicted =>
                        userEvents.Any(e =>
                            e.End >= predicted.Start &&
                            e.Start <= predicted.End &&
                            e.Emotion == predicted.Emotion
                        )
                    );

                    if (groupMatched)
                        matchedGroups += 1;
                }

                double userScore = (groups.Count > 0) ? (double)matchedGroups / groups.Count : 0.0;
                allUsersResults.Add(userScore);

                double watchPercent = (video.Length > 0) ? Math.Min((watchTime / video.Length) * 100, 100) : 0;
                userWatchPercents.Add(watchPercent);
            }

            video.EmotionAnalyze = allUsersResults.Any() ? $"{Math.Round(allUsersResults.Average() * 100)}%" : "Brak danych.";

            if (userWatchPercents.Any() || userLikes.Any() || allUsersResults.Any())
            {
                double avgWatchPercent = userWatchPercents.Any() ? userWatchPercents.Average() : 0;
                double avgLikePercent = userLikes.Any() ? userLikes.Average() * 100 : 0;
                double avgEmotionPercent = allUsersResults.Any() ? allUsersResults.Average() * 100 : 0;
            }
        }

        // Method for grouping emotion-based intervals for video
        private List<List<(string Emotion, double Start, double End)>> GroupOverlappingRanges(List<(string Emotion, double Start, double End)> ranges)
        {
            // Group only by time range, ignoring emotion
            var grouped = ranges
                .GroupBy(r => new { r.Start, r.End })
                .Select(g => g.ToList())
                .ToList();

            return grouped;
        }

        // Method for parsing assumed emotions
        private List<(string Emotion, double Start, double End)> ParsePredictedEmotions(string predicted)
        {
            var list = new List<(string, double, double)>();
            if (string.IsNullOrWhiteSpace(predicted))
                return list;

            var items = predicted.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                int startIdx = item.IndexOf('(');
                int endIdx = item.IndexOf(')');
                if (startIdx < 0 || endIdx < 0) continue;

                string emotion = item.Substring(0, startIdx).Trim().ToLower();
                string range = item.Substring(startIdx + 1, endIdx - startIdx - 1);
                var parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double start) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double end))
                {
                    list.Add((emotion, start, end));
                }
            }
            return list;
        }

        // Method to change the size of emojis if the window is maximized
        private void OnWindowStateChanged(bool isMaximized)
        {
            _isMaximized = isMaximized;
            UpdateEmojiSizes();
        }

        // Method to play and pause the video
        private void TogglePlayPause(object obj)
        {
            IsPlaying = !IsPlaying;
        }

        // Method to get full video path
        private string GetFullVideoPath(string filePath)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string videosFolder = Path.Combine(basePath, "Video");
            string fullPath = Path.Combine(videosFolder, filePath);

            if (!File.Exists(fullPath))
            {
                string devRoot = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\.."));
                videosFolder = Path.Combine(devRoot, "Video");
                fullPath = Path.Combine(videosFolder, filePath);
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

        // Method to generate offsets for user plots
        private List<double> GenerateOffsets(int n, double step = 0.1)
        {
            var offsets = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double pos = (i + 1) / 2 * step;
                offsets.Add(i % 2 == 0 ? (i == 0 ? 0 : -pos) : pos);
            }
            return offsets;
        }

        // Method for drawing a plot
        private void BuildEmotionPlot()
        {
            _emotionMap.Clear();
            _rangeColorMap.Clear();

            var emotions = new[] { "angry", "scared", "sad", "confused", "neutral", "surprised", "happy" };


            List<string> labelsPolish = new List<string>
            {
                "złość", "przestraszenie", "smutek", "zniesmaczenie", "neutralność",  "zaskoczenie", "radość"
            };

            var model = new PlotModel
            {
                Title = $"Porównanie emocji użytkowników w czasie",
                IsLegendVisible = true,
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                TitleFontSize = 14,
                PlotAreaBorderColor = OxyColor.FromAColor(60, OxyColors.White)
            };

            model.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendBorderThickness = 0,
                LegendItemSpacing = 14,
                LegendFontSize = 10
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = -2,
                Maximum = SelectedVideo.Length + 2,
                Title = "Czas [s]",
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.White,
                TextColor = OxyColors.White,
                TicklineColor = OxyColors.White,
                TitleColor = OxyColors.White,
                MajorGridlineColor = OxyColor.FromAColor(60, OxyColors.White),
                MinorGridlineColor = OxyColor.FromAColor(40, OxyColors.White)
            });

            model.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                Key = "EmotionAxis",
                ItemsSource = labelsPolish,
                MajorStep = 1,
                MinorStep = 1,
                GapWidth = 0.3,
                Minimum = -0.5,
                Maximum = emotions.Length - 0.5,
                AxislineColor = OxyColors.White,
                TextColor = OxyColors.White,
                TicklineColor = OxyColors.White,
                TitleColor = OxyColors.White
            });

            // Parsing Assumed Emotions
            var predictedRanges = ParsePredictedEmotions(SelectedVideo.PredictedEmotions);

            if (predictedRanges.Any())
            {
                var legendSeries = new LineSeries
                {
                    Title = "Założenia emocji",
                    Color = OxyColor.Parse("#FF1FA9"),
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Dash
                };
                legendSeries.Points.Add(new DataPoint(double.NaN, double.NaN));
                model.Series.Add(legendSeries);
            }

            // Sort all intervals chronologically
            var sortedRanges = predictedRanges
                .OrderBy(r => r.Start)
                .ThenBy(r => r.End)
                .ToList();

            // Color assignment algorithm:
            // - We go through the intervals chronologically
            // - For each interval, we check which colors are already occupied by overlapping intervals
            // - We assign the first available color

            var assignedColors = new Dictionary<(double Start, double End), int>(); // Mapping to color index

            foreach (var range in sortedRanges)
            {
                var key = (range.Start, range.End);

                // If already assigned (the same range can occur for different emotions), skip
                if (assignedColors.ContainsKey(key))
                    continue;

                // Find all intervals that overlap with the current one
                var overlappingRanges = assignedColors.Keys
                    .Where(r => r.Start < range.End && r.End > range.Start)
                    .ToList();

                // Find the colors used by overlapping intervals
                var usedColorIndices = overlappingRanges
                    .Select(r => assignedColors[r])
                    .ToHashSet();

                // Find the first free color (smallest index not used)
                int colorIndex = 0;
                while (usedColorIndices.Contains(colorIndex))
                {
                    colorIndex++;
                }

                assignedColors[key] = colorIndex;
            }

            // Map the color indices to the actual OxyColor colors
            foreach (var kvp in assignedColors)
            {
                _rangeColorMap[kvp.Key] = _emotionColors[kvp.Value % _emotionColors.Length];
            }

            _predictedAnnotations.Clear();

            // Add annotations
            foreach (var range in predictedRanges)
            {
                int emotionIndex = Array.IndexOf(emotions, range.Emotion);
                if (emotionIndex < 0) continue;

                double y = emotionIndex + 0.05;
                var timeKey = (range.Start, range.End);
                var groupColor = _rangeColorMap[timeKey];

                var ann = new LineAnnotation
                {
                    Type = LineAnnotationType.Horizontal,
                    Color = groupColor,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Dash,
                    Y = y,
                    MinimumX = range.Start,
                    MaximumX = range.End
                };

                model.Annotations.Add(ann);
                _predictedAnnotations.Add((ann, range.Start, range.End, range.Emotion));
            }

            var colors = new[]
            {
                OxyColor.Parse("#0BADD5"),
                OxyColor.Parse("#02F296"),
                OxyColor.Parse("#672EC5"),
                OxyColor.Parse("#E6DB11"),
                OxyColor.Parse("#A42DC4")
            };

            var selectedUsers = Users.Where(u => u.IsSelected).ToList();
            var offsets = GenerateOffsets(selectedUsers.Count);

            int idx = 0;

            // Drawing charts for users
            foreach (var user in selectedUsers)
            {
                var userData = _videoChartService.GetUserEmotionsForVideo(user.FolderName, SelectedVideo.DisplayName, SelectedVideo.Length);
                if (userData.Count == 0) { idx++; continue; }

                var color = colors[idx % colors.Length];
                double offset = offsets[idx];

                var stepSeries = new StairStepSeries
                {
                    Title = user.Name,
                    Color = color,
                    StrokeThickness = 2
                };

                string lastEmotion = null;
                double lastTime = 0;

                foreach (var current in userData)
                {
                    bool isIgnoredEmotion = current.Emotion == "too_many_faces" || current.Emotion == "no_face" || current.Emotion == "start" || current.Emotion == "stop";

                    if (emotions.Contains(current.Emotion) && !isIgnoredEmotion)
                    {
                        int emotionIndex = Array.IndexOf(emotions, current.Emotion);
                        double visualY = emotionIndex + offset;

                        if (lastEmotion != null)
                        {
                            int lastIndex = Array.IndexOf(emotions, lastEmotion);
                            double lastVisualY = lastIndex + offset;

                            stepSeries.Points.Add(new DataPoint(lastTime, lastVisualY));
                            stepSeries.Points.Add(new DataPoint(current.Time, lastVisualY));

                            _emotionMap[(user.Name, lastTime)] = lastEmotion;
                            _emotionMap[(user.Name, current.Time)] = lastEmotion;

                            AddEmojiImage(model, lastEmotion, (lastTime + current.Time) / 2.0, lastVisualY);
                        }

                        lastEmotion = current.Emotion;
                        lastTime = current.Time;
                    }
                }

                if (lastEmotion != null)
                {
                    int lastIndex = Array.IndexOf(emotions, lastEmotion);
                    double lastVisualY = lastIndex + offset;

                    stepSeries.Points.Add(new DataPoint(lastTime, lastVisualY));
                    stepSeries.Points.Add(new DataPoint(SelectedVideo.Length, lastVisualY));

                    _emotionMap[(user.Name, lastTime)] = lastEmotion;
                    _emotionMap[(user.Name, SelectedVideo.Length)] = lastEmotion;

                    AddEmojiImage(model, lastEmotion, (lastTime + SelectedVideo.Length) / 2.0, lastVisualY);
                }

                model.Series.Add(stepSeries);
                idx++;
            }

            // Vertical video timeline
            _timeLine = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                Color = OxyColor.FromArgb(128, 255, 255, 255),
                X = 0,
                LineStyle = LineStyle.Solid,
                StrokeThickness = 5
            };
            model.Annotations.Add(_timeLine);

            EmotionPlotModel = model;
            OnPropertyChanged(nameof(EmotionPlotModel));
        }

        // Method to update the timeline
        private void UpdateTimeLine(double currentTime)
        {
            if (_timeLine != null)
            {
                _timeLine.X = currentTime;

                // Active segments at the moment
                var activeSegments = _predictedAnnotations
                    .Where(p => currentTime >= p.Start && currentTime <= p.End)
                    .Select(p => (p.Start, p.End, p.Emotion))
                    .ToList();

                // Color and bold update
                foreach (var (ann, start, end, emotion) in _predictedAnnotations)
                {
                    var timeKey = (start, end);
                    bool isActive = currentTime >= start && currentTime <= end;

                    if (isActive && _rangeColorMap.ContainsKey(timeKey))
                    {
                        ann.Color = _rangeColorMap[timeKey]; // Use the same color as for construction
                        ann.StrokeThickness = 4;
                    }
                    else
                    {
                        ann.Color = OxyColor.Parse("#FF1FA9"); // Pink for the inactive
                        ann.StrokeThickness = 2;
                    }
                }

                // TextBlock Update
                CurrentSegmentAccuracy.Clear();

                if (activeSegments.Any())
                {
                    var results = _videoChartService.CalculateSegmentAccuracyPerGroup(SelectedVideo, activeSegments);

                    for (int i = 0; i < results.Count; i++)
                    {
                        var value = Math.Round(results[i]);
                        var timeKey = (activeSegments[i].Start, activeSegments[i].End);
                        var color = _rangeColorMap.ContainsKey(timeKey)
                            ? _rangeColorMap[timeKey]
                            : OxyColor.Parse("#FF1FA9");

                        CurrentSegmentAccuracy.Add(new Run($"{value}%")
                        {
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B))
                        });

                        if (i < results.Count - 1)
                        {
                            CurrentSegmentAccuracy.Add(new Run(", ")
                            {
                                FontWeight = System.Windows.FontWeights.Bold,
                                Foreground = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B))
                            });
                        }

                        if ((i - 1) % 4 == 1)
                        {
                            CurrentSegmentAccuracy.Add(new LineBreak());
                        }
                    }

                    if (results.Count % 4 == 1)
                        CurrentSegmentAccuracy.Add(new LineBreak());
                }
                else
                {
                    CurrentSegmentAccuracy.Add(new Run("Brak")
                    {
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6D, 0xC7))
                    });
                }

                if (SegmentAccuracyTextBlock != null)
                {
                    SegmentAccuracyTextBlock.Inlines.Clear();
                    foreach (var inline in CurrentSegmentAccuracy)
                        SegmentAccuracyTextBlock.Inlines.Add(inline);
                }

                EmotionPlotModel.InvalidatePlot(true);
            }
        }

        // Method to update emojis sizes
        private void UpdateEmojiSizes()
        {
            if (EmotionPlotModel == null) return;

            double newHeightRel = _isMaximized ? 0.09 : 0.11;
            double newWidthRel = 0.07;

            foreach (var ann in EmotionPlotModel.Annotations.OfType<ImageAnnotation>())
            {
                ann.Width = new PlotLength(newWidthRel, PlotLengthUnit.RelativeToPlotArea);
                ann.Height = new PlotLength(newHeightRel, PlotLengthUnit.RelativeToPlotArea);
            }

            EmotionPlotModel.InvalidatePlot(false);
        }

        // Method to draw emojis
        private void AddEmojiImage(PlotModel model, string emotion, double time, double yPosition)
        {
            string imgPath = Path.Combine(emojiFolder, $"{emotion}.png");
            if (!File.Exists(imgPath)) return;

            try
            {
                var bmp = new OxyImage(File.ReadAllBytes(imgPath));

                double widthRel = 0.07;
                double heightRel = _isMaximized ? 0.09 : 0.11;

                model.Annotations.Add(new ImageAnnotation
                {
                    ImageSource = bmp,
                    X = new PlotLength(time, PlotLengthUnit.Data),
                    Y = new PlotLength(yPosition, PlotLengthUnit.Data),
                    Width = new PlotLength(widthRel, PlotLengthUnit.RelativeToPlotArea),
                    Height = new PlotLength(heightRel, PlotLengthUnit.RelativeToPlotArea),
                    HorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                    VerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                    Interpolate = false
                });
            }
            catch (System.Exception ex)
            {
            }
        }
    }
}
