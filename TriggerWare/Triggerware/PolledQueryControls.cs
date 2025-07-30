using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Triggerware;

/// <summary>
///     A helper class for controlling how a polled query's data is sent back.
/// </summary>
/// <param name="reportUnchanged">Whether to bother sending an empty notification when no change is detected.</param>
/// <param name="reportInitial">Whether to send the full initial query first before beginning polling.</param>
/// <param name="delay">
///     If true, will make a polled query wait to begin polling until an initial state is set with
///     <see cref="PolledQuery{T}.Poll" />.
/// </param>
public class PolledQueryControlParameters(bool reportUnchanged, bool delay, bool reportInitial)
{
    public bool ReportUnchanged { get; set; } = reportUnchanged;
    public bool Delay { get; set; } = delay;
    public bool ReportInitial { get; set; } = reportInitial;
}

/// <summary>
///     A list of polled query schedules to be passed to a polled query. A polled query may have any number of schedules,
///     so PolledQuerySchedules provide means of adding schedules via the Add and With methods. The schedules array may be
///     integers,representing a time interval in seconds, or <see cref="PolledQueryCalendarSchedule">
///     PolledQueryCalendarSchedule</see>, representing a calendar schedule.
/// </summary>
[JsonConverter(typeof(PolledQueryScheduleConverter))]
public class PolledQuerySchedule
{
    private readonly List<object?> _schedules = [];

    public PolledQuerySchedule()
    {
        _schedules = [];
    }

    /// <param name="interval"></param>
    public PolledQuerySchedule(int interval)
    {
        _schedules = [interval];
    }

    public PolledQuerySchedule(PolledQueryCalendarSchedule calendarSchedule)
    {
        _schedules = [calendarSchedule];
    }

    public object?[] Schedules => _schedules.ToArray();

    public void Add(PolledQueryCalendarSchedule calendarSchedule)
    {
        _schedules.Add(calendarSchedule);
    }

    public void Add(int interval)
    {
        _schedules.Add(interval);
    }

    public PolledQuerySchedule With(PolledQueryCalendarSchedule schedule)
    {
        _schedules.Add(schedule);
        return this;
    }

    public PolledQuerySchedule With(int interval)
    {
        _schedules.Add(interval);
        return this;
    }
}

public class PolledQueryScheduleConverter : JsonConverter<PolledQuerySchedule>
{
    public override PolledQuerySchedule? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions? options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected a JSON array.");

        var schedules = new List<object?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) break;

            if (reader.TokenType == JsonTokenType.Number)
                schedules.Add(reader.GetInt32()); // Example: Reading integers
            else if (reader.TokenType == JsonTokenType.StartObject)
                schedules.Add(JsonSerializer.Deserialize<PolledQueryCalendarSchedule>(ref reader, options));
            else
                throw new JsonException("Unexpected token in array.");
        }

        var pqs = new PolledQuerySchedule();
        foreach (var schedule in schedules)
            if (schedule is int i)
                pqs.Add(i);
            else if (schedule is PolledQueryCalendarSchedule pcs)
                pqs.Add(pcs);

        return pqs;
    }

    public override void Write(Utf8JsonWriter writer, PolledQuerySchedule value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var schedule in value.Schedules) JsonSerializer.Serialize(writer, schedule, options);
        writer.WriteEndArray();
    }
}

/// <summary>
///     Represents a schedule which to run the query by. A polled query with this schedule will perform a polling operation
///     for each time in the schedule.
///     <para></para>
///     A time YYYY-MM-DD hh:mm  is 'in' the schedule iff MM is in the specified set of months, DD is in the set of
///     specified days, hh is in the set of specified hours, mm is in the set of specified minutes, and the day of the week
///     of that time is in the specified set of weekdays.
///     <para></para>
///     For each field (other than of course the timezone), the value may be a comma separated list of values. It may also
///     be a range of values separated by a hyphen.
///     For example, the Hours field may be "1-5,7,9-12".
///     <para></para>
///     Alternatiavely, these fields may be set to "*" (which is the default) to indicate all values are valid.
/// </summary>
public class PolledQueryCalendarSchedule
{
    private static readonly Regex _timezoneRegex = new("^[A-Za-z]+(?:_[A-Za-z]+)*(?:/[A-Za-z]+(?:_[A-Za-z]+)*)*$");
    private string _days;
    private string _hours;
    private string _minutes;
    private string _months;
    private string _timezone;
    private string _weekdays;

