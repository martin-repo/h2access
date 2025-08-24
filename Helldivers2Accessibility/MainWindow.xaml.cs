// -------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using Helldivers2Accessibility.Models;

namespace Helldivers2Accessibility;

public sealed partial class MainWindow : IDisposable
{
	private const bool DebugMode = false;

	private readonly SolidColorBrush _availableBrush;
	private readonly DispatcherTimer _clockTimer;
	private readonly ControllerService _controllerService = new();
	private readonly Timer _cooldownTimer;
	private readonly SolidColorBrush _highlightBrush;
	private readonly StratagemIdentificationService _stratagemIdentificationService = new();
	private readonly StratagemService _stratagemService;

	private ControllerButton _stratagemReplaceButton;

	public MainWindow()
	{
		InitializeComponent();

		_availableBrush = new SolidColorBrush(color: Colors.MediumSeaGreen);
		_highlightBrush = new SolidColorBrush(color: Colors.Orange);

		_stratagemService = new StratagemService(
			debugMode: DebugMode,
			controllerService: _controllerService,
			stratagemIdentificationService: _stratagemIdentificationService
		);

		_stratagemService.Initialize();
		_stratagemService.StratagemLoadoutUpdated += OnStratagemLoadoutUpdated;

		_stratagemIdentificationService.Initialize();

		UpdateStratagemImages();
		PopulateStratagems();

		_clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes: 1) };
		_clockTimer.Tick += ClockTimer_Tick;
		_clockTimer.Start();
		UpdateClock();

		_cooldownTimer = new Timer(
			callback: CooldownTimerCallback,
			state: null,
			dueTime: TimeSpan.Zero,
			period: TimeSpan.FromSeconds(seconds: 1)
		);
	}

	private ImmutableArray<StackPanel> Stratagems =>
		OffensiveStratagemList
			.Children.OfType<StackPanel>()
			.Concat(second: SupplyStratagemList.Children.OfType<StackPanel>())
			.Concat(second: DefensiveStratagemList.Children.OfType<StackPanel>())
			.ToImmutableArray();

	public void Dispose()
	{
		_controllerService.Dispose();
		_stratagemIdentificationService.Dispose();
		_stratagemService.Dispose();
		_cooldownTimer.Dispose();

		foreach (var stratagem in Stratagems)
		{
			stratagem.MouseLeftButtonDown -= Stratagem_Click;
		}
	}

	public void UpdateStatus(string message)
	{
		if (Dispatcher.CheckAccess())
		{
			StatusText.Text = message;
		}
		else
		{
			Dispatcher.Invoke(callback: () => StatusText.Text = message);
		}
	}

	private static ControllerButton GetButton(string buttonName) =>
		buttonName switch
		{
			"NormalA" => ControllerButton.ButtonA,
			"NormalB" => ControllerButton.ButtonB,
			"NormalX" => ControllerButton.ButtonX,
			"NormalY" => ControllerButton.ButtonY,
			_ => throw new InvalidOperationException()
		};

	private static string? GetStratagemNameFromMouseOver() =>
		Mouse.DirectlyOver is Image image &&
		VisualTreeHelper.GetParent(reference: image) is Border border &&
		VisualTreeHelper.GetParent(reference: border) is StackPanel stackPanel
			? stackPanel.Tag as string
			: null;

	private void ClockTimer_Tick(object? sender, EventArgs e) => UpdateClock();

	private void CooldownTimerCallback(object? state) => Dispatcher.Invoke(callback: UpdateCooldownTimers);

	private void HandleStratagemSelection(StackPanel clickedButton)
	{
		_stratagemReplaceButton = GetButton(buttonName: clickedButton.Name);
		var selectedStratagemName = _stratagemService.StratagemLoadout.GetSlot(button: _stratagemReplaceButton)?.Name;

		foreach (var stratagem in Stratagems)
		{
			var stratagemName = (string)stratagem.Tag;
			var border = stratagem.Children.OfType<Border>().Single();

			if (stratagemName == selectedStratagemName)
			{
				border.BorderBrush = _highlightBrush;
				border.BorderThickness = new Thickness(uniformLength: 3);
			}
			else
			{
				border.BorderBrush = (Brush)FindResource(resourceKey: "MaterialDesignDivider");
				border.BorderThickness = new Thickness(uniformLength: 2);
			}

			StratagemSelectorOverlay.Visibility = Visibility.Visible;
		}
	}

	private void NormalButton_Click(object sender, MouseButtonEventArgs _)
	{
		if (sender is StackPanel clickedButton)
		{
			HandleStratagemSelection(clickedButton: clickedButton);
		}
	}

	private void OnLoadoutMouseMove(object sender, MouseEventArgs eventArgs) =>
		LoadoutStratagemsHeader.Text = GetStratagemNameFromMouseOver() ?? "Loadout Stratagems (LB)";

	private void OnMissionMouseMove(object sender, MouseEventArgs eventArgs) =>
		MissionStratagemsHeader.Text = GetStratagemNameFromMouseOver() ?? "Mission Stratagems (LB + RB)";

	private void OnOverlayMouseMove(object sender, MouseEventArgs eventArgs) =>
		StratagemOverlayHeader.Text = GetStratagemNameFromMouseOver() ?? "Select Stratagem";

	private void OnStratagemLoadoutUpdated(object? sender, EventArgs _)
	{
		if (Dispatcher.CheckAccess())
		{
			UpdateStratagemImages();
		}
		else
		{
			Dispatcher.Invoke(callback: UpdateStratagemImages);
		}
	}

	private void PopulateStratagems()
	{
		foreach (var stratagem in _stratagemService.Stratagems.Values.OrderBy(keySelector: v => v.Position))
		{
			AddStratagem(
				stratagem: stratagem,
				list: stratagem.Category switch
				{
					StratagemCategory.Offensive => OffensiveStratagemList,
					StratagemCategory.Supply => SupplyStratagemList,
					StratagemCategory.Defensive => DefensiveStratagemList,
					_ => throw new InvalidOperationException()
				}
			);
		}

		return;

		void AddStratagem(StratagemCode stratagem, WrapPanel list)
		{
			var stackPanel = new StackPanel
			{
				Margin = new Thickness(uniformLength: 10),
				Cursor = Cursors.Hand,
				Tag = stratagem.Name
			};

			var border = new Border
			{
				Width = 80,
				Height = 80,
				CornerRadius = new CornerRadius(uniformRadius: 8),
				Background = (Brush)FindResource(resourceKey: "MaterialDesignCardBackground"),
				BorderBrush = (Brush)FindResource(resourceKey: "MaterialDesignDivider"),
				BorderThickness = new Thickness(uniformLength: 2)
			};

			var image = new Image
			{
				Source = ImageUtilities.LoadImageFromFile(
					path: System.IO.Path.Combine(path1: "StratagemImages", path2: $"{stratagem.Name}.png")
				),
				Width = 70,
				Height = 70,
				Stretch = Stretch.Uniform
			};

			stackPanel.MouseLeftButtonDown += Stratagem_Click;
			border.Child = image;
			stackPanel.Children.Add(element: border);

			list.Children.Add(element: stackPanel);
		}
	}

	private void Stratagem_Click(object sender, MouseButtonEventArgs _)
	{
		if (sender is StackPanel clickedButton)
		{
			StratagemSelectorOverlay.Visibility = Visibility.Collapsed;
			_stratagemService.ReplaceStratagem(button: _stratagemReplaceButton, stratagemName: (string)clickedButton.Tag);
		}
	}

	private void UpdateBorders()
	{
		UpdateBorder(border: BorderNormalA, button: ControllerButton.ButtonA);
		UpdateBorder(border: BorderNormalB, button: ControllerButton.ButtonB);
		UpdateBorder(border: BorderNormalX, button: ControllerButton.ButtonX);
		UpdateBorder(border: BorderNormalY, button: ControllerButton.ButtonY);

		return;

		void UpdateBorder(Border border, ControllerButton button)
		{
			var slot = _stratagemService.StratagemLoadout.GetSlot(button: button);
			var isOnCooldown = _stratagemService.StratagemLoadout.IsOnCooldown(button: button);
			if (slot is null || isOnCooldown)
			{
				border.BorderBrush = (Brush)FindResource(resourceKey: "MaterialDesignDivider");
				border.BorderThickness = new Thickness(uniformLength: 2);
				return;
			}

			border.BorderBrush = _availableBrush;
			border.BorderThickness = new Thickness(uniformLength: 3);
		}
	}

	private void UpdateClock() => ClockText.Text = DateTime.Now.ToString(format: "HH:mm");

	private void UpdateCooldownTimers()
	{
		var remainingCooldowns = _stratagemService.StratagemLoadout.GetRemainingCooldowns();
		UpdateTimer(
			timerBackground: NormalATimerBackground,
			timerForeground: NormalATimerForeground,
			remainingCooldown: remainingCooldowns[key: ControllerButton.ButtonA]
		);
		UpdateTimer(
			timerBackground: NormalBTimerBackground,
			timerForeground: NormalBTimerForeground,
			remainingCooldown: remainingCooldowns[key: ControllerButton.ButtonB]
		);
		UpdateTimer(
			timerBackground: NormalXTimerBackground,
			timerForeground: NormalXTimerForeground,
			remainingCooldown: remainingCooldowns[key: ControllerButton.ButtonX]
		);
		UpdateTimer(
			timerBackground: NormalYTimerBackground,
			timerForeground: NormalYTimerForeground,
			remainingCooldown: remainingCooldowns[key: ControllerButton.ButtonY]
		);

		return;

		void UpdateTimer(Rectangle timerBackground, TextBlock timerForeground, int? remainingCooldown)
		{
			if (remainingCooldown is null)
			{
				timerBackground.Visibility = Visibility.Collapsed;
				timerForeground.Visibility = Visibility.Collapsed;
			}
			else
			{
				timerBackground.Visibility = Visibility.Visible;
				timerForeground.Visibility = Visibility.Visible;
				timerForeground.Text = remainingCooldown.ToString();
			}
		}
	}

	private void UpdateStratagemImages()
	{
		UpdateButton(stackPanel: NormalA, image: ImageNormalA, button: ControllerButton.ButtonA);
		UpdateButton(stackPanel: NormalB, image: ImageNormalB, button: ControllerButton.ButtonB);
		UpdateButton(stackPanel: NormalX, image: ImageNormalX, button: ControllerButton.ButtonX);
		UpdateButton(stackPanel: NormalY, image: ImageNormalY, button: ControllerButton.ButtonY);

		UpdateBorders();

		return;

		void UpdateButton(StackPanel stackPanel, Image image, ControllerButton button)
		{
			var slot = _stratagemService.StratagemLoadout.GetSlot(button: button);
			stackPanel.Tag = slot?.Name;
			image.Source = _stratagemService.StratagemLoadout.GetImage(button: button);
		}
	}
}