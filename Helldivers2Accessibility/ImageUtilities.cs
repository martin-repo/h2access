// -------------------------------------------------------------------------------------------------
// <copyright file="ImageUtilities.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.IO;
using System.Windows.Media.Imaging;

namespace Helldivers2Accessibility;

public static class ImageUtilities
{
	public static BitmapImage LoadImageFromFile(string path)
	{
		var bitmap = new BitmapImage();
		using var stream = new FileStream(
			path: path,
			mode: FileMode.Open,
			access: FileAccess.Read,
			share: FileShare.Read
		);
		bitmap.BeginInit();
		bitmap.CacheOption = BitmapCacheOption.OnLoad;
		bitmap.StreamSource = stream;
		bitmap.EndInit();
		bitmap.Freeze();

		return bitmap;
	}
}