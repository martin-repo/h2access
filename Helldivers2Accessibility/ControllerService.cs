// -------------------------------------------------------------------------------------------------
// <copyright file="ControllerService.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Runtime.InteropServices;

using Helldivers2Accessibility.Models;

namespace Helldivers2Accessibility;

public sealed class ControllerService : IDisposable
{
	private const byte TriggerThreshold = 30;

	private static XInputState _lastState;
	private static Timer? _pollTimer;

	public ControllerService()
	{
		_ = XInputGetState(dwUserIndex: 0, pState: ref _lastState);

		_pollTimer = new Timer(
			callback: Poll,
			state: null,
			dueTime: 0,
			period: 8 // == 125hz == Xbox controller's native polling frequency
		);
	}

	public event EventHandler<ControllerState>? StateChanged;

	public void Dispose() => _pollTimer?.Dispose();

	private static ImmutableHashSet<ControllerButton> GetButtonsFromFlags(ushort buttonFlags)
	{
		var buttons = ImmutableHashSet.CreateBuilder<ControllerButton>();

		foreach (var button in Enum.GetValues<ControllerButton>())
		{
			if ((int)button <= 0)
			{
				continue;
			}

			if ((buttonFlags & (ushort)button) != 0)
			{
				buttons.Add(item: button);
			}
		}

		return buttons.ToImmutable();
	}

	[DllImport(dllName: "xinput1_4.dll")]
	private static extern uint XInputGetState(uint dwUserIndex, ref XInputState pState);

	private void Poll(object? state)
	{
		var currentState = new XInputState();
		var result = XInputGetState(dwUserIndex: 0, pState: ref currentState);

		if (result != 0)
		{
			return; // Controller not connected
		}

		if (currentState.Gamepad.wButtons == _lastState.Gamepad.wButtons &&
			currentState.Gamepad.bLeftTrigger == _lastState.Gamepad.bLeftTrigger &&
			currentState.Gamepad.bRightTrigger == _lastState.Gamepad.bRightTrigger)
		{
			return; // Button state hasn't changed
		}

		var currentButtons = currentState.Gamepad.wButtons;
		var pressed = GetButtonsFromFlags(buttonFlags: currentButtons);

		var pressedSincePreviousPoll = (ushort)(currentState.Gamepad.wButtons & ~_lastState.Gamepad.wButtons);
		var added = GetButtonsFromFlags(buttonFlags: pressedSincePreviousPoll);

		var releasedSincePreviousPoll = (ushort)(~currentState.Gamepad.wButtons & _lastState.Gamepad.wButtons);
		var removed = GetButtonsFromFlags(buttonFlags: releasedSincePreviousPoll);

		HandleTriggerState(
			currentTrigger: currentState.Gamepad.bLeftTrigger,
			lastTrigger: _lastState.Gamepad.bLeftTrigger,
			triggerButton: ControllerButton.LeftTrigger
		);
		HandleTriggerState(
			currentTrigger: currentState.Gamepad.bRightTrigger,
			lastTrigger: _lastState.Gamepad.bRightTrigger,
			triggerButton: ControllerButton.RightTrigger
		);

		_lastState = currentState;

		var controllerState = new ControllerState(Pressed: pressed, Added: added, Removed: removed);
		StateChanged?.Invoke(sender: this, e: controllerState);

		return;

		void HandleTriggerState(byte currentTrigger, byte lastTrigger, ControllerButton triggerButton)
		{
			var isCurrentlyPressed = currentTrigger > TriggerThreshold;
			var wasPreviouslyPressed = lastTrigger > TriggerThreshold;

			if (isCurrentlyPressed && !wasPreviouslyPressed)
			{
				// Trigger pressed
				pressed = pressed.Add(item: triggerButton);
				added = added.Add(item: triggerButton);
			}
			else if (!isCurrentlyPressed && wasPreviouslyPressed)
			{
				// Trigger released
				removed = removed.Add(item: triggerButton);
			}
			else if (isCurrentlyPressed)
			{
				// Trigger still pressed (but value changed)
				pressed = pressed.Add(item: triggerButton);
			}
		}
	}

	public sealed record ControllerState(
		ImmutableHashSet<ControllerButton> Pressed,
		ImmutableHashSet<ControllerButton> Added,
		ImmutableHashSet<ControllerButton> Removed
	);

	[StructLayout(layoutKind: LayoutKind.Sequential)]
	private struct XInputState
	{
		public uint dwPacketNumber;
		public XInputGamepad Gamepad;
	}

	[StructLayout(layoutKind: LayoutKind.Sequential)]
	private struct XInputGamepad
	{
		public ushort wButtons;
		public byte bLeftTrigger;
		public byte bRightTrigger;
		public short sThumbLX;
		public short sThumbLY;
		public short sThumbRX;
		public short sThumbRY;
	}
}