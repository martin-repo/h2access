// -------------------------------------------------------------------------------------------------
// <copyright file="StratagemLoadout.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Serilog;

namespace Helldivers2Accessibility.Models;

public sealed class StratagemLoadout
{
	public static readonly StratagemLoadout Empty = new(
		buttonA: null,
		buttonB: null,
		buttonX: null,
		buttonY: null
	);

	private readonly Dictionary<ControllerButton, DateTimeOffset> _cooldownEndTimes = new();

	private StratagemCode? _buttonA;

	private ImagePair? _buttonAImages;
	private StratagemCode? _buttonB;
	private ImagePair? _buttonBImages;
	private StratagemCode? _buttonX;
	private ImagePair? _buttonXImages;
	private StratagemCode? _buttonY;
	private ImagePair? _buttonYImages;

	public StratagemLoadout(
		StratagemCode? buttonA,
		StratagemCode? buttonB,
		StratagemCode? buttonX,
		StratagemCode? buttonY
	)
	{
		SetSlot(button: ControllerButton.ButtonA, slot: buttonA);
		SetSlot(button: ControllerButton.ButtonB, slot: buttonB);
		SetSlot(button: ControllerButton.ButtonX, slot: buttonX);
		SetSlot(button: ControllerButton.ButtonY, slot: buttonY);
	}

	public ImmutableArray<StratagemCode> ExistingSlots =>
		new[]
			{
				_buttonA,
				_buttonB,
				_buttonX,
				_buttonY
			}
			.Where(predicate: v => v is not null)
			.Select(selector: v => v!)
			.ToImmutableArray();

	public void ActivateCooldown(ControllerButton button)
	{
		var slot = GetSlot(button: button);
		if (slot is null)
		{
			return;
		}

		var now = DateTimeOffset.UtcNow;

		// Throwing duration is anywhere from 0-3s
		var throwingDuration = TimeSpan.FromSeconds(value: 2.5);

		var cooldownEndTime =
			now.Add(timeSpan: throwingDuration).Add(timeSpan: slot.DeploymentTime).Add(timeSpan: slot.CooldownTime);

		Log.Information(messageTemplate: "Activate cooldown for '{Slot}'", propertyValue: slot.Name);
		_cooldownEndTimes[key: button] = cooldownEndTime;
	}

	public void ClearAllCooldowns()
	{
		Log.Information(messageTemplate: "All cooldowns cleared");
		foreach (var key in _cooldownEndTimes.Keys.ToImmutableArray())
		{
			_cooldownEndTimes[key: key] = DateTimeOffset.MinValue;
		}
	}

	public ImageSource? GetImage(ControllerButton button) =>
		button switch
		{
			ControllerButton.ButtonA => IsOnCooldown(button: button) ? _buttonAImages?.Disabled : _buttonAImages?.Normal,
			ControllerButton.ButtonB => IsOnCooldown(button: button) ? _buttonBImages?.Disabled : _buttonBImages?.Normal,
			ControllerButton.ButtonX => IsOnCooldown(button: button) ? _buttonXImages?.Disabled : _buttonXImages?.Normal,
			ControllerButton.ButtonY => IsOnCooldown(button: button) ? _buttonYImages?.Disabled : _buttonYImages?.Normal,
			_ => throw new InvalidOperationException()
		};

	public ImmutableDictionary<ControllerButton, int?> GetRemainingCooldowns()
	{
		var now = DateTimeOffset.UtcNow;
		return _cooldownEndTimes.ToImmutableDictionary(
			keySelector: v => v.Key,
			elementSelector: v =>
			{
				var timeLeft = v.Value - now;
				return timeLeft > TimeSpan.Zero ? (int?)timeLeft.TotalSeconds : null;
			}
		);
	}

	public TimeSpan? GetShortestCooldownTime()
	{
		var now = DateTimeOffset.UtcNow;
		var cooldownEndTimes = _cooldownEndTimes
			.Values.Select(selector: v => v - now)
			.Where(predicate: v => v > TimeSpan.Zero)
			.ToImmutableArray();

		LogCooldowns(now: now);

		return cooldownEndTimes.IsEmpty ? null : cooldownEndTimes.Min();
	}

	public StratagemCode? GetSlot(ControllerButton button) =>
		button switch
		{
			ControllerButton.ButtonA => _buttonA,
			ControllerButton.ButtonB => _buttonB,
			ControllerButton.ButtonX => _buttonX,
			ControllerButton.ButtonY => _buttonY,
			_ => throw new InvalidOperationException()
		};

	public bool IsOnCooldown(ControllerButton button) => _cooldownEndTimes[key: button] > DateTimeOffset.UtcNow;

	public void SetSlot(ControllerButton button, StratagemCode? slot)
	{
		switch (button)
		{
			case ControllerButton.ButtonA:
				_buttonA = slot;
				_buttonAImages = LoadImagePair(stratagem: slot);
				_cooldownEndTimes[key: ControllerButton.ButtonA] = DateTimeOffset.MinValue;
				break;
			case ControllerButton.ButtonB:
				_buttonB = slot;
				_buttonBImages = LoadImagePair(stratagem: slot);
				_cooldownEndTimes[key: ControllerButton.ButtonB] = DateTimeOffset.MinValue;
				break;
			case ControllerButton.ButtonX:
				_buttonX = slot;
				_buttonXImages = LoadImagePair(stratagem: slot);
				_cooldownEndTimes[key: ControllerButton.ButtonX] = DateTimeOffset.MinValue;
				break;
			case ControllerButton.ButtonY:
				_buttonY = slot;
				_buttonYImages = LoadImagePair(stratagem: slot);
				_cooldownEndTimes[key: ControllerButton.ButtonY] = DateTimeOffset.MinValue;
				break;
			default: throw new InvalidOperationException();
		}
	}

	private static ImagePair? LoadImagePair(StratagemCode? stratagem)
	{
		if (stratagem is null)
		{
			return null;
		}

		var normal = ImageUtilities.LoadImageFromFile(path: stratagem.StratagemFileName);
		var disabled = new FormatConvertedBitmap(
			source: normal,
			destinationFormat: PixelFormats.Gray8,
			destinationPalette: null,
			alphaThreshold: 0
		);
		disabled.Freeze();

		return new ImagePair(Normal: normal, Disabled: disabled);
	}

	private void LogCooldowns(DateTimeOffset now)
	{
		Log.Information(
			messageTemplate: """
			Current cooldowns
			ButtonY: {ButtonY}
			ButtonX: {ButtonX}
			ButtonB: {ButtonB}
			ButtonA: {ButtonA}
			""",
			GetValue(button: ControllerButton.ButtonY),
			GetValue(button: ControllerButton.ButtonX),
			GetValue(button: ControllerButton.ButtonB),
			GetValue(button: ControllerButton.ButtonA)
		);

		return;

		string GetValue(ControllerButton button)
		{
			var cooldown = _cooldownEndTimes[key: button] > now ? _cooldownEndTimes[key: button] : (DateTimeOffset?)null;
			var cooldownText = cooldown is not null ? cooldown.Value.ToString(format: "HH:mm:ss") : "N/A";
			return cooldownText;
		}
	}

	private sealed record ImagePair(BitmapSource Normal, BitmapSource Disabled);
}