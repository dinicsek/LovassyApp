using System.Text.Json;
using Blueboard.Core.Auth.Services;
using Blueboard.Features.Users.Jobs;
using Blueboard.Infrastructure.Persistence;
using Blueboard.Infrastructure.Persistence.Entities;
using Helpers.WebApi.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Blueboard.Features.Users.Commands;

public static class KickUser
{
    public class Command : IRequest
    {
        public Guid Id { get; set; }
    }

    internal sealed class Handler : IRequestHandler<Command>
    {
        private readonly ApplicationDbContext _context;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly SessionService _sessionService;

        public Handler(ApplicationDbContext context, SessionService sessionService, ISchedulerFactory schedulerFactory)
        {
            _context = context;
            _sessionService = sessionService;
            _schedulerFactory = schedulerFactory;
        }

        public async Task<Unit> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await _context.Users.Where(u => u.Id == request.Id).Include(u => u.PersonalAccessTokens)
                .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

            if (user == null) throw new NotFoundException(nameof(User), request.Id);

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            var stopSessionsJob = JobBuilder.Create<StopSessionsJob>().WithIdentity("stopSessions", "userKickedJobs")
                .UsingJobData("tokensJson", JsonSerializer.Serialize(user.PersonalAccessTokens.Select(t => t.Token)))
                .Build();

            var stopSessionsTrigger = TriggerBuilder.Create().WithIdentity("stopSessionsTrigger", "userKickedJobs")
                .StartNow().Build();

            await scheduler.ScheduleJob(stopSessionsJob, stopSessionsTrigger, cancellationToken);

            return Unit.Value;
        }
    }
}