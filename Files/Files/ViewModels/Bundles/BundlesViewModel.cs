using Files.Dialogs;
using Files.Helpers;
using Files.SettingsInterfaces;
using Files.ViewModels.Dialogs;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Files.ViewModels.Bundles
{
    /// <summary>
    /// Bundles list View Model
    /// </summary>
    public class BundlesViewModel : ObservableObject, IDisposable
    {
        #region Singleton

        private IBundlesSettings BundlesSettings => App.BundlesSettings;

        #endregion Singleton

        #region Private Members

        private IShellPage associatedInstance;

        #endregion Private Members

        #region Public Properties

        /// <summary>
        /// Collection of all bundles
        /// </summary>
        public ObservableCollection<BundleContainerViewModel> Items { get; set; } = new ObservableCollection<BundleContainerViewModel>();

        private string bundleNameTextInput = string.Empty;

        public string BundleNameTextInput
        {
            get => bundleNameTextInput;
            set => SetProperty(ref bundleNameTextInput, value);
        }

        private string addBundleErrorText = string.Empty;

        public string AddBundleErrorText
        {
            get => addBundleErrorText;
            set => SetProperty(ref addBundleErrorText, value);
        }

        public bool noBundlesAddItemLoad = false;

        public bool NoBundlesAddItemLoad
        {
            get => noBundlesAddItemLoad;
            set => SetProperty(ref noBundlesAddItemLoad, value);
        }

        #endregion Public Properties

        #region Commands

        public ICommand InputTextKeyDownCommand { get; private set; }

        public ICommand OpenAddBundleDialogCommand { get; private set; }

        public ICommand AddBundleCommand { get; private set; }

        public ICommand ImportBundlesCommand { get; private set; }

        public ICommand ExportBundlesCommand { get; private set; }

        #endregion Commands

        #region Constructor

        public BundlesViewModel()
        {
            // Create commands
            InputTextKeyDownCommand = new RelayCommand<KeyRoutedEventArgs>(InputTextKeyDown);
            OpenAddBundleDialogCommand = new RelayCommand(OpenAddBundleDialog);
            AddBundleCommand = new RelayCommand(() => AddBundle(BundleNameTextInput));
            ImportBundlesCommand = new RelayCommand(ImportBundles);
            ExportBundlesCommand = new RelayCommand(ExportBundles);
        }

        #endregion Constructor

        #region Command Implementation

        private void InputTextKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                AddBundle(BundleNameTextInput);
                e.Handled = true;
            }
        }

        private async void OpenAddBundleDialog()
        {
            DynamicDialog dialog = new DynamicDialog(new DynamicDialogViewModel()
            {
                DisplayControl = new TextBox()
                {
                    PlaceholderText = "BundlesWidgetAddBundleInputPlaceholderText".GetLocalized()
                },
                TitleText = "BundlesWidgetCreateBundleDialogTitleText".GetLocalized(),
                SubtitleText = "BundlesWidgetCreateBundleDialogSubtitleText".GetLocalized(),
                PrimaryButtonText = "BundlesWidgetCreateBundleDialogPrimaryButtonText".GetLocalized(),
                CloseButtonText = "BundlesWidgetCreateBundleDialogCloseButtonText".GetLocalized(),
                PrimaryButtonAction = (vm, e) =>
                {
                    AddBundle((vm.DisplayControl as TextBox).Text);
                },
                CloseButtonAction = (vm, e) =>
                {
                    vm.HideDialog();
                },
                KeyDownAction = (vm, e) =>
                {
                    if (e.Key == VirtualKey.Enter)
                    {
                        AddBundle((vm.DisplayControl as TextBox).Text);
                    }
                    else if (e.Key == VirtualKey.Escape)
                    {
                        vm.HideDialog();
                    }
                },
                DynamicButtons = DynamicButtons.Primary | DynamicButtons.Cancel
            });
            await dialog.ShowAsync();
        }

        private void AddBundle(string name)
        {
            if (!CanAddBundle(name))
            {
                return;
            }

            string savedBundleNameTextInput = name;
            BundleNameTextInput = string.Empty;

            if (BundlesSettings.SavedBundles == null || (BundlesSettings.SavedBundles?.ContainsKey(savedBundleNameTextInput) ?? false)) // Init
            {
                BundlesSettings.SavedBundles = new Dictionary<string, List<string>>()
                {
                    { savedBundleNameTextInput, new List<string>() { null } }
                };
            }

            Items.Add(new BundleContainerViewModel(associatedInstance)
            {
                BundleName = savedBundleNameTextInput,
                NotifyItemRemoved = NotifyItemRemovedHandle,
            });
            NoBundlesAddItemLoad = false;

            // Save bundles
            Save();
        }

        private async void ImportBundles()
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(System.IO.Path.GetExtension(Constants.LocalSettings.BundlesSettingsFileName));

            StorageFile file = await filePicker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    string data = NativeFileOperationsHelper.ReadStringFromFile(file.Path);
                    var deserialized = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(data);
                    BundlesSettings.ImportSettings(JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(data));
                    await Load(); // Update the collection
                }
                catch // Couldn't deserialize, data is corrupted
                {
                }
            }
        }

        private async void ExportBundles()
        {
            FileSavePicker filePicker = new FileSavePicker();
            filePicker.FileTypeChoices.Add("Json File", new List<string>() { System.IO.Path.GetExtension(Constants.LocalSettings.BundlesSettingsFileName) });

            StorageFile file = await filePicker.PickSaveFileAsync();

            if (file != null)
            {
                NativeFileOperationsHelper.WriteStringToFile(file.Path, (string)BundlesSettings.ExportSettings());
            }
        }

        #endregion Command Implementation

        #region Handlers

        /// <summary>
        /// This function gets called when an item is removed to update the collection
        /// </summary>
        /// <param name="item"></param>
        private void NotifyItemRemovedHandle(BundleContainerViewModel item)
        {
            Items.Remove(item);
            item?.Dispose();

            if (Items.Count == 0)
            {
                NoBundlesAddItemLoad = true;
            }
        }

        /// <summary>
        /// This function gets called when an item is renamed to update the collection
        /// </summary>
        /// <param name="item"></param>
        private void NotifyBundleItemRemovedHandle(BundleItemViewModel item)
        {
            foreach (var bundle in Items)
            {
                if (bundle.BundleName == item.ParentBundleName)
                {
                    bundle.Contents.Remove(item);
                    item?.Dispose();

                    if (bundle.Contents.Count == 0)
                    {
                        bundle.NoBundleContentsTextVisibility = Visibility.Visible;
                    }
                }
            }
        }

        #endregion Handlers

        #region Public Helpers

        public void Save()
        {
            if (BundlesSettings.SavedBundles != null)
            {
                Dictionary<string, List<string>> bundles = new Dictionary<string, List<string>>();

                // For every bundle in items bundle collection:
                foreach (var bundle in Items)
                {
                    List<string> bundleItems = new List<string>();

                    // For every bundleItem in current bundle
                    foreach (var bundleItem in bundle.Contents)
                    {
                        if (bundleItem != null)
                        {
                            bundleItems.Add(bundleItem.Path);
                        }
                    }

                    bundles.Add(bundle.BundleName, bundleItems);
                }

                BundlesSettings.SavedBundles = bundles; // Calls Set()
            }
        }

        public async Task Load()
        {
            if (BundlesSettings.SavedBundles != null)
            {
                Items.Clear();

                // For every bundle in saved bundle collection:
                foreach (var bundle in BundlesSettings.SavedBundles)
                {
                    List<BundleItemViewModel> bundleItems = new List<BundleItemViewModel>();

                    // For every bundleItem in current bundle
                    foreach (var bundleItem in bundle.Value)
                    {
                        if (bundleItems.Count < Constants.Widgets.Bundles.MaxAmountOfItemsPerBundle)
                        {
                            if (bundleItem != null)
                            {
                                bundleItems.Add(new BundleItemViewModel(associatedInstance, bundleItem, await StorageItemHelpers.GetTypeFromPath(bundleItem, associatedInstance))
                                {
                                    ParentBundleName = bundle.Key,
                                    NotifyItemRemoved = NotifyBundleItemRemovedHandle
                                });
                            }
                        }
                    }

                    // Fill current bundle with collected bundle items
                    Items.Add(new BundleContainerViewModel(associatedInstance)
                    {
                        BundleName = bundle.Key,
                        NotifyItemRemoved = NotifyItemRemovedHandle,
                    }.SetBundleItems(bundleItems));
                }

                if (Items.Count == 0)
                {
                    NoBundlesAddItemLoad = true;
                }
                else
                {
                    NoBundlesAddItemLoad = false;
                }
            }
            else // Null, therefore no items :)
            {
                NoBundlesAddItemLoad = true;
            }
        }

        public void Initialize(IShellPage associatedInstance)
        {
            this.associatedInstance = associatedInstance;
        }

        public bool CanAddBundle(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                AddBundleErrorText = "BundlesWidgetAddBundleErrorInputEmpty".GetLocalized();
                return false;
            }

            if (!Items.Any((item) => item.BundleName == name))
            {
                AddBundleErrorText = string.Empty;
                return true;
            }
            else
            {
                AddBundleErrorText = "BundlesWidgetAddBundleErrorAlreadyExists".GetLocalized();
                return false;
            }
        }

        #endregion Public Helpers

        #region IDisposable

        public void Dispose()
        {
            foreach (var item in Items)
            {
                item.NotifyItemRemoved -= NotifyItemRemovedHandle;
                item?.Dispose();
            }

            associatedInstance = null;
            Items = null;
        }

        #endregion IDisposable
    }
}
