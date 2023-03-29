using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using WebApi.Helpers.Cryptography.Services.Options;

namespace WebApi.Helpers.Cryptography.Services;

public class HashService : IHashService
{
    private readonly int _passwordBytesRequested;
    private readonly int _passwordHashIterations;
    private readonly int _passwordSaltLength;
    private readonly RandomNumberGenerator _rng;

    public HashService(IOptions<HashOptions> hashOptions)
    {
        _rng = hashOptions.Value.DefaultRng;

        if (hashOptions.Value.PasswordHashIterations < 1)
            throw new ArgumentException("Password hash iterations must be greater than 0", nameof(hashOptions));

        _passwordHashIterations = hashOptions.Value.PasswordHashIterations;

        if (hashOptions.Value.PasswordSaltLength < 1)
            throw new ArgumentException("Password salt length must be greater than 0", nameof(hashOptions));

        _passwordSaltLength = hashOptions.Value.PasswordSaltLength;

        if (hashOptions.Value.PasswordBytesRequested < 1)
            throw new ArgumentException("Password bytes requested must be greater than 0", nameof(hashOptions));

        _passwordBytesRequested = hashOptions.Value.PasswordBytesRequested;
    }

    public string Hash(string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);

        var hashAlgorithm = SHA256.Create();
        var hashBytes = hashAlgorithm.ComputeHash(dataBytes);

        return Convert.ToBase64String(hashBytes);
    }

    public bool Verify(string data, string hash)
    {
        var dataHash = Hash(data);

        return dataHash == hash;
    }

    public string HashWithSalt(string data, string salt)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data + ";" + salt);

        var hashAlgorithm = SHA512.Create();
        var hashBytes = hashAlgorithm.ComputeHash(dataBytes);

        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyWithSalt(string data, string salt, string hash)
    {
        var dataHash = HashWithSalt(data, salt);

        return dataHash == hash;
    }

    public string HashPassword(string password)
    {
        var salt = new byte[_passwordSaltLength];
        _rng.GetBytes(salt);

        var subkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA512, _passwordHashIterations,
            _passwordBytesRequested);

        var outputBytes = new byte[13 + salt.Length + subkey.Length];
        outputBytes[0] = 0x01;
        WriteNetworkByteOrder(outputBytes, 1, (uint)KeyDerivationPrf.HMACSHA512);
        WriteNetworkByteOrder(outputBytes, 5, (uint)_passwordHashIterations);
        WriteNetworkByteOrder(outputBytes, 9, (uint)_passwordSaltLength);
        Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
        Buffer.BlockCopy(subkey, 0, outputBytes, 13 + _passwordSaltLength, subkey.Length);

        return Convert.ToBase64String(outputBytes);
    }

    public bool VerifyPassword(string password, string hash)
    {
        var decodedHash = Convert.FromBase64String(hash);

        if (decodedHash.Length == 0)
            return false;

        if (decodedHash[0] != 0x01)
            return false;

        var prf = (KeyDerivationPrf)ReadNetworkByteOrder(decodedHash, 1);
        var iterCount = (int)ReadNetworkByteOrder(decodedHash, 5);
        var saltLength = (int)ReadNetworkByteOrder(decodedHash, 9);

        if (saltLength != _passwordSaltLength)
            return false;

        if (iterCount < 1)
            return false;

        var salt = new byte[saltLength];
        Buffer.BlockCopy(decodedHash, 13, salt, 0, salt.Length);

        var expectedSubkey = new byte[decodedHash.Length - 13 - salt.Length];
        Buffer.BlockCopy(decodedHash, 13 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

        var actualSubkey = KeyDerivation.Pbkdf2(password, salt, prf, iterCount, expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }

    public string GenerateBasicKey(string data, string salt)
    {
        var keyBytes = KeyDerivation.Pbkdf2(data, Encoding.UTF8.GetBytes(salt), KeyDerivationPrf.HMACSHA512, 1000, 32);
        return Convert.ToBase64String(keyBytes);
    }

    public string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(128 / 8);
        return Convert.ToBase64String(saltBytes);
    }

    private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
    {
        return ((uint)buffer[offset + 0] << 24)
               | ((uint)buffer[offset + 1] << 16)
               | ((uint)buffer[offset + 2] << 8)
               | buffer[offset + 3];
    }

    private static void WriteNetworkByteOrder(byte[] buffer, int offset, uint value)
    {
        buffer[offset + 0] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)(value >> 0);
    }
}