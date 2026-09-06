using System.Diagnostics;
using Newtonsoft.Json;

namespace Effortless.Cli.Config;

public class KeyFile
{
    public string EmailAddress { get; set; }
    public string Secret { get; set; }

    public static KeyFile CurrentKey
    {
        get { return GetKey(); }
        set { SetKey(value); }
    }

    public static void SetKey(KeyFile value, string account = "")
    {
        FileInfo keyFile = GetKeyForAccount(account);
        string keyJson = JsonConvert.SerializeObject(value, Formatting.Indented);

        try
        {
            if (!keyFile.Directory.Exists)
            {
                keyFile.Directory.Create();
            }

            File.WriteAllText(keyFile.FullName, keyJson);

            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"600 \"{keyFile.FullName}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                catch
                {
                    // Silently ignore chmod failures.
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                $"Cannot write to '{keyFile.FullName}'. " +
                $"Please ensure the directory '{keyFile.Directory.FullName}' exists and you have write permissions. " +
                $"You may need to run: chmod 700 \"{keyFile.Directory.FullName}\"",
                ex);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"Cannot write to '{keyFile.FullName}'. " +
                "Please ensure you have write permissions to this location. " +
                $"You may need to run: chmod 700 \"{keyFile.Directory.FullName}\" && chmod 600 \"{keyFile.FullName}\"",
                ex);
        }
    }

    public static KeyFile GetKey(string runAs = "")
    {
        FileInfo keyFile = GetKeyForAccount(runAs);

        if (keyFile.Exists)
        {
            return JsonConvert.DeserializeObject<KeyFile>(
                File.ReadAllText(keyFile.FullName));
        }

        return new KeyFile
        {
            APIKeys = new Dictionary<string, string>()
        };
    }

    private Dictionary<string, string> _apiKeys;

    public Dictionary<string, string> APIKeys
    {
        get
        {
            if (ReferenceEquals(_apiKeys, null))
            {
                _apiKeys = new Dictionary<string, string>();
            }

            return _apiKeys;
        }
        set { _apiKeys = value; }
    }

    private static FileInfo GetKeyForAccount(string accountUsername)
    {
        if (String.IsNullOrEmpty(accountUsername))
        {
            return new FileInfo(Path.Combine(
                UserConfigDir.EffortlessDir.FullName,
                "effortless.key"));
        }

        return new FileInfo(Path.Combine(
            UserConfigDir.EffortlessDir.FullName,
            String.Format("effortless.{0}.key", accountUsername)));
    }

    public override string ToString()
    {
        return EmailAddress;
    }
}
