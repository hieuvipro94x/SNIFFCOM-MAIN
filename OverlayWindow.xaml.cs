using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace SniffCom
{
    public partial class OverlayWindow : Window
    {
        private bool _allowClose;
        public event EventHandler? RestoreRequested;
        public event EventHandler? ExitRequested;

        public OverlayWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => PositionTopRight();
        }

        public void CloseForExit()
        {
            _allowClose = true;
            Close();
        }

        public void PositionTopRight()
        {
            Rect workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, workArea.Right - ActualWidth - 16);
            Top = workArea.Top + 16;
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
                RestoreRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_allowClose)
                return;

            e.Cancel = true;
            Hide();
            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OpenApplication_Click(object sender, RoutedEventArgs e)
            => RestoreRequested?.Invoke(this, EventArgs.Empty);

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
            => ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
