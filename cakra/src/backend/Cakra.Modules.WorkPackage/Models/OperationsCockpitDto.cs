using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cakra.Modules.WorkPackage.Models;

/// <summary>
/// Root read model representing the Executive Operations Cockpit dataset (Architecture CR-025 §4 TD-005).
/// Provides macro portfolio metrics, the 2D Pressure × Health triage matrix distribution,
/// and comprehensive Work Package telemetry records in a single query payload.
/// </summary>
public sealed record OperationsCockpitDto
{
    /// <summary>High-level portfolio aggregate metrics and capacity indicators.</summary>
    public PortfolioMetricsDto PortfolioMetrics { get; init; } = new();

    /// <summary>2D Pressure × Health distribution matrix with row and column aggregates.</summary>
    public PressureHealthMatrixDto Matrix { get; init; } = new();

    /// <summary>Complete collection of calculated telemetry models for active Work Packages.</summary>
    public IReadOnlyList<WorkPackageTelemetryDto> Packages { get; init; } = Array.Empty<WorkPackageTelemetryDto>();
}

/// <summary>
/// Macro portfolio metrics displayed in the top ribbon of the Operations Cockpit (Architecture CR-025 §4 TD-005, §4.1).
/// </summary>
public sealed record PortfolioMetricsDto
{
    /// <summary>Total count of active Work Packages in the portfolio.</summary>
    public int TotalActiveWorkPackages { get; init; }

    /// <summary>Count of active Work Packages with an established target deadline.</summary>
    public int WithDeadlineCount { get; init; }

    /// <summary>Count of active Work Packages operating without a target deadline (UNPLANNED).</summary>
    public int WithoutDeadlineCount { get; init; }

    /// <summary>Demonstrated organization 30-day daily throughput baseline (C_org).</summary>
    public double OrgDailyThroughput { get; init; }

    /// <summary>
    /// Aggregate portfolio commitment load percentage calculated as
    /// (sum of Required Daily Burn of dated WPs / C_org) * 100%.
    /// </summary>
    public double PortfolioAggregateLoadPercentage { get; init; }

    /// <summary>Total count of Work Packages violating at least one operational health invariant.</summary>
    public int TotalInvariantViolationsCount { get; init; }
}

/// <summary>
/// 2D Pressure × Health triage matrix distributing active Work Packages across demand density and health states
/// (Architecture CR-025 §4 TD-005, §4.2).
/// </summary>
[JsonConverter(typeof(PressureHealthMatrixJsonConverter))]
public sealed record PressureHealthMatrixDto
{
    /// <summary>
    /// Nested dictionary mapping Row (HealthState) -> Column (PressureTier) -> Count.
    /// Rows: DEADLINE_BREACHED, ACTIVE_BLOCKERS, DORMANT, WIP_STAGNANT, FLOWING.
    /// Cols: UNPLANNED, NOMINAL, ELEVATED, CRITICAL, IMPOSSIBLE.
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> Cells { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Aggregate package count for each health state row.</summary>
    public Dictionary<string, int> RowTotals { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Aggregate package count for each pressure tier column.</summary>
    public Dictionary<string, int> ColumnTotals { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Custom System.Text.Json converter for <see cref="PressureHealthMatrixDto"/> to preserve canonical
/// uppercase matrix keys (Architecture CR-025 §4 TD-005, TD-006) regardless of global DictionaryKeyPolicy settings,
/// while providing case-insensitive deserialization.
/// </summary>
public sealed class PressureHealthMatrixJsonConverter : JsonConverter<PressureHealthMatrixDto>
{
    public override PressureHealthMatrixDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token, got {reader.TokenType}.");
        }

        var cells = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var rowTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var colTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new PressureHealthMatrixDto
                {
                    Cells = cells,
                    RowTotals = rowTotals,
                    ColumnTotals = colTotals
                };
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = reader.GetString();
                reader.Read();

                if (string.Equals(propName, "cells", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    foreach (var rowProp in doc.RootElement.EnumerateObject())
                    {
                        var inner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        foreach (var colProp in rowProp.Value.EnumerateObject())
                        {
                            inner[colProp.Name] = colProp.Value.GetInt32();
                        }
                        cells[rowProp.Name] = inner;
                    }
                }
                else if (string.Equals(propName, "rowTotals", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        rowTotals[prop.Name] = prop.Value.GetInt32();
                    }
                }
                else if (string.Equals(propName, "columnTotals", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        colTotals[prop.Name] = prop.Value.GetInt32();
                    }
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new PressureHealthMatrixDto
        {
            Cells = cells,
            RowTotals = rowTotals,
            ColumnTotals = colTotals
        };
    }

    public override void Write(Utf8JsonWriter writer, PressureHealthMatrixDto value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("cells");
        writer.WriteStartObject();
        if (value.Cells != null)
        {
            foreach (var row in value.Cells)
            {
                writer.WritePropertyName(row.Key);
                writer.WriteStartObject();
                if (row.Value != null)
                {
                    foreach (var col in row.Value)
                    {
                        writer.WritePropertyName(col.Key);
                        writer.WriteNumberValue(col.Value);
                    }
                }
                writer.WriteEndObject();
            }
        }
        writer.WriteEndObject();

        writer.WritePropertyName("rowTotals");
        writer.WriteStartObject();
        if (value.RowTotals != null)
        {
            foreach (var kvp in value.RowTotals)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteNumberValue(kvp.Value);
            }
        }
        writer.WriteEndObject();

        writer.WritePropertyName("columnTotals");
        writer.WriteStartObject();
        if (value.ColumnTotals != null)
        {
            foreach (var kvp in value.ColumnTotals)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteNumberValue(kvp.Value);
            }
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
