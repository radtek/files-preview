using Files.Filesystem;
using Files.ViewModels.Properties;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace Files.ViewModels.Previews
{
    public class PDFPreviewViewModel : BasePreviewModel
    {
        private Visibility loadingBarVisibility;

        public PDFPreviewViewModel(ListedItem item) : base(item)
        {
        }

        public static List<string> Extensions = new List<string>()
        {
            ".pdf",
        };

        public Visibility LoadingBarVisibility
        {
            get => loadingBarVisibility;
            set => SetProperty(ref loadingBarVisibility, value);
        }

        public ObservableCollection<PageViewModel> Pages { get; set; } = new ObservableCollection<PageViewModel>();

        public async override void LoadPreviewAndDetails()
        {
            var pdf = await PdfDocument.LoadFromFileAsync(ItemFile);

            // Add the number of pages to the details
            Item.FileDetails.Add(new FileProperty()
            {
                NameResource = "PropertyPageCount",
                Value = pdf.PageCount,
            });

            LoadSystemFileProperties();

            // This fixes an issue where loading an absurdly large PDF would take to much RAM
            // and eventually cause a crash
            var limit = Math.Clamp(pdf.PageCount, 0, Constants.PreviewPane.PDFPageLimit);

            for (uint i = 0; i < limit; i++)
            {
                // Stop loading if the user has cancelled
                if (LoadCancelledTokenSource.Token.IsCancellationRequested)
                {
                    return;
                }

                var page = pdf.GetPage(i);
                await page.PreparePageAsync();
                using var stream = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(stream);

                var src = new BitmapImage();
                await src.SetSourceAsync(stream);
                var pageData = new PageViewModel()
                {
                    PageImage = src,
                    PageNumber = (int)i,
                };
                Pages.Add(pageData);
            }
            LoadingBarVisibility = Visibility.Collapsed;
        }

        public struct PageViewModel
        {
            public int PageNumber { get; set; }
            public BitmapImage PageImage { get; set; }
        }
    }
}