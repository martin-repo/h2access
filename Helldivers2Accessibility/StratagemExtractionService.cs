// -------------------------------------------------------------------------------------------------
// <copyright file="StratagemExtractionService.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Helldivers2Accessibility;

public static class StratagemExtractionService
{
	// Predefined coordinates for the second row of stratagems (will need calibration)
	private static readonly ImmutableArray<Rectangle> StratagemRegions =
	[
		new(
			x: 60,
			y: 842,
			width: 70,
			height: 70
		),
		new(
			x: 145,
			y: 842,
			width: 70,
			height: 70
		),
		new(
			x: 230,
			y: 842,
			width: 70,
			height: 70
		),
		new(
			x: 315,
			y: 842,
			width: 70,
			height: 70
		)
	];

	public static ImmutableArray<Bitmap> ExtractStratagemIcons(Bitmap screenshot, bool saveToDisk)
	{
		var stratagemBuilder = ImmutableArray.CreateBuilder<Bitmap>();

		foreach (var region in StratagemRegions)
		{
			// Extract the region as a new bitmap
			var iconBitmap = new Bitmap(width: region.Width, height: region.Height);

			using (var graphics = Graphics.FromImage(image: iconBitmap))
			{
				graphics.DrawImage(
					image: screenshot,
					destRect: region with
					{
						X = 0,
						Y = 0
					},
					srcRect: region,
					srcUnit: GraphicsUnit.Pixel
				);
			}

			stratagemBuilder.Add(item: iconBitmap);
		}

		var stratagems = stratagemBuilder.ToImmutable();

		if (saveToDisk)
		{
			SaveExtractedIcons(stratagems: stratagems);
		}

		return stratagems;
	}

	private static void SaveExtractedIcons(ImmutableArray<Bitmap> stratagems)
	{
		const string DirectoryName = "ExtractedIcons";
		if (!Directory.Exists(path: DirectoryName))
		{
			Directory.CreateDirectory(path: DirectoryName);
		}

		// Generate timestamp for this batch
		var timestamp = DateTime.Now.ToString(format: "yyyyMMdd_HHmmss");

		// Save each icon
		for (var i = 0; i < stratagems.Length; i++)
		{
			var filename = $"stratagem_{i + 1}_{timestamp}.png";
			var fullPath = Path.Combine(path1: DirectoryName, path2: filename);
			stratagems[index: i].Save(filename: fullPath, format: ImageFormat.Png);
		}
	}
}