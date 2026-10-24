using ColorVision.Engine.Services.Devices.SMU.Dao;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Monitoring;

internal sealed record LiveIvlPoint(string Series, DateTime Time, double? Voltage, double? Current, double? Luminance, bool IsScan = false);
internal sealed record LiveIvlSnapshot(int BatchId, string Name, FlowStatus Status, DateTime Start,
    IReadOnlyList<LiveIvlPoint> Points, int Unpaired, int Invalid, bool Truncated);

// This projection deliberately excludes images and full spectra from periodic reads.
internal sealed class LiveIvlOpticalRow
{
    public int Id { get; set; }
    public int? SmuId { get; set; }
    public string? Device { get; set; }
    public int? Node { get; set; }
    public string? Poi { get; set; }
    public string? Value { get; set; }
    public DateTime? Time { get; set; }
    public float? Voltage { get; set; }
    public float? Current { get; set; }
    public double? Luminance { get; set; }
}

internal static class LiveIvlData
{
    internal const int MaximumRows = 20000;

    internal static LiveIvlSnapshot Build(MeasureBatchModel batch, IReadOnlyList<SMUResultModel> measurements,
        IReadOnlyList<SmuScanModel> scans, IReadOnlyList<LiveIvlOpticalRow> camera,
        IReadOnlyList<LiveIvlOpticalRow> spectrum, bool truncated = false)
    {
        var smu = measurements.Where(p => p.BatchId == batch.Id && p.ResultCode is null or 0)
            .OrderBy(p => p.CreateDate).ThenBy(p => p.Id).ToArray();
        var byId = smu.ToDictionary(p => p.Id);
        bool singleSource = smu.Select(SmuSeries).Distinct().Take(2).Count() == 1;
        var points = new List<LiveIvlPoint>();
        int invalid = 0, unpaired = 0;
        foreach (var p in smu)
        {
            double? voltage = Finite(p.VResult), current = Finite(p.IResult);
            if (voltage == null && current == null) { invalid++; continue; }
            points.Add(new(SmuSeries(p), p.CreateDate, voltage, current, null));
        }
        int scanBudget = MaximumRows;
        foreach (var scan in scans.Where(p => p.BatchId == batch.Id && p.ResultCode is null or 0).OrderByDescending(p => p.Id))
        {
            try
            {
                var voltages = JsonConvert.DeserializeObject<double[]>(scan.VResult ?? "[]") ?? [];
                var currents = JsonConvert.DeserializeObject<double[]>(scan.IResult ?? "[]") ?? [];
                int count = Math.Min(voltages.Length, currents.Length);
                if (voltages.Length != currents.Length) invalid++;
                truncated |= count > scanBudget;
                int start = Math.Max(0, count - scanBudget);
                scanBudget -= count - start;
                for (int i = start; i < count; i++)
                {
                    double? voltage = Finite(voltages[i]), current = Finite(currents[i] * 1000);
                    if (voltage == null && current == null) { invalid++; continue; }
                    // Scan payloads store A. There is no per-point clock: keep the actual completion time.
                    points.Add(new($"Scan {scan.DeviceCode}/{scan.ChannelType}/Z{scan.ZIndex} #{scan.Id}", scan.CreateDate,
                        voltage, current, null, true));
                }
            }
            catch (JsonException) { invalid++; }
        }
        AddOptical(camera, "Camera");
        AddOptical(spectrum, "Spectrum");
        return new(batch.Id, batch.Name ?? batch.Code ?? batch.Id.ToString(), batch.FlowStatus,
            batch.CreateDate ?? points.Select(p => p.Time).DefaultIfEmpty(DateTime.Now).Min(), points, unpaired, invalid, truncated);

        void AddOptical(IReadOnlyList<LiveIvlOpticalRow> rows, string kind)
        {
            foreach (var row in rows.OrderBy(p => p.Time).ThenBy(p => p.Id))
            {
                double? luminance = Finite(row.Luminance);
                if (luminance == null || row.Time == null) { invalid++; continue; }
                SMUResultModel? match = null;
                if (row.SmuId > 0) byId.TryGetValue(row.SmuId.Value, out match);
                double? voltage = Finite(row.Voltage) ?? Finite(match?.VResult);
                double? current = Finite(row.Current) ?? Finite(match?.IResult);
                // Old records may lack links. Only an unambiguous preceding measurement is safe.
                // Never pair by row count: a partial batch can contain different numbers of each result.
                if ((row.SmuId is null or <= 0) && voltage == null && current == null && singleSource)
                {
                    int low = 0, high = smu.Length;
                    while (low < high)
                    {
                        int mid = low + (high - low) / 2;
                        if (smu[mid].CreateDate <= row.Time.Value) low = mid + 1; else high = mid;
                    }
                    int index = low - 1;
                    if (index >= 0 && (index == 0 || smu[index - 1].CreateDate != smu[index].CreateDate))
                    {
                        match = smu[index];
                        voltage = Finite(match.VResult); current = Finite(match.IResult);
                    }
                }
                if (voltage == null || current == null) unpaired++;
                string source = match == null ? string.Empty : $" / {SmuSeries(match)}";
                points.Add(new($"{kind} {row.Device}/Z{row.Node}{(row.Poi == null ? "" : " / " + row.Poi)}{source}",
                    row.Time.Value, voltage, current, luminance));
            }
        }
    }

    private static string SmuSeries(SMUResultModel p) => $"SMU {p.DeviceCode}/{p.ChannelType}/Z{p.ZIndex}";
    private static double? Finite(double? value) => value.HasValue && double.IsFinite(value.Value) ? value : null;
}
