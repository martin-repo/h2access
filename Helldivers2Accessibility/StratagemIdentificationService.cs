// -------------------------------------------------------------------------------------------------
// <copyright file="StratagemIdentificationService.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Helldivers2Accessibility;

public sealed class StratagemIdentificationService : IDisposable
{
	private const double PixelDifferenceThreshold = 0.05;
	private const string StratagemImagesFolder = "StratagemImages";
	private const double TotalDifferenceThreshold = 0.01;

	private readonly Dictionary<string, Bitmap> _knownStratagems = new();

	public void Dispose()
	{
		foreach (var bitmap in _knownStratagems.Values)
		{
			bitmap.Dispose();
		}

		_knownStratagems.Clear();
	}

	public ImmutableArray<string> IdentifyStratagems(ImmutableArray<Bitmap> stratagemIcons) =>
	[
		GetOrCreateStratagemName(icon: stratagemIcons[index: 0], unknownPrefix: "first"),
		GetOrCreateStratagemName(icon: stratagemIcons[index: 1], unknownPrefix: "second"),
		GetOrCreateStratagemName(icon: stratagemIcons[index: 2], unknownPrefix: "third"),
		GetOrCreateStratagemName(icon: stratagemIcons[index: 3], unknownPrefix: "fourth")
	];

	public void Initialize() => LoadKnownStratagems();

	private bool AreIconsSimilar(Bitmap icon1, Bitmap icon2)
	{
		if (icon1.Width != icon2.Width || icon1.Height != icon2.Height)
		{
			return false;
		}

		var totalPixels = icon1.Width * icon1.Height;
		var significantlyDifferentPixels = 0;

		for (var x = 0; x < icon1.Width; x++)
		{
			for (var y = 0; y < icon1.Height; y++)
			{
				var pixel1 = icon1.GetPixel(x: x, y: y);
				var pixel2 = icon2.GetPixel(x: x, y: y);

				if (IsPixelSignificantlyDifferent(pixel1: pixel1, pixel2: pixel2))
				{
					significantlyDifferentPixels++;
				}
			}
		}

		var percentDifferentPixels = (double)significantlyDifferentPixels / totalPixels;
		return percentDifferentPixels < TotalDifferenceThreshold;
	}

	private string GetOrCreateStratagemName(Bitmap icon, string unknownPrefix)
	{
		// Check against all known stratagems
		foreach (var known in _knownStratagems)
		{
			if (AreIconsSimilar(icon1: icon, icon2: known.Value))
			{
				return known.Key;
			}
		}

		var unknownStratagemName = $"_{unknownPrefix}UnknownStratagem";
		var filePath = Path.Combine(path1: StratagemImagesFolder, path2: $"{unknownStratagemName}.png");
		icon.Save(filename: filePath, format: ImageFormat.Png);
		return unknownStratagemName;
	}

	private bool IsPixelSignificantlyDifferent(Color pixel1, Color pixel2)
	{
		// Calculate color difference as percentage
		var rDiff = Math.Abs(value: pixel1.R - pixel2.R) / 255.0;
		var gDiff = Math.Abs(value: pixel1.G - pixel2.G) / 255.0;
		var bDiff = Math.Abs(value: pixel1.B - pixel2.B) / 255.0;

		// Use average of RGB differences
		var avgDiff = (rDiff + gDiff + bDiff) / 3.0;

		return avgDiff > PixelDifferenceThreshold;
	}

	private void LoadKnownStratagems()
	{
		if (!Directory.Exists(path: StratagemImagesFolder))
		{
			Directory.CreateDirectory(path: StratagemImagesFolder);
			return;
		}

		foreach (var filePath in Directory.GetFiles(path: StratagemImagesFolder, searchPattern: "*.png"))
		{
			var name = Path.GetFileNameWithoutExtension(path: filePath);
			if (name.StartsWith(value: '_'))
			{
				continue;
			}

			var imageBytes = File.ReadAllBytes(path: filePath);
			using var memoryStream = new MemoryStream(buffer: imageBytes);
			var bitmap = new Bitmap(stream: memoryStream);

			_knownStratagems[key: name] = bitmap;
		}
	}
}