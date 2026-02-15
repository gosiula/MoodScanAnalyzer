using System.Diagnostics;
using System.Globalization;
using System.IO;
using MoodScanAnalyzer.Model;

namespace MoodScanAnalyzer.Service
{

    public class VideoChartService
    {
        private readonly string _userFolderPath;

        public VideoChartService()
        {
            _userFolderPath = "Users";
        }

        // Method to retrieve users who have watched a selected video
        public List<string> GetUsersWhoWatchedVideo(string videoFileName)
        {
            Debug.WriteLine(videoFileName);
            if (!Directory.Exists(_userFolderPath))
                return new List<string>();

            Debug.WriteLine(_userFolderPath);

            var userFolders = Directory.GetDirectories(_userFolderPath)
                                       .Select(Path.GetFileName)
                                       .OrderBy(name => name)
                                       .ToList();

            var usersWhoWatched = new List<string>();

            foreach (var folder in userFolders)
            {
                var csvFiles = Directory.GetFiles(Path.Combine(_userFolderPath, folder), "*.csv");
                foreach (var csvFile in csvFiles)
                {
                    var lines = File.ReadAllLines(csvFile);
                    if (lines.Length <= 1)
                        continue;

                    var dataLines = lines.Skip(1).ToList();
                    if (dataLines.Count == 0)
                        continue;

                    // Check if the user has an entry for this video anywhere in the CSV
                    if (dataLines.Any(l =>
                        l.Split(';')[2].Trim().Equals(videoFileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        usersWhoWatched.Add(folder);
                        break;
                    }
                }
            }


            return usersWhoWatched;
        }

        // Method that returns a list (Time, Emotion) for the selected user and video
        public List<(double Time, string Emotion)> GetUserEmotionsForVideo(string userFolderName, string videoFileName, double videoLength)
        {
            var results = new List<(double Time, string Emotion)>();
            var userFolder = Path.Combine(_userFolderPath, userFolderName);
            if (!Directory.Exists(userFolder)) return results;

            var csvFiles = Directory.GetFiles(userFolder, "*.csv");
            if (csvFiles.Length == 0) return results;

            var csvFile = csvFiles[0];

            var lines = File.ReadAllLines(csvFile)
                .Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(';'))
                .Where(parts =>
                    parts.Length > 2 &&
                    parts[2].Trim().Equals(videoFileName, StringComparison.OrdinalIgnoreCase))
                .Select(parts => new
                {
                    Time = double.TryParse(parts[3].Replace(',', '.'), NumberStyles.Any,
                                           CultureInfo.InvariantCulture, out var t) ? t : 0,
                    Emotion = parts[1].Trim().ToLower()
                })
                .Where(x => x.Time <= videoLength)
                .OrderBy(x => x.Time)
                .ToList();

            foreach (var x in lines)
                results.Add((x.Time, x.Emotion));

            return results;
        }

        // Method for calculating the percentage of satisfaction of the current segment in terms of emotions
        public List<double> CalculateSegmentAccuracyPerGroup(VideoModel video, List<(double Start, double End, string Emotion)> activeSegments)
        {
            if (activeSegments == null || !activeSegments.Any())
                return new List<double>();

            string usersFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Users");

            if (string.IsNullOrWhiteSpace(usersFolderPath) || !Directory.Exists(usersFolderPath))
                return new List<double>();

            List<string> userFiles;
            try
            {
                userFiles = Directory.GetDirectories(usersFolderPath)
                                     .Select(d => Directory.GetFiles(d, "*.csv").FirstOrDefault())
                                     .Where(f => !string.IsNullOrWhiteSpace(f))
                                     .ToList();
            }
            catch (IOException)
            {
                return new List<double>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<double>();
            }

            if (!userFiles.Any()) return new List<double>();

            // Grouping segments by identical interval
            var groupedSegments = activeSegments
                .GroupBy(s => (s.Start, s.End))
                .ToList();

            var groupResults = new List<double>();

            foreach (var group in groupedSegments)
            {
                var allUsersScores = new List<double>();

                foreach (var file in userFiles)
                {
                    string userName = Path.GetFileName(Path.GetDirectoryName(file));
                    var userLines = File.ReadAllLines(file).Skip(1).ToList();
                    var userLinesForVideo = userLines
                        .Where(l => l.Split(';')[2].Trim() == Path.GetFileName(video.FilePath))
                        .ToList();
                    if (!userLinesForVideo.Any()) continue;

                    var userEvents = new List<(double Start, double End, string Emotion)>();
                    double lastTime = 0;
                    string lastEmotion = null;

                    foreach (var line in userLinesForVideo)
                    {
                        var parts = line.Split(';');
                        if (parts.Length < 4) continue;

                        string emotion = parts[1].Trim().ToLower();
                        if (double.TryParse(parts[3].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double videoElapsed))
                        {
                            if (lastEmotion != null)
                                userEvents.Add((lastTime, videoElapsed, lastEmotion));

                            lastTime = videoElapsed;
                            lastEmotion = emotion;
                        }
                    }

                    if (lastEmotion != null)
                        userEvents.Add((lastTime, video.Length, lastEmotion));

                    double userScoreForGroup;

                    if (group.Count() > 1)
                    {
                        // It is enough that any segment is met (OR)
                        bool hitAny = group.Any(seg =>
                            userEvents.Any(ev =>
                                ev.Emotion == seg.Emotion &&
                                ev.End >= seg.Start &&
                                ev.Start <= seg.End));

                        userScoreForGroup = hitAny ? 1.0 : 0.0;
                    }
                    else
                    {
                        // Single segment
                        var seg = group.First();
                        bool hit = userEvents.Any(ev =>
                            ev.Emotion == seg.Emotion &&
                            ev.End >= seg.Start &&
                            ev.Start <= seg.End);

                        userScoreForGroup = hit ? 1.0 : 0.0;
                    }

                    allUsersScores.Add(userScoreForGroup);
                }

                double groupScore = allUsersScores.Any() ? allUsersScores.Average() * 100.0 : 0.0;
                groupResults.Add(groupScore);
            }

            return groupResults;
        }
    }

}