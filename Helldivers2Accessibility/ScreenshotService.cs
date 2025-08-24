// -------------------------------------------------------------------------------------------------
// <copyright file="ScreenshotService.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Helldivers2Accessibility;

public static class ScreenshotService
{
	private const int SM_CXSCREEN = 0;
	private const int SM_CYSCREEN = 1;
	private const int SRCCOPY = 0x00CC0020;

	public static Bitmap TakeScreenshot(bool saveToDisk)
	{
		// Get screen dimensions
		var screenWidth = GetSystemMetrics(nIndex: SM_CXSCREEN);
		var screenHeight = GetSystemMetrics(nIndex: SM_CYSCREEN);

		// Get device context for the screen
		var screenDC = GetDC(hWnd: IntPtr.Zero);
		var memoryDC = CreateCompatibleDC(hDC: screenDC);
		var screenshotBitmap = CreateCompatibleBitmap(hDC: screenDC, nWidth: screenWidth, nHeight: screenHeight);

		// Put the screenshot bitmap into the memory dc, and delete the default bitmap that was there during creation
		var defaultBitmap = SelectObject(hDC: memoryDC, hGDIObj: screenshotBitmap);
		DeleteObject(hObject: defaultBitmap);

		// Copy screen to memory
		BitBlt(
			hDestDC: memoryDC,
			x: 0,
			y: 0,
			nWidth: screenWidth,
			nHeight: screenHeight,
			hSrcDC: screenDC,
			xSrc: 0,
			ySrc: 0,
			dwRop: SRCCOPY
		);

		// Create managed bitmap from the native bitmap
		var screenshot = Image.FromHbitmap(hbitmap: screenshotBitmap);

		// Cleanup native resources
		DeleteDC(hDC: memoryDC);
		_ = ReleaseDC(hWnd: IntPtr.Zero, hDC: screenDC);

		if (saveToDisk)
		{
			SaveScreenshot(screenshot: screenshot);
		}

		return screenshot;
	}

	[DllImport(dllName: "gdi32.dll")]
	private static extern bool BitBlt(
		IntPtr hDestDC,
		int x,
		int y,
		int nWidth,
		int nHeight,
		IntPtr hSrcDC,
		int xSrc,
		int ySrc,
		int dwRop
	);

	[DllImport(dllName: "gdi32.dll")]
	private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

	[DllImport(dllName: "gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

	[DllImport(dllName: "gdi32.dll")]
	private static extern bool DeleteDC(IntPtr hDC);

	[DllImport(dllName: "gdi32.dll")]
	private static extern bool DeleteObject(IntPtr hObject);

	[DllImport(dllName: "user32.dll")]
	private static extern IntPtr GetDC(IntPtr hWnd);

	[DllImport(dllName: "user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport(dllName: "user32.dll")]
	private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

	private static void SaveScreenshot(Bitmap screenshot)
	{
		// Create screenshots directory if it doesn't exist
		var directory = "Screenshots";
		if (!Directory.Exists(path: directory))
		{
			Directory.CreateDirectory(path: directory);
		}

		// Generate filename with timestamp
		var filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
		var fullPath = Path.Combine(path1: directory, path2: filename);

		// Save the screenshot
		screenshot.Save(filename: fullPath, format: ImageFormat.Png);
	}

	[DllImport(dllName: "gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIObj);
}