// -------------------------------------------------------------------------------------------------
// <copyright file="StratagemService.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Helldivers2Accessibility.Models;

using Serilog;

namespace Helldivers2Accessibility;

public sealed class StratagemService : IDisposable
{
	private const string CurrentStratagemsFileName = "CurrentStratagems.json";

	private static readonly TimeSpan DelayBeforeKeyUp = TimeSpan.FromMilliseconds(milliseconds: 25);
	private static readonly TimeSpan DelayBetweenKeyPresses = TimeSpan.FromMilliseconds(milliseconds: 50);

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly ControllerService _controllerService;

	private readonly bool _debugMode;
	private readonly StratagemIdentificationService _stratagemIdentificationService;
	private ControllerButton? _activeLoadoutSlot;

	private Timer? _cooldownOverTimer;

	private ImmutableDictionary<string, ImmutableArray<StratagemButton>> _stratagemCodes =
		ImmutableDictionary<string, ImmutableArray<StratagemButton>>.Empty;

	private bool _stratagemMacroActivated;
	private volatile bool _stratagemReadyToDeploy;

	public StratagemService(
		bool debugMode,
		ControllerService controllerService,
		StratagemIdentificationService stratagemIdentificationService
	)
	{
		_debugMode = debugMode;
		_controllerService = controllerService;
		_stratagemIdentificationService = stratagemIdentificationService;

		_controllerService.StateChanged += OnControllerStateChanged;
	}

	public event EventHandler? StratagemLoadoutUpdated;

	public StratagemLoadout StratagemLoadout { get; private set; } = StratagemLoadout.Empty;

	public ImmutableDictionary<string, StratagemCode> Stratagems { get; private set; } =
		ImmutableDictionary<string, StratagemCode>.Empty;

	public void Dispose() => _controllerService.StateChanged -= OnControllerStateChanged;

	public void Initialize()
	{
		var assembly = Assembly.GetExecutingAssembly();

		using var jsonStream =
			assembly.GetManifestResourceStream(name: "Helldivers2Accessibility.Resources.StratagemCodes.json")!;
		using var jsonReader = new StreamReader(stream: jsonStream);
		var json = jsonReader.ReadToEnd();
		var jsonOptions = new JsonSerializerOptions(defaults: JsonSerializerDefaults.Web)
		{
			Converters = { new JsonStringEnumConverter() }
		};
		var codes = JsonSerializer.Deserialize<ImmutableArray<StratagemCode>>(json: json, options: jsonOptions);
		Stratagems = codes.ToImmutableDictionary(keySelector: v => v.Name);
		_stratagemCodes = codes.ToImmutableDictionary(keySelector: v => v.Name, elementSelector: v => v.Code);

		const string DirectoryName = "StratagemImages";
		if (!Directory.Exists(path: DirectoryName))
		{
			Directory.CreateDirectory(path: DirectoryName);
		}

		using var unknownStratagemImageStream = assembly.GetManifestResourceStream(
			name: "Helldivers2Accessibility.Resources.Stratagems.UnknownStratagem.png"
		)!;

		var existingFiles =
			Directory.GetFiles(path: DirectoryName).Select(selector: Path.GetFileName).ToImmutableHashSet();

		foreach (var code in codes)
		{
			var fileName = $"{code.Name}.png";
			if (existingFiles.Contains(item: fileName))
			{
				continue;
			}

			unknownStratagemImageStream.Position = 0;

			using var fileStream = File.Create(path: Path.Combine(path1: DirectoryName, path2: fileName));
			unknownStratagemImageStream.CopyTo(destination: fileStream);
		}

		if (File.Exists(path: CurrentStratagemsFileName))
		{
			var savedNames = JsonSerializer.Deserialize<ImmutableArray<string>>(
				json: File.ReadAllText(path: CurrentStratagemsFileName),
				options: JsonOptions
			);
			UpdateLoadoutFromNames(stratagemNames: savedNames);
		}
	}

	public void ReplaceStratagem(ControllerButton button, string stratagemName)
	{
		var stratagemSlot = Stratagems[key: stratagemName];
		StratagemLoadout.SetSlot(button: button, slot: stratagemSlot);

		SaveAndTriggerLoadout();
	}

