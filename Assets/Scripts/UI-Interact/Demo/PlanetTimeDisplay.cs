using UnityEngine;
using TMPro;

public class PlanetCalendarDisplay : MonoBehaviour
{
    [Header("References")]
    public SunRotate sun;

    [Header("UI Output (single text)")]
    public TMP_Text calendarText;
    public TMP_Text timeScaleText;

    [Header("Calendar Rules")]
    public int daysPerYear = 60;
    public int daysPerSeason = 15; // 4 seasons -> 60
    public int daysPerMonth = 5;   // 12 months -> 60

    [Header("Start Date")]
    public int startYear = 1850;
    [Range(1, 12)] public int startMonth = 1; // 1..12 (5-day months)
    [Range(1, 5)] public int startDay = 1;   // 1..5

    // Optional names (you can change these later)
    public string[] seasonNames = { "Spring", "Summer", "Fall", "Winter" };
    public string[] monthNames =
    {
        "Aster","Brine","Cinder","Dawn","Ember","Frost",
        "Gale","Haze","Iris","Jade","Kite","Lumen"
    };

    private int totalDays;          // days since start date (0+)
    private float dayAccumulator;   // seconds accumulator

    private void Awake()
    {
        totalDays = EncodeToTotalDays(startYear, startMonth, startDay);
        UpdateLabel();
    }

    private void Update()
    {
        if (sun == null || calendarText == null) return;

        float dayLength = Mathf.Max(0.0001f, sun.dayLengthSeconds);
        float dt = Time.deltaTime * sun.timeScale;

        dayAccumulator += dt;

        // handle fast-forward (50x etc)
        while (dayAccumulator >= dayLength)
        {
            dayAccumulator -= dayLength;
            totalDays++;
        }

        UpdateLabel();

        if (timeScaleText != null) timeScaleText.text = $"Time Scale: {sun.timeScale}x";
    }

    private void UpdateLabel()
    {
        DecodeFromTotalDays(totalDays,
            out int year, out int month, out int day, out int dayOfYear,
            out int seasonIndex, out int dayOfSeason);

        string monthStr = (monthNames != null && monthNames.Length >= (daysPerYear / daysPerMonth))
            ? monthNames[month - 1]
            : $"M{month}";

        string seasonStr = (seasonNames != null && seasonNames.Length >= (daysPerYear / daysPerSeason))
            ? seasonNames[seasonIndex]
            : $"S{seasonIndex + 1}";

        // Example formats (pick one):
        // calendarText.text = $"{month}/{day}/{year}";
        if (calendarText != null)
        {
            calendarText.text = $"{monthStr} {day}, {year}  •  {seasonStr} {dayOfSeason + 1}/{daysPerSeason}";
        }
        // Or: calendarText.text = $"Y{year}  D{dayOfYear + 1}/{daysPerYear}";
    }

    // ----- Conversion helpers -----

    // Encodes a starting Y/M/D into the total-day counter
    private int EncodeToTotalDays(int year, int month, int day)
    {
        // month: 1..12, day: 1..5
        month = Mathf.Clamp(month, 1, 12);
        day = Mathf.Clamp(day, 1, daysPerMonth);

        int yearsOffset = 0; // if you want "year 0" start, change this
        int dayOfYear = (month - 1) * daysPerMonth + (day - 1);

        // totalDays from some epoch: year * daysPerYear + dayOfYear
        return (year - startYear + yearsOffset) * daysPerYear + dayOfYear;
    }

    // Decodes totalDays -> Y/M/D + season info
    private void DecodeFromTotalDays(int t,
        out int year, out int month, out int day, out int dayOfYear,
        out int seasonIndex, out int dayOfSeason)
    {
        if (t < 0) t = 0;

        int yearsSinceStart = t / daysPerYear;
        dayOfYear = t % daysPerYear;

        year = startYear + yearsSinceStart;

        month = (dayOfYear / daysPerMonth) + 1;         // 1..12
        day = (dayOfYear % daysPerMonth) + 1;           // 1..5

        seasonIndex = Mathf.Clamp(dayOfYear / daysPerSeason, 0, 3); // 0..3
        dayOfSeason = dayOfYear % daysPerSeason;        // 0..14
    }

    // Optional: allow other systems to jump date
    public void SetDate(int year, int month, int day)
    {
        // Convert to absolute day count relative to startYear
        int yearsSinceStart = year - startYear;
        int dayOfYear = (Mathf.Clamp(month, 1, 12) - 1) * daysPerMonth + (Mathf.Clamp(day, 1, daysPerMonth) - 1);
        totalDays = Mathf.Max(0, yearsSinceStart * daysPerYear + dayOfYear);

        dayAccumulator = 0f;
        UpdateLabel();
    }
}
