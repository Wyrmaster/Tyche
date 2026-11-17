using System.Diagnostics;

namespace Tyche.Service.Test.Support;

public static class TestImages
{
  public static bool MagickAvailable { get; } = DetectMagick();

  /// <summary>True in CI or with REQUIRE_MAGICK=1: a missing ImageMagick then fails the tests.</summary>
  public static bool MagickRequired { get; } =
    Environment.GetEnvironmentVariable("REQUIRE_MAGICK") == "1"
    || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

  /// <summary>Creates a solid color image (any extension ImageMagick knows) with the given size.</summary>
  public static string Create(string path, int width, int height, string color = "red")
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    Magick("-size", $"{width}x{height}", color.Contains(':') ? color : $"xc:{color}", path);
    return path;
  }

  public static string CreateAnimatedGif(string path, int width, int height)
  {
    Magick("-size", $"{width}x{height}", "xc:red", "(", "-size", $"{width}x{height}", "xc:blue", ")",
      "-delay", "20", "-loop", "0", path);
    return path;
  }

  public static string CreateTransparentPng(string path, int width, int height)
  {
    Magick("-size", $"{width}x{height}", "xc:none", "-fill", "red", "-draw", "rectangle 0,0 10,10", path);
    return path;
  }

  /// <summary>Returns format, size and frame count as reported by ImageMagick for the given bytes.</summary>
  public static (string Format, int Width, int Height, int Frames) Identify(byte[] bytes)
  {
    var file = Path.Combine(Path.GetTempPath(), "tyche-id-" + Guid.NewGuid().ToString("N"));
    File.WriteAllBytes(file, bytes);
    try
    {
      var output = Magick("identify", "-format", "%m %w %h\n", file);
      var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      var parts = lines[0].Split(' ');
      return (parts[0], int.Parse(parts[1]), int.Parse(parts[2]), lines.Length);
    }
    finally
    {
      File.Delete(file);
    }
  }

  private static string Magick(params string[] arguments)
  {
    var info = new ProcessStartInfo("magick") { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments)
    {
      info.ArgumentList.Add(argument);
    }

    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
      throw new InvalidOperationException($"magick failed: {error}");
    }

    return output;
  }

  private static bool DetectMagick()
  {
    try
    {
      using var process = Process.Start(new ProcessStartInfo("magick", "-version")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      })!;
      process.StandardOutput.ReadToEnd();
      process.WaitForExit();
      return process.ExitCode == 0;
    }
    catch (System.ComponentModel.Win32Exception)
    {
      return false;
    }
  }
}
