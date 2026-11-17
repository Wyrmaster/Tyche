namespace Tyche.Service.Versioning;

public interface IVersionProvider
{
  /// <summary>The version without build number, for example v1.1.5 (or v0.0.0-dev for local builds).</summary>
  string Version { get; }
}
