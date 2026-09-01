using CivicFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CivicFlow.Api.Common;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ||
            exception is Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } })
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { message = "This operation was already processed or the case changed. Refresh before retrying." }, cancellationToken);
            return true;
        }
        if (exception is not (DomainRuleException or ArgumentException)) return false;
        logger.LogInformation(exception, "A CivicFlow business rule rejected the request.");
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The requested operation is not valid.",
            Detail = exception.Message
        }, cancellationToken);
        return true;
    }
}
