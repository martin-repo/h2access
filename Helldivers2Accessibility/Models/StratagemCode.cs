// -------------------------------------------------------------------------------------------------
// <copyright file="StratagemCode.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.IO;
using System.Text.Json.Serialization;

namespace Helldivers2Accessibility.Models;

public sealed record StratagemCode(
	string Name,
	ImmutableArray<StratagemButton> Code,
	StratagemCategory Category,
	int Position,
	StratagemDeployment Deployment,
	[property: JsonPropertyName(name: "deploymentSeconds")]
	[property: JsonConverter(converterType: typeof(TimeSpanSecondsConverter))]
	TimeSpan DeploymentTime,
	[property: JsonPropertyName(name: "cooldownSeconds")]
	[property: JsonConverter(converterType: typeof(TimeSpanSecondsConverter))]
	TimeSpan CooldownTime
)
{
	public string StratagemFileName => Path.Combine(path1: "StratagemImages", path2: $"{Name}.png");
}