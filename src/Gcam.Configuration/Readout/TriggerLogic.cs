using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Trigger truth table (RD-2). Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TriggerLogic>))]
public enum TriggerLogic
{
    /// <summary>Fires when the SUM of all channels reaches the threshold (the baseline).</summary>
    Sum,

    /// <summary>Fires when ANY channel reaches the threshold.</summary>
    Or,

    /// <summary>Fires when ALL four channels reach the threshold (four-output readout only).</summary>
    And,
}
