using Blueboard.Infrastructure.Persistence;
using Blueboard.Infrastructure.Persistence.Entities;
using Helpers.WebApi.Exceptions;
using MediatR;

namespace Blueboard.Features.Import.Commands;

public static class DeleteImportKey
{
    public class Command : IRequest
    {
        public int Id { get; set; }
    }

    internal sealed class Handler : IRequestHandler<Command>
    {
        private readonly ApplicationDbContext _context;

        public Handler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(Command request, CancellationToken cancellationToken)
        {
            var importKey = await _context.ImportKeys.FindAsync(request.Id);

            if (importKey is null)
                throw new NotFoundException(nameof(ImportKey), request.Id);

            _context.ImportKeys.Remove(importKey);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}