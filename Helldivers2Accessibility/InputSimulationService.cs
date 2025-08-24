// -------------------------------------------------------------------------------------------------
// <copyright file="InputSimulationService.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Helldivers2Accessibility;

public static class InputSimulationService
{
	private const int SM_SWAPBUTTON = 23;

	public static void SimulateKeyPress(byte virtualKeyCode, bool press)
	{
		var input = new Input
		{
			type = 1, // INPUT_KEYBOARD
			u = new InputUnion
			{
				ki = new KeyboardInput
				{
					wVk = virtualKeyCode,
					wScan = 0,
					dwFlags = press ? 0u : 0x0002,
					time = 0,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};

		_ = SendInput(nInputs: 1, pInputs: [input], cbSize: Marshal.SizeOf<Input>());
	}

	public static void SimulateLeftMouseClick(TimeSpan holdDuration)
	{
		// Check if mouse buttons are swapped in Windows settings
		var buttonsSwapped = GetSystemMetrics(nIndex: SM_SWAPBUTTON) != 0;

		var downFlag = buttonsSwapped ? 0x0008 : 0x0002; // MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN
		var upFlag = buttonsSwapped ? 0x0010 : 0x0004; // MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP

		// Mouse down
		var mouseDownInput = new Input
		{
			type = 0, // INPUT_MOUSE
			u = new InputUnion
			{
				mi = new MouseInput
				{
					dx = 0,
					dy = 0,
					mouseData = 0,
					dwFlags = (uint)downFlag,
					time = 0,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};

		// Mouse up
		var mouseUpInput = new Input
		{
			type = 0, // INPUT_MOUSE
			u = new InputUnion
			{
				mi = new MouseInput
				{
					dx = 0,
					dy = 0,
					mouseData = 0,
					dwFlags = (uint)upFlag,
					time = 0,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};

		// Send mouse down
		_ = SendInput(nInputs: 1, pInputs: [mouseDownInput], cbSize: Marshal.SizeOf<Input>());

		// Hold for specified duration
		Thread.Sleep(millisecondsTimeout: (int)holdDuration.TotalMilliseconds);

		// Send mouse up
		_ = SendInput(nInputs: 1, pInputs: [mouseUpInput], cbSize: Marshal.SizeOf<Input>());
	}

	[DllImport(dllName: "user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport(dllName: "user32.dll")]
	private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

	// SendInput structures
	[StructLayout(layoutKind: LayoutKind.Sequential)]
	private struct Input
	{
		public uint type;
		public InputUnion u;
	}

	[StructLayout(layoutKind: LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(offset: 0)] public MouseInput mi;
		[FieldOffset(offset: 0)] public KeyboardInput ki;
		[FieldOffset(offset: 0)] public HardwareInput hi;
	}

	[StructLayout(layoutKind: LayoutKind.Sequential)]
	private struct KeyboardInput
	{
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	}

	[StructLayout(layoutKind: LayoutKind.Sequential)]
	private struct MouseInput
	{
		public int dx;
		public int dy;
		public uint mouseData;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	}

	[StructLayout(layoutKind: LayoutKind.Sequential)]
	private struct HardwareInput
	{
		public uint uMsg;
		public ushort wParamL;
		public ushort wParamH;
	}
}