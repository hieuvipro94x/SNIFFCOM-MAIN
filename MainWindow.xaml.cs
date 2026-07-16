using System.ComponentModel;
using System.Windows;

namespace SniffCom
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private OverlayWindow? _overlayWindow;
        private bool _isExiting;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }

        public void Start(bool forceOverlayMode)
        {
            if (forceOverlayMode || _viewModel.StartInOverlayMode)
                ShowOverlayMode();
            else
                Show();

            _ = _viewModel.InitializeAsync();
        }

        private void ShowOverlay_Click(object sender, RoutedEventArgs e)
            => ShowOverlayMode();

        private void ShowOverlayMode()
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayWindow
                {
                    DataContext = _viewModel
                };
                _overlayWindow.RestoreRequested += OverlayWindow_RestoreRequested;
                _overlayWindow.ExitRequested += OverlayWindow_ExitRequested;
            }

            if (!_overlayWindow.IsVisible)
                _overlayWindow.Show();

            _overlayWindow.PositionTopRight();
            if (IsVisible)
                Hide();
        }

        private void RestoreMainWindow()
        {
            _overlayWindow?.Hide();
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void OverlayWindow_RestoreRequested(object? sender, EventArgs e)
            => RestoreMainWindow();

        private void OverlayWindow_ExitRequested(object? sender, EventArgs e)
            => ExitApplication();

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (!_isExiting && WindowState == WindowState.Minimized && _viewModel.MinimizeToOverlay)
            {
                WindowState = WindowState.Normal;
                ShowOverlayMode();
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_isExiting)
                return;

            e.Cancel = true;
            Dispatcher.BeginInvoke(new Action(ExitApplication));
        }

        private void ExitApplication()
        {
            if (_isExiting)
                return;

            _isExiting = true;
            _viewModel.Shutdown();

            if (_overlayWindow != null)
            {
                _overlayWindow.RestoreRequested -= OverlayWindow_RestoreRequested;
                _overlayWindow.ExitRequested -= OverlayWindow_ExitRequested;
                _overlayWindow.CloseForExit();
                _overlayWindow = null;
            }

            Close();
            System.Windows.Application.Current.Shutdown(0);
        }
    }
}


