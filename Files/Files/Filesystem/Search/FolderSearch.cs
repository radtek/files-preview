using Files.Common;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using static Files.Helpers.NativeFindStorageItemHelper;
using FileAttributes = System.IO.FileAttributes;

namespace Files.Filesystem.Search
{
    internal class FolderSearch
    {
        public static async Task<ObservableCollection<ListedItem>> SearchForUserQueryTextAsync(string userText, string WorkingDirectory, IShellPage associatedInstance, int maxItemCount = 10)
        {
            var returnedItems = new ObservableCollection<ListedItem>();
            maxItemCount = maxItemCount < 0 ? int.MaxValue : maxItemCount;

            var hiddenOnlyFromWin32 = false;
            var workingDir = await associatedInstance.FilesystemViewModel.GetFolderFromPathAsync(WorkingDirectory);
            if (workingDir)
            {
                foreach (var item in await SearchWithStorageFolder(userText, workingDir, maxItemCount))
                {
                    returnedItems.Add(item);
                }
                hiddenOnlyFromWin32 = true;
            }
            if (!hiddenOnlyFromWin32 || App.AppSettings.AreHiddenItemsVisible)
            {
                foreach (var item in await SearchWithWin32(userText, WorkingDirectory, hiddenOnlyFromWin32,
                    maxItemCount - returnedItems.Count))
                {
                    returnedItems.Add(item);
                }
            }

            return returnedItems;
        }

        private static async Task<IList<ListedItem>> SearchWithWin32(string userText, string WorkingDirectory, bool hiddenOnly, int maxItemCount = 10)
        {
            var returnedItems = new List<ListedItem>();
            (IntPtr hFile, WIN32_FIND_DATA findData) = await Task.Run(() =>
            {
                FINDEX_INFO_LEVELS findInfoLevel = FINDEX_INFO_LEVELS.FindExInfoBasic;
                int additionalFlags = FIND_FIRST_EX_LARGE_FETCH;
                IntPtr hFileTsk = FindFirstFileExFromApp(WorkingDirectory + $"\\*{userText}*.*", findInfoLevel, out WIN32_FIND_DATA findDataTsk, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero,
                    additionalFlags);
                return (hFileTsk, findDataTsk);
            }).WithTimeoutAsync(TimeSpan.FromSeconds(5));

            if (hFile != IntPtr.Zero)
            {
                await Task.Run(() =>
                {
                    var hasNextFile = false;
                    do
                    {
                        if (returnedItems.Count >= maxItemCount)
                        {
                            break;
                        }
                        var itemPath = Path.Combine(WorkingDirectory, findData.cFileName);
                        if (((FileAttributes)findData.dwFileAttributes & FileAttributes.System) != FileAttributes.System || !App.AppSettings.AreSystemItemsHidden)
                        {
                            var isHidden = ((FileAttributes)findData.dwFileAttributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                            if ((!isHidden && !hiddenOnly) || (isHidden && App.AppSettings.AreHiddenItemsVisible))
                            {
                                if (((FileAttributes)findData.dwFileAttributes & FileAttributes.Directory) != FileAttributes.Directory)
                                {
                                    returnedItems.Add(new ListedItem(null)
                                    {
                                        PrimaryItemAttribute = StorageItemTypes.File,
                                        ItemName = findData.cFileName,
                                        ItemPath = itemPath,
                                        IsHiddenItem = true,
                                        LoadFileIcon = false,
                                        LoadUnknownTypeGlyph = true,
                                        LoadFolderGlyph = false,
                                        ItemPropertiesInitialized = true
                                    });
                                }
                                else if (((FileAttributes)findData.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                                {
                                    if (findData.cFileName != "." && findData.cFileName != "..")
                                    {
                                        returnedItems.Add(new ListedItem(null)
                                        {
                                            PrimaryItemAttribute = StorageItemTypes.Folder,
                                            ItemName = findData.cFileName,
                                            ItemPath = itemPath,
                                            IsHiddenItem = true,
                                            LoadFileIcon = false,
                                            LoadUnknownTypeGlyph = false,
                                            LoadFolderGlyph = true,
                                            ItemPropertiesInitialized = true
                                        });
                                    }
                                }
                            }
                        }

                        hasNextFile = FindNextFile(hFile, out findData);
                    } while (hasNextFile);

                    FindClose(hFile);
                });
            }
            return returnedItems;
        }

        private static async Task<IList<ListedItem>> SearchWithStorageFolder(string userText, StorageFolder workingDir, int maxItemCount = 10)
        {
            QueryOptions options = new QueryOptions()
            {
                FolderDepth = FolderDepth.Deep,
                UserSearchFilter = string.IsNullOrWhiteSpace(userText) ? null : userText,
            };

            if (App.AppSettings.SearchUnindexedItems)
            {
                options.IndexerOption = IndexerOption.DoNotUseIndexer;
            }
            else
            {
                options.IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties;
            }

            options.SortOrder.Clear();
            options.SortOrder.Add(new SortEntry()
            {
                PropertyName = "System.Search.Rank",
                AscendingOrder = false
            });
            options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.None, null);
            options.SetThumbnailPrefetch(Windows.Storage.FileProperties.ThumbnailMode.ListView, 24, Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);
            var itemQueryResult = workingDir.CreateItemQueryWithOptions(options);
            uint stepSize = Math.Min(500, (uint)maxItemCount);
            IReadOnlyList<IStorageItem> items = await itemQueryResult.GetItemsAsync(0, stepSize);
            var returnedItems = new List<ListedItem>();
            uint index = 0;
            while (items.Count > 0)
            {
                foreach (IStorageItem item in items)
                {
                    if (item.IsOfType(StorageItemTypes.Folder))
                    {
                        var folder = (StorageFolder)item;
                        returnedItems.Add(new ListedItem(null)
                        {
                            PrimaryItemAttribute = StorageItemTypes.Folder,
                            ItemName = folder.DisplayName,
                            ItemPath = folder.Path,
                            LoadFolderGlyph = true,
                            LoadUnknownTypeGlyph = false,
                            ItemPropertiesInitialized = true
                        });
                    }
                    else if (item.IsOfType(StorageItemTypes.File))
                    {
                        var file = (StorageFile)item;
                        var bitmapIcon = new BitmapImage();
                        var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.ListView, 24, Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

                        if (thumbnail != null)
                        {
                            await bitmapIcon.SetSourceAsync(thumbnail);
                            returnedItems.Add(new ListedItem(null)
                            {
                                PrimaryItemAttribute = StorageItemTypes.File,
                                ItemName = file.DisplayName,
                                ItemPath = file.Path,
                                LoadFileIcon = true,
                                FileImage = bitmapIcon,
                                LoadUnknownTypeGlyph = false,
                                LoadFolderGlyph = false,
                                ItemPropertiesInitialized = true
                            });
                        }
                        else
                        {
                            returnedItems.Add(new ListedItem(null)
                            {
                                PrimaryItemAttribute = StorageItemTypes.File,
                                ItemName = file.DisplayName,
                                ItemPath = file.Path,
                                LoadFileIcon = false,
                                LoadUnknownTypeGlyph = true,
                                LoadFolderGlyph = false,
                                ItemPropertiesInitialized = true
                            });
                        }
                    }
                }
                index += (uint)items.Count;
                stepSize = Math.Min(500, (uint)(maxItemCount - returnedItems.Count));
                items = await itemQueryResult.GetItemsAsync(index, stepSize);
            }
            return returnedItems;
        }
    }
}