using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApi.Common.Exceptions;
using WebApi.Core.Cryptography.Models;
using WebApi.Core.Cryptography.Services;
using WebApi.Infrastructure.Persistence;
using WebApi.Infrastructure.Persistence.Entities;

namespace WebApi.Features.Users.Commands;

public class CreateUserCommand : IRequest
{
    public CreateUserBody Body { get; set; }
}

public class CreateUserBody
{
    public string Email { get; set; }

    public string Password { get; set; }

    public string Name { get; set; }
    public string OmCode { get; set; }
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private readonly ApplicationDbContext _context;
    private readonly HashService _hashService;

    public CreateUserCommandValidator(ApplicationDbContext context, HashService hashService)
    {
        _context = context;
        _hashService = hashService;

        RuleFor(x => x.Body.Email).NotEmpty().EmailAddress().Must(EndWithAllowedDomainEmail).MustAsync(BeUniqueEmail);
        RuleFor(x => x.Body.Password).NotEmpty();
        RuleFor(x => x.Body.Name).NotEmpty();
        RuleFor(x => x.Body.OmCode).NotEmpty().MustAsync(BeUniqueOmCode);
    }

    private bool EndWithAllowedDomainEmail(CreateUserCommand model, string email)
    {
        return email.EndsWith("@lovassy.edu.hu");
    }

    private Task<bool> BeUniqueEmail(CreateUserCommand model, string email, CancellationToken cancellationToken)
    {
        return _context.Users.AllAsync(x => x.Email != email, cancellationToken);
    }

    private Task<bool> BeUniqueOmCode(CreateUserCommand model, string omCode, CancellationToken cancellationToken)
    {
        return _context.Users.AllAsync(x => x.OmCodeHashed != _hashService.Hash(omCode), cancellationToken);
    }
}

internal sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
{
    private readonly ApplicationDbContext _context;
    private readonly EncryptionManager _encryptionManager;
    private readonly HashService _hashService;
    private readonly ResetService _resetService;

    public CreateUserCommandHandler(ApplicationDbContext context, EncryptionManager encryptionManager,
        HashService hashService, ResetService resetService)
    {
        _context = context;
        _encryptionManager = encryptionManager;
        _hashService = hashService;
        _resetService = resetService;
    }

    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_resetService.IsResetKeyPasswordSet()) throw new UnavailableException();

        var masterKeySalt = _hashService.GenerateSalt();
        var masterKey = new EncryptableKey();
        _encryptionManager.Init(masterKey.GetKey());

        var storedKey = masterKey.Lock(request.Body.Password, masterKeySalt);

        var keys = new KyberKeypair();

        var hasherSalt = _hashService.GenerateSalt();

        var user = new User
        {
            Email = request.Body.Email,
            Name = request.Body.Name,
            PasswordHashed = _hashService.HashPassword(request.Body.Password),
            PublicKey = keys.PublicKey,
            PrivateKeyEncrypted = _encryptionManager.Encrypt(keys.PrivateKey),
            MasterKeyEncrypted = storedKey,
            MasterKeySalt = masterKeySalt,
            ResetKeyEncrypted = _resetService.EncryptMasterKey(_encryptionManager.MasterKey!),
            HasherSaltEncrypted = _encryptionManager.Encrypt(hasherSalt),
            HasherSaltHashed = _hashService.Hash(hasherSalt),
            OmCodeEncrypted = _encryptionManager.Encrypt(request.Body.OmCode),
            OmCodeHashed = _hashService.Hash(request.Body.OmCode)
        };

        await _context.Users.AddAsync(user, cancellationToken);

        //TODO: Add user to default group

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}