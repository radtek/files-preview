using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.System;

namespace Files.Dialogs
{
    public sealed partial class RestartDialog : UserControl
    {
        public RestartDialog()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ShowDialogProperty = DependencyProperty.Register(
          "ShowDialog",
          typeof(bool),
          typeof(RestartDialog),
          new PropertyMetadata(false, new PropertyChangedCallback(OnShowDialogPropertyChanged))
        );

        public bool ShowDialog
        {
            get
            {
                return (bool)GetValue(ShowDialogProperty);
            }
            set
            {
                SetValue(ShowDialogProperty, value);
            }
        }

        private static void OnShowDialogPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var dialog = (RestartDialog)sender;
            if ((bool)e.NewValue)
            {
                dialog.RestartNotification.Show(10000);
            }
            else
            {
                dialog.RestartNotification.Dismiss();
            }
        }

        public void Show()
        {
            RestartNotification.Show(10000);
        }

        public void Dismiss()
        {
            RestartNotification.Dismiss();
        }

        private async void YesButton_Click(object sender, RoutedEventArgs e)
        {
            App.AppSettings.ResumeAfterRestart = true;
            App.SaveSessionTabs();
            await Launcher.LaunchUriAsync(new Uri("files-uwp:"));
            Process.GetCurrentProcess().Kill();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            RestartNotification.Dismiss();
        }
    }
}