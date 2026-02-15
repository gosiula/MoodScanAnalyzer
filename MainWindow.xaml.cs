using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MoodScanAnalyzer
{
    public partial class MainWindow : Window
    {
        public event Action<bool>? WindowStateChangedEvent;

        public MainWindow()
        {
            InitializeComponent();
            this.StateChanged += MainWindow_StateChanged;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        // Methods for moving the application window
        private void PnlControlBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            SendMessage(helper.Handle, 161, 2, 0);
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Method to minimize the application window
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Method to change border thickness (depending on whether the window is maximized)
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            bool isMaximized = this.WindowState == WindowState.Maximized;
            WindowStateChangedEvent?.Invoke(isMaximized);

            if (MainBorder != null)
            {
                MainBorder.BorderThickness = isMaximized
                    ? new Thickness(10)
                    : new Thickness(3);
            }
        }

        // Method to set application window to maximized or normal
        private void BtnFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        // Method for closing the application window
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}