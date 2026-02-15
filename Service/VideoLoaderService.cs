using System.Globalization;
using System.IO;
using MoodScanAnalyzer.Model;

namespace MoodScanAnalyzer.Service
{
    public class VideoLoaderService
    {
        // Method to load video from .csv 
        public VideoModel LoadSingleVideoFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV file was not found: {csvPath}.");

            var lines = File.ReadAllLines(csvPath);

            if (lines.Length < 2)
                throw new InvalidDataException("CSV must contain a header and at least one data row.");

            // We skip the header (line 0) and read the first line of data (line 1)
            string dataLine = lines[1].Trim();
            if (string.IsNullOrWhiteSpace(dataLine))
                throw new InvalidDataException("The data row in the CSV file is empty.");

            // Separate data by a separator (semicolon)
            string[] columns = dataLine.Split(';');

            if (columns.Length < 1 || string.IsNullOrWhiteSpace(columns[0]))
                throw new InvalidDataException("The first column (video path) is empty.");

            // The first column is the path to the video
            string relativeVideoPath = columns[0].Trim();

            // The second column is the list of predicted emotions
            string predictedEmotions = columns.Length > 1 ? columns[1].Trim() : string.Empty;

            // The third column is the video duration
            string lengthStr = columns.Length > 2 ? columns[2].Trim() : "0";
            lengthStr = lengthStr.Replace(',', '.');

            double length = 0d;
            if (!double.TryParse(lengthStr, NumberStyles.Float, CultureInfo.InvariantCulture, out length))
                length = 0d;

            // Path to Video
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string videosFolder = Path.Combine(basePath, "Video");
            string fullVideoPath = Path.Combine(videosFolder, relativeVideoPath);

            if (!File.Exists(fullVideoPath))
            {
                string devRoot = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\.."));
                videosFolder = Path.Combine(devRoot, "Video");
                fullVideoPath = Path.Combine(videosFolder, relativeVideoPath);
            }

            if (!File.Exists(fullVideoPath))
                throw new FileNotFoundException($"Video file was not found: {relativeVideoPath}\nSearched: {fullVideoPath}");

            return new VideoModel
            {
                FilePath = relativeVideoPath,
                PredictedEmotions = predictedEmotions,
                DisplayName = Path.GetFileName(relativeVideoPath),
                Length = length,
            };
        }
    }
}
