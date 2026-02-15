namespace MoodScanAnalyzer.Model
{
    public class VideoModel
    {
        public string FilePath { get; set; }
        public string PredictedEmotions { get; set; }
        public string DisplayName { get; set; } = "";
        public string EmotionAnalyze { get; set; }
        public double Length { get; set; }
    }
}