	private static async Task PlayMacroAsync(ImmutableArray<StratagemButton> macro, bool standUpFirst)
	{
		if (standUpFirst)
		{
			InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.C, press: true);
			await Task.Delay(delay: DelayBeforeKeyUp);
			InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.C, press: false);
			await Task.Delay(delay: DelayBetweenKeyPresses);
		}

		// Hold CTRL
		InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.LeftCtrl, press: true);

		// Press macro keys with delay
		foreach (var button in macro)
		{
			var keyCode = button switch
			{
				StratagemButton.Left => VirtualKeyCodes.Left,
				StratagemButton.Right => VirtualKeyCodes.Right,
				StratagemButton.Up => VirtualKeyCodes.Up,
				StratagemButton.Down => VirtualKeyCodes.Down,
				_ => throw new InvalidOperationException()
			};

			await Task.Delay(delay: DelayBetweenKeyPresses);
			InputSimulationService.SimulateKeyPress(virtualKeyCode: keyCode, press: true);
			await Task.Delay(delay: DelayBeforeKeyUp);
			InputSimulationService.SimulateKeyPress(virtualKeyCode: keyCode, press: false);
		}

		// Release CTRL
		await Task.Delay(delay: DelayBeforeKeyUp);
		InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.LeftCtrl, press: false);
	}

	private void AssistCurrentStratagemWeapon()
	{
		if (StratagemLoadout.ExistingSlots.Any(predicate: v => v.Name == "Epoch"))
		{
			InputSimulationService.SimulateLeftMouseClick(holdDuration: TimeSpan.FromSeconds(value: 2.5d));
		}
	}

	private void CooldownTimerElapsed(object? state)
	{
		Log.Information(messageTemplate: "Cooldown timer elapsed");
		StartCooldownTimerIfAnyStratagemOnCooldown();
		StratagemLoadoutUpdated?.Invoke(sender: this, e: EventArgs.Empty);
	}

	private void OnControllerStateChanged(object? sender, ControllerService.ControllerState state)
	{
		ImmutableHashSet<ControllerButton> stratagemDeploymentCancellationButtons =
		[
			ControllerButton.LeftBumper, ControllerButton.ButtonY
		];
		if (state.Added.Intersect(other: stratagemDeploymentCancellationButtons).Count > 0)
		{
			_stratagemReadyToDeploy = false;
		}

		if (state.Added.Contains(item: ControllerButton.LeftBumper))
		{
			// Only allow one macro to run per single LB press
			_stratagemMacroActivated = false;
		}

		if (_stratagemReadyToDeploy &&
			state.Removed.Contains(item: ControllerButton.RightTrigger) &&
			_activeLoadoutSlot is { } activeLoadoutSlot)
		{
			_stratagemReadyToDeploy = false;
			StratagemLoadout.ActivateCooldown(button: activeLoadoutSlot);
			StartCooldownTimerIfAnyStratagemOnCooldown();

			Task.Run(action: () => StratagemLoadoutUpdated?.Invoke(sender: this, e: EventArgs.Empty));
		}

		ImmutableHashSet<ControllerButton> screenshotButtons =
		[
			ControllerButton.LeftTrigger, ControllerButton.RightTrigger, ControllerButton.ButtonY
		];
		if (state.Pressed.Intersect(other: screenshotButtons).Count == screenshotButtons.Count &&
			state.Added.Count == 1 &&
			state.Added.Single() == ControllerButton.ButtonY)
		{
			Task.Run(action: UpdateCurrentStratagems);
			return;
		}

		ImmutableHashSet<ControllerButton> weaponAssistButtons = [ControllerButton.LeftTrigger, ControllerButton.ButtonA];
		if (state.Pressed.Intersect(other: weaponAssistButtons).Count == weaponAssistButtons.Count &&
			state.Added.Count == 1 &&
			state.Added.Single() == ControllerButton.ButtonA)
		{
			Task.Run(action: AssistCurrentStratagemWeapon);
			return;
		}

		if (_stratagemMacroActivated)
		{
			return;
		}

		ImmutableHashSet<ControllerButton> faceButtons =
		[
			ControllerButton.ButtonA,
			ControllerButton.ButtonB,
			ControllerButton.ButtonX,
			ControllerButton.ButtonY
		];
		var pressedFaceButtons = state.Pressed.Intersect(other: faceButtons);
		var addedFaceButtons = state.Added.Intersect(other: faceButtons);
		if (pressedFaceButtons.Count != 1 || addedFaceButtons.Count != 1)
		{
			return;
		}

		var faceButton = pressedFaceButtons.Single();
		if (!state.Pressed.Contains(item: ControllerButton.LeftBumper))
		{
			return;
		}

		var rbPressed = state.Pressed.Contains(item: ControllerButton.RightBumper);

		if (state.Pressed.Count != (rbPressed ? 3 : 2))
		{
			return;
		}

		var standUpFirst = faceButton == ControllerButton.ButtonB;

		if (rbPressed)
		{
			ImmutableArray<StratagemButton> shiftCode = faceButton switch
			{
				ControllerButton.ButtonA =>
				[
					StratagemButton.Down,
					StratagemButton.Up,
					StratagemButton.Left,
					StratagemButton.Down,
					StratagemButton.Up,
					StratagemButton.Right,
					StratagemButton.Down,
					StratagemButton.Up
				], // Hellbomb
				ControllerButton.ButtonB =>
				[
					StratagemButton.Up,
					StratagemButton.Up,
					StratagemButton.Left,
					StratagemButton.Up,
					StratagemButton.Right
				], // Eagle Rearm
				ControllerButton.ButtonX =>
				[
					StratagemButton.Down,
					StratagemButton.Down,
					StratagemButton.Up,
					StratagemButton.Right,
					StratagemButton.Up,
					StratagemButton.Down
				], // Resupply
				ControllerButton.ButtonY =>
				[
					StratagemButton.Up,
					StratagemButton.Down,
					StratagemButton.Right,
					StratagemButton.Left,
					StratagemButton.Up
				], // Reinforce
				_ => throw new InvalidOperationException()
			};

			Task.Run(function: () => PlayMacroAsync(macro: shiftCode, standUpFirst: standUpFirst));
			_stratagemMacroActivated = true;
			return;
		}

		var slot = StratagemLoadout.GetSlot(button: faceButton);
		if (slot is not null)
		{
			_activeLoadoutSlot = faceButton;
			Task.Run(function: () => PlayMacroAsync(macro: slot.Code, standUpFirst: standUpFirst));
			_stratagemReadyToDeploy = true;
			_stratagemMacroActivated = true;
		}
	}

	private void SaveAndTriggerLoadout()
	{
		var stratagemNames = new[]
			{
				StratagemLoadout.GetSlot(button: ControllerButton.ButtonA)?.Name,
				StratagemLoadout.GetSlot(button: ControllerButton.ButtonB)?.Name,
				StratagemLoadout.GetSlot(button: ControllerButton.ButtonX)?.Name,
				StratagemLoadout.GetSlot(button: ControllerButton.ButtonY)?.Name
			}
			.Where(predicate: v => v is not null)
			.Select(selector: v => v!)
			.ToImmutableHashSet();

		File.WriteAllText(
			path: CurrentStratagemsFileName,
			contents: JsonSerializer.Serialize(value: stratagemNames, options: JsonOptions)
		);

		StratagemLoadoutUpdated?.Invoke(sender: this, e: EventArgs.Empty);
	}

	private void StartCooldownTimerIfAnyStratagemOnCooldown()
	{
		_cooldownOverTimer?.Dispose();

		var cooldown = StratagemLoadout.GetShortestCooldownTime();
		if (cooldown is null)
		{
			Log.Information(messageTemplate: "Cooldown timer stopped");
			return;
		}

		Log.Information(messageTemplate: @"Cooldown timer set to '{Time:mm\:ss}'", propertyValue: cooldown.Value);
		_cooldownOverTimer = new Timer(
			callback: CooldownTimerElapsed,
			state: null,
			dueTime: cooldown.Value,
			period: Timeout.InfiniteTimeSpan
		);
	}

	private void UpdateCurrentStratagems()
	{
		var screenshot = ScreenshotService.TakeScreenshot(saveToDisk: _debugMode);
		var stratagemIcons =
			StratagemExtractionService.ExtractStratagemIcons(screenshot: screenshot, saveToDisk: _debugMode);
		var stratagemNames = _stratagemIdentificationService
			.IdentifyStratagems(stratagemIcons: stratagemIcons)
			.Where(predicate: v => _stratagemCodes.ContainsKey(key: v))
			.ToImmutableArray();

		if (StratagemLoadout.ExistingSlots.Select(selector: v => v.Name).Intersect(second: stratagemNames).Count() != 4)
		{
			// User might have swapped some Stratagem positions, do not overwrite if they are the same (but maybe in different positions)
			UpdateLoadoutFromNames(stratagemNames: stratagemNames);
		}

		StratagemLoadout.ClearAllCooldowns();
		SaveAndTriggerLoadout();

		// A single press doesn't work for some reason...
		InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.B, press: true);
		Task.Delay(delay: DelayBeforeKeyUp).GetAwaiter().GetResult();
		InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.B, press: false);

		Task.Delay(millisecondsDelay: 250).GetAwaiter().GetResult();

		InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.B, press: true);
		Task.Delay(delay: DelayBeforeKeyUp).GetAwaiter().GetResult();
		InputSimulationService.SimulateKeyPress(virtualKeyCode: VirtualKeyCodes.B, press: false);
	}

	private void UpdateLoadoutFromNames(ImmutableArray<string> stratagemNames)
	{
		var slots = new Dictionary<ControllerButton, StratagemCode?>();

		var remainingButtons = new List<ControllerButton>
		{
			ControllerButton.ButtonY,
			ControllerButton.ButtonX,
			ControllerButton.ButtonB,
			ControllerButton.ButtonA
		};
		var remainingStratagems = stratagemNames
			.Select(selector: v => Stratagems[key: v])
			.OrderBy(
				keySelector: v => v.Category switch
				{
					StratagemCategory.Offensive => 1,
					StratagemCategory.Supply => 2,
					StratagemCategory.Defensive => 3,
					_ => throw new InvalidOperationException()
				}
			)
			.ThenBy(
				keySelector: v => v.Deployment switch
				{
					StratagemDeployment.Orbital => 1,
					StratagemDeployment.Airstrike => 2,
					StratagemDeployment.Weapon => 3,
					StratagemDeployment.Backpack => 4,
					StratagemDeployment.Ground => 5,
					_ => throw new InvalidOperationException()
				}
			)
			.ThenBy(keySelector: v => v.Position)
			.Take(count: 4)
			.ToList();

		// Assign preferred positions first
		AssignSlot(deployment: StratagemDeployment.Ground, button: ControllerButton.ButtonA);
		AssignSlot(deployment: StratagemDeployment.Backpack, button: ControllerButton.ButtonB);
		AssignSlot(deployment: StratagemDeployment.Weapon, button: ControllerButton.ButtonX);
		AssignSlot(deployment: StratagemDeployment.Orbital, button: ControllerButton.ButtonY);

		// Next backup positions
		if (remainingButtons.Contains(item: ControllerButton.ButtonY))
		{
			AssignSlot(deployment: StratagemDeployment.Airstrike, button: ControllerButton.ButtonY);
		}

		// Finally layout remaining stratagems
		foreach (var button in remainingButtons)
		{
			var stratagem = remainingStratagems.FirstOrDefault();
			slots.Add(key: button, value: stratagem);

			if (stratagem is not null)
			{
				remainingStratagems.Remove(item: stratagem);
			}
		}

		StratagemLoadout = new StratagemLoadout(
			buttonA: slots[key: ControllerButton.ButtonA],
			buttonB: slots[key: ControllerButton.ButtonB],
			buttonX: slots[key: ControllerButton.ButtonX],
			buttonY: slots[key: ControllerButton.ButtonY]
		);
		return;

		void AssignSlot(StratagemDeployment deployment, ControllerButton button)
		{
			var stratagem = remainingStratagems.FirstOrDefault(predicate: v => v.Deployment == deployment);
			if (stratagem is null)
			{
				return;
			}

			slots.Add(key: button, value: stratagem);
			remainingButtons.Remove(item: button);
			remainingStratagems.Remove(item: stratagem);
		}
	}
}