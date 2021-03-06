using Files.Filesystem;
using Files.Helpers;
using Files.Interacts;
using Files.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Files.Views
{
    public sealed partial class Properties : Page
    {
        private static ApplicationViewTitleBar TitleBar;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private ContentDialog propertiesDialog;

        private object navParameterItem;
        private IShellPage AppInstance;

        private ListedItem listedItem;

        public SettingsViewModel AppSettings => App.AppSettings;

        public Properties()
        {
            InitializeComponent();

            //TODO: fix rtl here also
            //var flowDirectionSetting = ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            //if (flowDirectionSetting == "RTL")
            //{
            //    FlowDirection = FlowDirection.RightToLeft;
            //}
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var args = e.Parameter as PropertiesPageNavigationArguments;
            AppInstance = args.AppInstanceArgument;
            navParameterItem = args.Item;
            TabShorcut.Visibility = args.Item is ShortcutItem ? Visibility.Visible : Visibility.Collapsed;
            listedItem = args.Item as ListedItem;
            TabDetails.Visibility = listedItem != null && listedItem.FileExtension != null && !listedItem.IsShortcutItem ? Visibility.Visible : Visibility.Collapsed;
            SetBackground();
            base.OnNavigatedTo(e);
        }

        private void Properties_Loaded(object sender, RoutedEventArgs e)
        {
            AppSettings.ThemeModeChanged += AppSettings_ThemeModeChanged;
            AppSettings.PropertyChanged += AppSettings_PropertyChanged;

            // TODO: Fix this when Windowing is added
            //if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            //{
            //    // Set window size in the loaded event to prevent flickering
            //    ApplicationView.GetForCurrentView().TryResizeView(new Windows.Foundation.Size(400, 550));
            //    ApplicationView.GetForCurrentView().Consolidated += Properties_Consolidated;
            //    TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            //    TitleBar.ButtonBackgroundColor = Colors.Transparent;
            //    TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            //    MainWindow.dispatcherQueue.TryEnqueue(() => AppSettings.UpdateThemeElements.Execute(null));
            //}
            //else
            //{
            propertiesDialog = Interaction.FindParent<ContentDialog>(this);
            propertiesDialog.Closed += PropertiesDialog_Closed;
            //}
        }

        private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsAcrylicDisabled":
                case "FallbackColor":
                case "TintColor":
                case "TintOpacity":
                    SetBackground();
                    break;
            }
        }

        private void SetBackground()
        {
            DispatcherQueue.TryEnqueue(Microsoft.System.DispatcherQueuePriority.Normal, () =>
            {
                var backgroundBrush = new AcrylicBrush()
                {
                    AlwaysUseFallback = AppSettings.IsAcrylicDisabled,
                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                    FallbackColor = AppSettings.AcrylicTheme.FallbackColor,
                    TintColor = AppSettings.AcrylicTheme.TintColor,
                    TintOpacity = AppSettings.AcrylicTheme.TintOpacity,
                };
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 9))
                {
                    backgroundBrush.TintLuminosityOpacity = 0.9;
                }

                if (!(new AccessibilitySettings()).HighContrast)
                {
                    Background = backgroundBrush;
                }
                else
                {
                    Background = Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] as SolidColorBrush;
                }
            });
        }

        private void Properties_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            AppSettings.ThemeModeChanged -= AppSettings_ThemeModeChanged;
            AppSettings.PropertyChanged -= AppSettings_PropertyChanged;
            // TODO: Replace this when windowing support is added
            //ApplicationView.GetForCurrentView().Consolidated -= Properties_Consolidated;
            //if (tokenSource != null && !tokenSource.IsCancellationRequested)
            //{
            //    tokenSource.Cancel();
            //    tokenSource.Dispose();
            //    tokenSource = null;
            //}
        }

        private void PropertiesDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            AppSettings.ThemeModeChanged -= AppSettings_ThemeModeChanged;
            AppSettings.PropertyChanged -= AppSettings_PropertyChanged;
            sender.Closed -= PropertiesDialog_Closed;
            if (tokenSource != null && !tokenSource.IsCancellationRequested)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
                tokenSource = null;
            }
            propertiesDialog.Hide();
        }

        private void Properties_Unloaded(object sender, RoutedEventArgs e)
        {
            // Why is this not called? Are we cleaning up properly?
        }

        private void AppSettings_ThemeModeChanged(object sender, EventArgs e)
        {
            var selectedTheme = ThemeHelper.RootTheme;
            DispatcherQueue.TryEnqueue(Microsoft.System.DispatcherQueuePriority.Normal, () =>
            {
                RequestedTheme = selectedTheme;
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    switch (RequestedTheme)
                    {
                        case ElementTheme.Default:
                            TitleBar.ButtonHoverBackgroundColor = (Color)Application.Current.Resources["SystemBaseLowColor"];
                            TitleBar.ButtonForegroundColor = (Color)Application.Current.Resources["SystemBaseHighColor"];
                            break;

                        case ElementTheme.Light:
                            TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 0, 0, 0);
                            TitleBar.ButtonForegroundColor = Colors.Black;
                            break;

                        case ElementTheme.Dark:
                            TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 255, 255, 255);
                            TitleBar.ButtonForegroundColor = Colors.White;
                            break;
                    }
                }
                SetBackground();
            });
        }

        private async void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (contentFrame.Content is PropertiesGeneral propertiesGeneral)
            {
                await propertiesGeneral.SaveChangesAsync(listedItem);
            }
            else if (contentFrame.Content is PropertiesDetails propertiesDetails)
            {
                if (!await propertiesDetails.SaveChangesAsync())
                {
                    return;
                }
            }
            // TODO: Addsupport for closing alongside the window itself
            //if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            //{
            //    await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            //}
            //else
            //{
            propertiesDialog?.Hide();
            //}
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Addsupport for closing alongside the window itself
            //if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            //{
            //    await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            //}
            //else
            //{
            propertiesDialog?.Hide();
            //}
        }

        private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.Equals(VirtualKey.Escape))
            {
                // TODO: Add support for closing alongside the window itself
                //if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                //{
                //    await ApplicationView.GetForCurrentView().TryConsolidateAsync();
                //}
                //else
                //{
                propertiesDialog?.Hide();
                //}
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var navParam = new PropertyNavParam()
            {
                tokenSource = tokenSource,
                navParameter = navParameterItem,
                AppInstanceArgument = AppInstance
            };

            switch (args.SelectedItemContainer.Tag)
            {
                case "General":
                    contentFrame.Navigate(typeof(PropertiesGeneral), navParam, args.RecommendedNavigationTransitionInfo);
                    break;

                case "Shortcut":
                    contentFrame.Navigate(typeof(PropertiesShortcut), navParam, args.RecommendedNavigationTransitionInfo);
                    break;

                case "Details":
                    contentFrame.Navigate(typeof(PropertiesDetails), navParam, args.RecommendedNavigationTransitionInfo);
                    break;
            }
        }

        public class PropertiesPageNavigationArguments
        {
            public object Item { get; set; }
            public IShellPage AppInstanceArgument { get; set; }
        }

        public class PropertyNavParam
        {
            public CancellationTokenSource tokenSource;
            public object navParameter;
            public IShellPage AppInstanceArgument { get; set; }
        }
    }
}