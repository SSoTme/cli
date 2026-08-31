using System.Diagnostics;
using Newtonsoft.Json;

namespace Effortless.Cli.Config;

public class KeyFile
{
    public string EmailAddress { get; set; }
    public string Secret { get; set; }

    public static KeyFile CurrentKey
    {
        get { return GetSSoTmeKey(); }
        set { SetSSoTmeKey(value); }
    }

    public static void SetSSoTmeKey(KeyFile value, string account = "")
    {
        FileInfo ssotmeKeyFile = GetKeyForAccount(account);
        string ssotmeJson = JsonConvert.SerializeObject(value, Formatting.Indented);

        try
        {
            if (!ssotmeKeyFile.Directory.Exists)
            {
                ssotmeKeyFile.Directory.Create();
            }

            File.WriteAllText(ssotmeKeyFile.FullName, ssotmeJson);

            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"600 \"{ssotmeKeyFile.FullName}\"",
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
                $"Cannot write to '{ssotmeKeyFile.FullName}'. " +
                $"Please ensure the directory '{ssotmeKeyFile.Directory.FullName}' exists and you have write permissions. " +
                $"You may need to run: chmod 700 \"{ssotmeKeyFile.Directory.FullName}\"",
                ex);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"Cannot write to '{ssotmeKeyFile.FullName}'. " +
                "Please ensure you have write permissions to this location. " +
                $"You may need to run: chmod 700 \"{ssotmeKeyFile.Directory.FullName}\" && chmod 600 \"{ssotmeKeyFile.FullName}\"",
                ex);
        }
    }

    public static KeyFile GetSSoTmeKey(string runAs = "")
    {
        FileInfo ssotmeKeyFile = GetKeyForAccount(runAs);

        if (ssotmeKeyFile.Exists)
        {
            return JsonConvert.DeserializeObject<KeyFile>(
                File.ReadAllText(ssotmeKeyFile.FullName));
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
                UserConfigDir.SSoTmeDir.FullName,
                "ssotme.key"));
        }

        return new FileInfo(Path.Combine(
            UserConfigDir.SSoTmeDir.FullName,
            String.Format("ssotme.{0}.key", accountUsername)));
    }

    public override string ToString()
    {
        return EmailAddress;
    }
}
