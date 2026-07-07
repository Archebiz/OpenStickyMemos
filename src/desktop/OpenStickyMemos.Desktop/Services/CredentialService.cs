using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenStickyMemos.Desktop.Services;

public interface ICredentialService
{
    void Save(string accessToken, string refreshToken, string userJson);
    string? GetAccessToken();
    string? GetRefreshToken();
    string? GetUserJson();
    void Clear();

    // Persistencia de "Recordar contraseña"
    void SavePassword(string email, string password);
    string? GetRememberedEmail();
    string? GetRememberedPassword();
    void ClearPassword();
}

public class CredentialService : ICredentialService
{
    private readonly string _dbPath;
    private readonly byte[] _entropy;

    public CredentialService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenStickyMemos");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "credentials.dat");
        _entropy = Encoding.UTF8.GetBytes("OpenStickyMemos_v1");
    }

    public void Save(string accessToken, string refreshToken, string userJson)
    {
        var data = $"{accessToken}|{refreshToken}|{userJson}";
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(data),
            _entropy,
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_dbPath, encrypted);
    }

    public string? GetAccessToken()
    {
        var parts = Decrypt();
        return parts?.Length > 0 ? parts[0] : null;
    }

    public string? GetRefreshToken()
    {
        var parts = Decrypt();
        return parts?.Length > 1 ? parts[1] : null;
    }

    public string? GetUserJson()
    {
        var parts = Decrypt();
        return parts?.Length > 2 ? parts[2] : null;
    }

    public void Clear()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── "Recordar contraseña" ──

    private static readonly string PasswordPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenStickyMemos",
        "password.dat");

    private static readonly byte[] PasswordEntropy = Encoding.UTF8.GetBytes("OpenStickyMemos_pwd_v1");

    public void SavePassword(string email, string password)
    {
        var data = $"{email}|{password}";
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(data),
            PasswordEntropy,
            DataProtectionScope.CurrentUser);
        var dir = Path.GetDirectoryName(PasswordPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(PasswordPath, encrypted);
    }

    public string? GetRememberedEmail()
    {
        var parts = DecryptPassword();
        return parts?.Length > 0 ? parts[0] : null;
    }

    public string? GetRememberedPassword()
    {
        var parts = DecryptPassword();
        return parts?.Length > 1 ? parts[1] : null;
    }

    public void ClearPassword()
    {
        if (File.Exists(PasswordPath))
            File.Delete(PasswordPath);
    }

    private string[]? DecryptPassword()
    {
        if (!File.Exists(PasswordPath)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(PasswordPath);
            var decrypted = ProtectedData.Unprotect(
                encrypted, PasswordEntropy, DataProtectionScope.CurrentUser);
            var data = Encoding.UTF8.GetString(decrypted);
            return data.Split('|', 2);
        }
        catch
        {
            return null;
        }
    }

    // ── Token credentials ──

    private string[]? Decrypt()
    {
        if (!File.Exists(_dbPath)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(_dbPath);
            var decrypted = ProtectedData.Unprotect(
                encrypted, _entropy, DataProtectionScope.CurrentUser);
            var data = Encoding.UTF8.GetString(decrypted);
            return data.Split('|', 3);
        }
        catch
        {
            return null;
        }
    }
}
