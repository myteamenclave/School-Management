using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.WebApi.Filters;

public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case DomainException:
                context.Result = new BadRequestObjectResult(new { error = context.Exception.Message });
                context.ExceptionHandled = true;
                break;
            case NotFoundException:
                context.Result = new NotFoundObjectResult(new { error = context.Exception.Message });
                context.ExceptionHandled = true;
                break;
            case ConflictException:
                context.Result = new ConflictObjectResult(new { error = context.Exception.Message });
                context.ExceptionHandled = true;
                break;
        }
    }
}