    /// <summary>
    ///     The name of a TZ database timezone.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("timezone")]
    public string? Timezone
    {
        get => _timezone;
        set
        {
            if (value != null && !_timezoneRegex.IsMatch(value))
                throw new PolledQueryScheduleException("Timezone name is invalid.");
        }
    }

    /// <summary>
    ///     Minutes, constrained from 0 to 59.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("minutes")]
    public string? Minutes
    {
        get => _minutes;
        set
        {
            if (value == null)
            {
                _minutes = "*";
            }
            else
            {
                ValidateTime("Minutes", value, 0, 59);
                _minutes = value;
            }
        }
    }

    /// <summary>
    ///     Hours, constrained from 0 to 23.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("hours")]
    public string? Hours
    {
        get => _hours;
        set
        {
            if (value == null)
            {
                _hours = "*";
            }
            else
            {
                ValidateTime("Hours", value, 0, 23);
                _hours = value;
            }
        }
    }

    /// <summary>
    ///     Days, constrained from 1 to 31.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("days")]
    public string? Days
    {
        get => _days;
        set
        {
            if (value == null)
            {
                _days = "*";
            }
            else
            {
                ValidateTime("Days", value, 1, 31);
                _days = value;
            }
        }
    }

    /// <summary>
    ///     Months, constrained from 1 to 12.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("months")]
    public string? Months
    {
        get => _months;
        set
        {
            if (value == null)
            {
                _months = "*";
            }
            else
            {
                ValidateTime("Months", value, 1, 12);
                _months = value;
            }
        }
    }

    /// <summary>
    ///     Weekdays, constrained from 0 to 6.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("weekdays")]
    public string? Weekdays
    {
        get => _weekdays;
        set
        {
            if (value == null)
            {
                _weekdays = "*";
            }
            else
            {
                ValidateTime("Weekdays", value, 0, 6);
                _weekdays = value;
            }
        }
    }

    /// <summary>
    ///     Ensures that the schedule is valid in accordance with the guidelines of a
    ///     <see cref="PolledQueryCalendarSchedule" />.
    /// </summary>
    /// <returns>A copy of this calendar schedule.</returns>
    /// <exception cref="PolledQueryScheduleException">When a field invalid.</exception>
    public PolledQueryCalendarSchedule Validated()
    {
        if (Minutes != null) ValidateTime("Minutes", Minutes, 0, 59);
        if (Hours != null) ValidateTime("Hours", Hours, 0, 23);
        if (Days != null) ValidateTime("Days", Days, 1, 31);
        if (Months != null) ValidateTime("Months", Months, 1, 12);
        if (Weekdays != null) ValidateTime("Weekdays", Weekdays, 0, 6);
        if (Timezone != null && !_timezoneRegex.IsMatch(Timezone))
            throw new PolledQueryScheduleException("Timezone name is invalid.");

        return this;
    }

    /// <exception cref="PolledQueryScheduleException">When the time is invalid.</exception>
    private void ValidateTime(string name, string amount, int min, int max)
    {
        var parts = amount.Split(",").SelectMany(x => x.Split('-')).ToArray();
        foreach (var p in parts)
        {
            if (p == "*") continue;
            try
            {
                var parsed = int.Parse(p);
                if (parsed < 0 || parsed > max)
                    throw new PolledQueryScheduleException($"{name} must be between {min} and {max}.");
            }
            catch (FormatException e)
            {
                throw new PolledQueryScheduleException($"{name} must be an integer or '*'.");
            }
        }
    }
}

public class PolledQueryScheduleException(string message) : Exception(message)
{
}