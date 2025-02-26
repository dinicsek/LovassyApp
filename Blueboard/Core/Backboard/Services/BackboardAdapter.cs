using System.Text.Json;
using Blueboard.Core.Auth.Services;
using Blueboard.Core.Backboard.Exceptions;
using Blueboard.Core.Backboard.Models;
using Blueboard.Core.Backboard.Services.Options;
using Blueboard.Core.Backboard.Utils;
using Blueboard.Infrastructure.Persistence;
using Blueboard.Infrastructure.Persistence.Entities;
using EFCore.BulkExtensions;
using Helpers.Cryptography.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Blueboard.Core.Backboard.Services;

/// <summary>
///     The scoped service responsible for converting the encrypted contents of <see cref="GradeImport" /> models from
///     Backboard into <see cref="Grade" /> models.
/// </summary>
public class BackboardAdapter
{
    private readonly BackboardOptions _backboardOptions;
    private readonly ApplicationDbContext _context;
    private readonly EncryptionManager _encryptionManager;
    private readonly HashManager _hashManager;
    private readonly IMemoryCache _memoryCache;
    private readonly UserAccessor _userAccessor;

    private User? _user;

    public BackboardAdapter(ApplicationDbContext context, EncryptionManager encryptionManager, HashManager hashManager,
        IMemoryCache memoryCache, UserAccessor userAccessor, IOptions<BackboardOptions> options)
    {
        _context = context;
        _encryptionManager = encryptionManager;
        _hashManager = hashManager;
        _memoryCache = memoryCache;
        _userAccessor = userAccessor;
        _backboardOptions = options.Value;
    }

    /// <summary>
    ///     Attempts to update the user's grades, real name and class. It only works if a <see cref="GradeImport" /> for the
    ///     user exists.
    /// </summary>
    /// <exception cref="InvalidImportException">
    ///     The exception thrown when invalid grade import data is found in the database
    ///     (not decryptable or deserializable).
    /// </exception>
    public async Task UpdateAsync()
    {
        if (_user == null)
            Init();

        if ((bool?)_memoryCache.Get(_backboardOptions.BackboardAdapterLockPrefix + _user.Id) == true)
            return;

        _memoryCache.Set(_backboardOptions.BackboardAdapterLockPrefix + _user.Id, true,
            TimeSpan.FromSeconds(10)); // Lock thread

        BackboardGradeCollection? gradeCollection;
        try
        {
            gradeCollection = await GetUpdatedGradeCollectionAsync();
        }
        catch (Exception)
        {
            throw new InvalidImportException();
        }

        if (gradeCollection == null)
            return;

        _user.RealName = gradeCollection.StudentName;
        _user.Class = gradeCollection.SchoolClass;

        var grades = TransformGrades(gradeCollection);

        await _context.BulkInsertOrUpdateAsync(grades, new BulkConfig
        {
            UpdateByProperties = new List<string> { nameof(Grade.Uid) },
            PropertiesToExclude = new List<string> { nameof(Grade.Id) }
        });
        await _context.GradeImports.Where(i => i.UserId == _user.Id).BatchDeleteAsync();

        _user.ImportAvailable = false;

        await _context.SaveChangesAsync(); // only necessary for the changes to the user

        _memoryCache.Remove(_backboardOptions.BackboardAdapterLockPrefix + _user.Id);
    }

    private async Task<BackboardGradeCollection?> GetUpdatedGradeCollectionAsync()
    {
        if (!_user!.ImportAvailable)
            return null;

        var gradeImport = await _context.GradeImports.Where(i => i.UserId == _user.Id).OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync();

        var keyPair = new KyberKeypair(_encryptionManager.Decrypt(_user.PrivateKeyEncrypted));
        var gradeCollectionString = keyPair.Decrypt(gradeImport!.JsonEncrypted);

        var gradeCollection = JsonSerializer.Deserialize<BackboardGradeCollection>(gradeCollectionString);

        return gradeCollection;
    }

    private List<Grade> TransformGrades(BackboardGradeCollection gradeCollection)
    {
        var grades = new List<Grade>();

        foreach (var grade in gradeCollection.Grades)
            grades.Add(BackboardUtils.TransformBackboardGrade(grade,
                _hashManager.HashWithHasherSalt(_user!.Id.ToString())));

        return grades;
    }

    private void Init()
    {
        _user = _userAccessor.User;
    }
}