#pragma warning disable CA1707
using System.IO;
using System.Text.RegularExpressions;

namespace Spectrum.Tests;

public sealed class SpectrumArchitectureBoundaryTests
{
    [Fact]
    public void SpectrometerManager_DoesNotOwnDialogsWindowsOrSynchronousUiDispatch()
    {
        string managerPath = FindRepositoryFile("Plugins", "Spectrum", "SpectrometerManager.cs");
        string source = File.ReadAllText(managerPath);
        (string Name, string Pattern)[] forbiddenReferences =
        [
            ("WPF MessageBox", @"\bMessageBox\b"),
            ("SaveFileDialog", @"\bSaveFileDialog\b"),
            ("OpenFileDialog", @"\bOpenFileDialog\b"),
            ("PropertyEditorWindow", @"\bPropertyEditorWindow\b"),
            ("CalibrationGroupWindow", @"\bCalibrationGroupWindow\b"),
            ("synchronous Application dispatcher", @"\bApplication\s*\.\s*Current\s*\.\s*Dispatcher\s*\.\s*Invoke\s*\("),
        ];

        string[] violations = forbiddenReferences
            .Where(reference => Regex.IsMatch(source, reference.Pattern, RegexOptions.CultureInvariant))
            .Select(reference => reference.Name)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"SpectrometerManager.cs must delegate UI concerns outside the device manager. Found: {string.Join(", ", violations)}");
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine([current.FullName, .. relativeParts]);
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository file '{Path.Combine(relativeParts)}' from '{AppContext.BaseDirectory}'.");
    }
}
