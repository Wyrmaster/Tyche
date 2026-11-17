namespace Tyche.Service.Test.Support;

public sealed class TempFolder : IDisposable
{
  public TempFolder()
  {
    Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tyche-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path);
  }

  public string Path { get; }

  public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

  public string Sub(string name)
  {
    var folder = Combine(name);
    Directory.CreateDirectory(folder);
    return folder;
  }

  public void Dispose()
  {
    try
    {
      Directory.Delete(Path, recursive: true);
    }
    catch (IOException)
    {
    }
  }
}
