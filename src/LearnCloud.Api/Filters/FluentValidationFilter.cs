using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LearnCloud.Api.Filters;

// Runs the FluentValidation validator registered for each action argument, if any, and
// answers 422 with a ValidationProblemDetails body when it fails.
//
// The modules define 28 validators (registration, login, subjects, attendance, setup
// wizard, ...) and they were registered, but nothing ever invoked them: registration
// accepted any slug and any password. Validating centrally means a new request type
// only needs a validator class to be enforced.
public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new UnprocessableEntityObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Validation failed",
                Type = "https://learncloud.co.zw/errors/validation",
                Instance = context.HttpContext.Request.Path
            });
            return;
        }

        await next();
    }
}
