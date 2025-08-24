// -------------------------------------------------------------------------------------------------
// <copyright file="App.xaml.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Windows;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Helldivers2Accessibility;

public partial class App
{
	protected override void OnExit(ExitEventArgs eventArgs)
	{
		Log.Information(messageTemplate: "Application shutting down");
		Log.CloseAndFlush();

		base.OnExit(e: eventArgs);
	}

	protected override void OnStartup(StartupEventArgs eventArgs)
	{
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.Enrich.With<UtcTimestampEnricher>()
			.WriteTo.Console(outputTemplate: "[{UtcTimestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
			.CreateLogger();
		Log.Information(messageTemplate: "Application starting up");

		base.OnStartup(e: eventArgs);
	}
}

public class UtcTimestampEnricher : ILogEventEnricher
{
	public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
		logEvent.AddPropertyIfAbsent(
			property: propertyFactory.CreateProperty(name: "UtcTimestamp", value: DateTimeOffset.UtcNow)
		);
}