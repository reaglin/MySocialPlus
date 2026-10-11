using System.Security.Cryptography;
using System.Text;

namespace MySocialPlus.Core.Secrets;

/// <summary>
/// Sign-in secrets (a Bluesky app password, later a Facebook token), one small file each,
/// encrypted with DPAPI for the current Windows user (CLAUDE.md, rule 6). Nobody signed in as
/// anyone else can read them, and they never travel with the tree or the cache.
/// </summary>
public sealed class SecretStore
{
    public SecretStore(string folder) => Folder = folder;

    public string Folder { get; }

    private string PathFor(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_').ToArray());
        return Path.Combine(Folder, safe + ".dat");
    }

    public bool Has(string name) => File.Exists(PathFor(name));

    public void Save(string name, string value)
    {
        if (string.IsNullOrEmpty(value)) { Delete(name); return; }
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(PathFor(name), Protect(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>The secret, or null when there is none or it was written by another user or machine.</summary>
    public string? Load(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        try { return Encoding.UTF8.GetString(Unprotect(File.ReadAllBytes(path))); }
        catch (CryptographicException) { return null; }
        catch (IOException) { return null; }
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>"abcd…wxyz" — enough to recognise, not enough to use.</summary>
    public static string Mask(string secret) =>
        secret.Length <= 10 ? new string('•', secret.Length) : $"{secret[..4]}…{secret[^4..]}";

    private static byte[] Protect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser) : data;

    private static byte[] Unprotect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser) : data;
}
