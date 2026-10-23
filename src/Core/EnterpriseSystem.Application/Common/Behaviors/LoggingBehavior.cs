using System.Diagnostics;
using EnterpriseSystem.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseSystem.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId?.ToString() ?? "Anonymous";

        _logger.LogInformation("EnterpriseSystem Request: {Name} initiated by User [{UserId}]", requestName, userId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            _logger.LogInformation("EnterpriseSystem Request: {Name} completed in {ElapsedMilliseconds}ms for User [{UserId}]",
                requestName, stopwatch.ElapsedMilliseconds, userId);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "EnterpriseSystem Request: {Name} failed after {ElapsedMilliseconds}ms for User [{UserId}]",
                requestName, stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }
}
