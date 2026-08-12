using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Correction;

/// <summary>
/// Managed representation of the native Magiude.dat binary format.
/// </summary>
public sealed class MagnitudeCalibrationFile
{
    private const int HeaderSize = sizeof(ulong) + sizeof(float) + sizeof(int) + sizeof(ulong);
    private readonly double[] _wavelengths;
    private readonly double[] _coefficients;

    private MagnitudeCalibrationFile(
        ulong dataLength,
        float exposureTime,
        int luminanceCoefficient,
        double[] wavelengths,
        double[] coefficients,
        string? sourcePath)
    {
        DataLength = dataLength;
        ExposureTime = exposureTime;
        LuminanceCoefficient = luminanceCoefficient;
        _wavelengths = wavelengths;
        _coefficients = coefficients;
        Wavelengths = Array.AsReadOnly(_wavelengths);
        Coefficients = Array.AsReadOnly(_coefficients);
        SourcePath = sourcePath;
    }

    public ulong DataLength { get; }
    public float ExposureTime { get; }
    public int LuminanceCoefficient { get; }
    public int Count => _wavelengths.Length;
    public ReadOnlyCollection<double> Wavelengths { get; }
    public ReadOnlyCollection<double> Coefficients { get; }
    public string? SourcePath { get; }

    public static MagnitudeCalibrationFile Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);

        using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < HeaderSize)
            throw new InvalidDataException($"Magnitude calibration file is too small: {stream.Length} bytes.");

        using BinaryReader reader = new(stream);
        ulong dataLength = reader.ReadUInt64();
        if (dataLength != checked((ulong)stream.Length))
            throw new InvalidDataException($"Header length {dataLength} does not match file length {stream.Length}.");

        float exposureTime = reader.ReadSingle();
        int luminanceCoefficient = reader.ReadInt32();
        ulong countValue = reader.ReadUInt64();
        if (countValue < 2 || countValue > int.MaxValue)
            throw new InvalidDataException($"Invalid magnitude calibration point count: {countValue}.");

        ulong expectedLength;
        try
        {
            expectedLength = checked((ulong)HeaderSize + countValue * 2UL * sizeof(double));
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("Magnitude calibration point count overflows the file format.", ex);
        }

        if (dataLength != expectedLength)
            throw new InvalidDataException($"Point count {countValue} requires {expectedLength} bytes, but the file contains {dataLength} bytes.");

        int count = checked((int)countValue);
        double[] wavelengths = ReadValues(reader, count, "wavelength");
        double[] coefficients = ReadValues(reader, count, "coefficient");

        ValidateHeader(exposureTime);
        ValidateWavelengths(wavelengths);
        ValidateCoefficients(coefficients);

        return new MagnitudeCalibrationFile(dataLength, exposureTime, luminanceCoefficient, wavelengths, coefficients, fullPath);
    }

    public static MagnitudeCalibrationFile Create(
        float exposureTime,
        int luminanceCoefficient,
        IReadOnlyList<double> wavelengths,
        IReadOnlyList<double> coefficients)
    {
        ArgumentNullException.ThrowIfNull(wavelengths);
        ArgumentNullException.ThrowIfNull(coefficients);
        if (wavelengths.Count != coefficients.Count)
            throw new ArgumentException("Wavelength and coefficient counts must match.");
        if (wavelengths.Count < 2)
            throw new ArgumentException("At least two magnitude calibration points are required.", nameof(wavelengths));

        ValidateHeader(exposureTime);
        double[] wavelengthValues = wavelengths.ToArray();
        double[] coefficientValues = coefficients.ToArray();
        ValidateWavelengths(wavelengthValues);
        ValidateCoefficients(coefficientValues);

        ulong dataLength = checked((ulong)HeaderSize + (ulong)wavelengthValues.Length * 2UL * sizeof(double));
        return new MagnitudeCalibrationFile(dataLength, exposureTime, luminanceCoefficient, wavelengthValues, coefficientValues, null);
    }

    public MagnitudeCalibrationFile WithCoefficients(IReadOnlyList<double> coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        if (coefficients.Count != Count)
            throw new ArgumentException($"Expected {Count} coefficients, but received {coefficients.Count}.", nameof(coefficients));

        double[] values = coefficients.ToArray();
        ValidateCoefficients(values);
        return new MagnitudeCalibrationFile(
            DataLength,
            ExposureTime,
            LuminanceCoefficient,
            (double[])_wavelengths.Clone(),
            values,
            SourcePath);
    }

    /// <summary>
    /// Writes a new file and refuses to replace any existing path, including the source file.
    /// </summary>
    public string SaveNew(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        string fullPath = Path.GetFullPath(outputPath);
        if (SourcePath != null && string.Equals(fullPath, SourcePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The source magnitude calibration file cannot be overwritten.");

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Output directory does not exist: {directory}");

        using FileStream stream = new(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(stream);
        writer.Write(DataLength);
        writer.Write(ExposureTime);
        writer.Write(LuminanceCoefficient);
        writer.Write(checked((ulong)Count));
        WriteValues(writer, _wavelengths);
        WriteValues(writer, _coefficients);
        writer.Flush();

        if (stream.Length != checked((long)DataLength))
            throw new IOException($"Written magnitude calibration length {stream.Length} does not match expected length {DataLength}.");

        return fullPath;
    }

    private static double[] ReadValues(BinaryReader reader, int count, string valueName)
    {
        double[] values = new double[count];
        try
        {
            for (int index = 0; index < count; index++)
                values[index] = reader.ReadDouble();
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException($"Magnitude calibration {valueName} data is truncated.", ex);
        }
        return values;
    }

    private static void WriteValues(BinaryWriter writer, double[] values)
    {
        for (int index = 0; index < values.Length; index++)
            writer.Write(values[index]);
    }

    private static void ValidateHeader(float exposureTime)
    {
        if (!float.IsFinite(exposureTime) || exposureTime <= 0)
            throw new InvalidDataException($"Invalid magnitude calibration exposure time: {exposureTime}.");
    }

    private static void ValidateWavelengths(double[] wavelengths)
    {
        for (int index = 0; index < wavelengths.Length; index++)
        {
            double value = wavelengths[index];
            if (!double.IsFinite(value))
                throw new InvalidDataException($"Wavelength at index {index} is not finite.");
            if (index > 0 && value <= wavelengths[index - 1])
                throw new InvalidDataException($"Wavelengths must be strictly increasing; index {index} contains {value}.");
        }
    }

    private static void ValidateCoefficients(double[] coefficients)
    {
        for (int index = 0; index < coefficients.Length; index++)
        {
            if (!double.IsFinite(coefficients[index]) || coefficients[index] < 0)
                throw new InvalidDataException($"Coefficient at index {index} must be finite and non-negative.");
        }
    }
}
