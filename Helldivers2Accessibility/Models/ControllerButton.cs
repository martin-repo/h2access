// -------------------------------------------------------------------------------------------------
// <copyright file="ControllerButton.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace Helldivers2Accessibility.Models;

public enum ControllerButton
{
	DpadUp = 0x0001,
	DpadDown = 0x0002,
	DpadLeft = 0x0004,
	DpadRight = 0x0008,
	Start = 0x0010,
	Back = 0x0020,
	LeftStick = 0x0040,
	RightStick = 0x0080,
	LeftBumper = 0x0100,
	LeftTrigger = -1,
	RightBumper = 0x0200,
	RightTrigger = -2,
	ButtonA = 0x1000,
	ButtonB = 0x2000,
	ButtonX = 0x4000,
	ButtonY = 0x8000
}