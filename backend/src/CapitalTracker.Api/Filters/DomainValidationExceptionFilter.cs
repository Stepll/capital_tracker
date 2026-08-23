using CapitalTracker.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CapitalTracker.Api.Filters;

/// <summary>
/// Turns a DomainValidationException into a 400 carrying its message. Without this the
/// only thing a form could say when a rule is broken is "щось пішло не так" — the whole
/// value of "не можна продати більше, ніж є" is in the sentence itself reaching the user.
///
/// An MVC filter rather than exception-handling middleware so the SSE actions, which write
/// their own body and report failures as stream events, are left completely alone.
/// </summary>
public class DomainValidationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DomainValidationException exception)
            return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = exception.Message,
            Status = StatusCodes.Status400BadRequest,
        })
        {
            StatusCode = StatusCodes.Status400BadRequest,
        };
        context.ExceptionHandled = true;
    }
}
