using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Assets;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.FlyoutControls.DeckScreenshot
{
	public static class DeckScreenshotHelper
	{
		private const int CardHeight = 35;
		private const int InfoHeight = 124;
		private const int ScreenshotWidth = 219;
		private const int Dpi = 96;

		public static async Task<RenderTargetBitmap> Generate(Deck deck, bool cardsOnly)
		{
			var height = CardHeight * deck.GetSelectedDeckVersion().Cards.Count;
			if(!cardsOnly)
				height += InfoHeight;

			// Wait for all images to be available
			if(AssetDownloaders.cardTileDownloader != null)
			{
				var tasks = deck.Cards
					.Select(c => AssetDownloaders.cardTileDownloader.GetAssetData(c))
					// If the tiles are available on disk they will be loaded
					// synchronously and the task should complete immediately.
					.Where(x => !x.IsCompleted).ToList();

				if(tasks.Any())
				{
					await Task.WhenAll(tasks);

					// For good measure. For some reason some small number of
					// images isn't always available immediately
					await Task.Delay(250);
				}
			}

			var control = new DeckView(deck, cardsOnly);
			await control.Init();
			control.Measure(new Size(ScreenshotWidth, height));
			control.Arrange(new Rect(new Size(ScreenshotWidth, height)));
			control.UpdateLayout();
			Log.Debug($"Screenshot: {control.ActualWidth} x {control.ActualHeight}");
			var bmp = new RenderTargetBitmap(ScreenshotWidth, height, Dpi, Dpi, PixelFormats.Pbgra32);
			bmp.Render(control);
			return bmp;
		}

		public static async Task<string?> Upload(BitmapSource bmpSource)
		{
			using(var ms = new MemoryStream())
			{
				var tmpFile = new FileInfo(Path.Combine(Config.Instance.DataDir, $"tmp{DateTime.Now.ToFileTime()}.png"));
				SaveToStream(ms, bmpSource);
				return await Imgur.Upload(Config.Instance.ImgurClientId, ms, tmpFile.FullName);
			}
		}

		public static string? Save(Deck deck, BitmapSource bmpSource)
		{
			var fileName = Helper.ShowSaveFileDialog(Helper.RemoveInvalidFileNameChars(deck.Name), "png");
			if(fileName == null)
				return null;
			using(var ms = new MemoryStream())
			using(var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				SaveToStream(ms, bmpSource);
				ms.WriteTo(fs);
			}
			return fileName;
		}

		private static void SaveToStream(Stream ms, BitmapSource bmpSource)
		{
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bmpSource));
			encoder.Save(ms);
		}

		public static bool CopyToClipboard(BitmapSource bmpSource)
		{
			try
			{
				Clipboard.SetImage(bmpSource);
				return true;
			}
			catch(Exception e)
			{
				ErrorManager.AddError("Error copying screenshot to clipboard", e.ToString());
				return false;
			}
		}


	}
}
