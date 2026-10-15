using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RewindFix.Api.Configuration;

namespace RewindFix.Api.Security;

public sealed class ApiKeyValidator
{
    private readonly ApiKeyOptions _options;

    public ApiKeyValidator(
        IOptions<ApiKeyOptions> options)
    {
        _options = options.Value;
    }

    public bool IsValidIngestKey(string? providedKey)
    {
        return IsValid(
            providedKey,
            _options.IngestKey);
    }

    public bool IsValidAdminKey(string? providedKey)
    {
        return IsValid(
            providedKey,
            _options.AdminKey);
    }

    private static bool IsValid(
        string? providedKey,
        string? expectedKey)
    {
        if (string.IsNullOrWhiteSpace(providedKey) ||
            string.IsNullOrWhiteSpace(expectedKey))
        {
            return false;
        }

        var providedBytes =
            Encoding.UTF8.GetBytes(providedKey);

        var expectedBytes =
            Encoding.UTF8.GetBytes(expectedKey);

        return providedBytes.Length ==
               expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   providedBytes,
                   expectedBytes);
    }
}