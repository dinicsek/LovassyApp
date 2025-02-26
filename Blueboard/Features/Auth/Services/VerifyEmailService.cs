using Blueboard.Features.Auth.Models;
using Blueboard.Features.Auth.Services.Options;
using Blueboard.Infrastructure.Persistence.Entities;
using Helpers.Cryptography.Services;
using Microsoft.Extensions.Options;

namespace Blueboard.Features.Auth.Services;

/// <summary>
///     The singleton service responsible for generating and decrypting email verify tokens.
/// </summary>
public class VerifyEmailService
{
    private readonly EncryptionService _encryptionService;
    private readonly VerifyEmailOptions _verifyEmailOptions;

    public VerifyEmailService(EncryptionService encryptionService, IOptions<VerifyEmailOptions> verifyEmailOptions)
    {
        _encryptionService = encryptionService;
        _verifyEmailOptions = verifyEmailOptions.Value;
    }

    /// <summary>
    ///     Generates an email verify token for a <see cref="User" />.
    /// </summary>
    /// <param name="userId">The id of the <see cref="User" />.</param>
    /// <returns>The verify token itself.</returns>
    public string GenerateVerifyToken(Guid userId)
    {
        return _encryptionService.SerializeProtect(
            new VerifyEmailTokenContents { UserId = userId, Purpose = "VerifyEmail" },
            TimeSpan.FromMinutes(_verifyEmailOptions.ExpiryMinutes));
    }

    /// <summary>
    ///     Gets the decrypted contents of an email verify token (or null, if the verify token is already expired).
    /// </summary>
    /// <param name="verifyToken">The verify token to decrypt.</param>
    /// <returns>The decrypted contents.</returns>
    public VerifyEmailTokenContents? DecryptVerifyToken(string verifyToken)
    {
        try
        {
            var contents = _encryptionService.DeserializeUnprotect<VerifyEmailTokenContents>(verifyToken, out _);
            if (contents == null || contents.Purpose != "VerifyEmail") return null;

            return contents;
        }
        catch
        {
            return null;
        }
    }
}