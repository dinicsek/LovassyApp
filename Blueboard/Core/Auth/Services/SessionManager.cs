using System.Security.Cryptography;
using System.Web;
using Blueboard.Core.Auth.Exceptions;
using Blueboard.Core.Auth.Models;
using Blueboard.Core.Auth.Schemes.Token;
using Blueboard.Infrastructure.Persistence;
using Blueboard.Infrastructure.Persistence.Entities;
using Helpers.Cryptography.Utils;
using Mapster;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SessionOptions = Blueboard.Core.Auth.Services.Options.SessionOptions;

namespace Blueboard.Core.Auth.Services;

/// <summary>
///     The scoped service responsible for managing the session of the current user. Initialized in the
///     <see cref="TokenAuthenticationSchemeHandler" />.
/// </summary>
public class SessionManager
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly SessionOptions _sessionOptions;

    private string? _encryptionKey;

    private string? _token;

    public SessionManager(IMemoryCache memoryCache, ApplicationDbContext context, IOptions<SessionOptions> options)
    {
        _memoryCache = memoryCache;
        _context = context;
        _sessionOptions = options.Value;
    }

    public Session? Session { get; set; }

    /// <summary>
    ///     Initializes the <see cref="SessionManager" /> with a token. It assumes that the token is valid and a session
    ///     already exists.
    /// </summary>
    /// <param name="token">The access token from the Authorize header.</param>
    /// <exception cref="SessionNotFoundException">
    ///     The exception thrown when no session exists for the token. (Or the session
    ///     expired)
    /// </exception>
    public void ResumeSession(string token)
    {
        _token = token;

        var tokenHash = HashingUtils.Hash(token);
        var session = _memoryCache.Get<Session>(tokenHash);

        Session = session ?? throw new SessionNotFoundException();
        _encryptionKey = HashingUtils.GenerateBasicKey(token, Session.Salt);
    }

    /// <summary>
    ///     Starts an entirely new session.
    /// </summary>
    /// <param name="user">The <see cref="User" />, for which the session is started.</param>
    /// <returns>
    ///     The access token that can later be used to initialize the <see cref="SessionManager" />. This token is url
    ///     encoded and should be decoded before checking!
    /// </returns>
    public async Task<string> StartSessionAsync(Guid userId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        _token = Convert.ToBase64String(tokenBytes);

        var expiry = DateTime.Now.AddMinutes(_sessionOptions.ExpiryMinutes);

        var hash = HashingUtils.Hash(_token);

        var personalAccessToken = new PersonalAccessToken
        {
            UserId = userId,
            Token = _token
        };
        await _context.PersonalAccessTokens.AddAsync(personalAccessToken);
        await _context.SaveChangesAsync();

        Session = new Session
        {
            Hash = hash,
            Salt = HashingUtils.GenerateSalt(),
            AccessToken = personalAccessToken.Adapt<SessionAccessToken>(), // The Id is filled by ef core
            Expiry = expiry,
            Data = new Dictionary<string, string>()
        };
        UpdateCache();

        _encryptionKey = HashingUtils.GenerateBasicKey(_token, Session.Salt);

        return HttpUtility.UrlEncode(_token); // The reason why we do this here instead of back when creating the access
        // token is that it's kind of random when asp.net core decides to urldecode
        // by itself so we want to avoid issues with that 
    }

    /// <summary>
    ///     Stops the current session. Used at logout.
    /// </summary>
    /// <exception cref="SessionNotFoundException">
    ///     The exception thrown when no session is set in <see cref="SessionManager" />.
    /// </exception>
    public void StopSession()
    {
        if (Session == null)
            throw new SessionNotFoundException();

        _memoryCache.Remove(Session.Hash);
        Session = null;
        _token = null;
        _encryptionKey = null;
    }

    /// <summary>
    ///     Stores a value in the session, encrypted with a session specific key.
    /// </summary>
    /// <param name="key">The key used in the internal dictionary of the session. Later used for retrieving the value.</param>
    /// <param name="value">The value to be stored in the session.</param>
    /// <exception cref="SessionNotFoundException">
    ///     The exception thrown when the <see cref="SessionManager" /> has not been
    ///     initialized yet.
    /// </exception>
    public void SetEncrypted(string key, string value)
    {
        if (Session == null || _encryptionKey == null)
            throw new SessionNotFoundException();

        Session.Data[key] = EncryptionUtils.Encrypt(value, _encryptionKey);
        UpdateCache();
    }

    /// <summary>
    ///     Retrieves a value from the session and decrypts it with a session specific key.
    /// </summary>
    /// <param name="key">The key used in the internal dictionary of the session.</param>
    /// <returns>The decrypted value retrieved from the session.</returns>
    /// <exception cref="SessionNotFoundException">
    ///     The exception thrown when the <see cref="SessionManager" /> has not been
    ///     initialized yet.
    /// </exception>
    public string? GetEncrypted(string key)
    {
        if (Session == null || _encryptionKey == null)
            throw new SessionNotFoundException();

        var encryptedValue = Session.Data.GetValueOrDefault(key);

        if (encryptedValue == null)
            return null;

        return EncryptionUtils.Decrypt(encryptedValue, _encryptionKey);
    }

    private void UpdateCache()
    {
        _memoryCache.Set(Session!.Hash, Session, Session.Expiry);
    }
}