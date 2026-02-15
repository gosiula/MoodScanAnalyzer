using System.ComponentModel;

namespace MoodScanAnalyzer.Model
{
    public class UserItemModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FolderName { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
