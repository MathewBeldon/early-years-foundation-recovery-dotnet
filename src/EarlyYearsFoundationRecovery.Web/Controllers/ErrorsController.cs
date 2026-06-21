using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

public class ErrorsController : Controller
{
    [Route("errors/not-found")]
    [Route("404")]
    public IActionResult PageNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    [Route("errors/internal-server-error")]
    [Route("500")]
    public IActionResult InternalServerError()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("InternalServerError");
    }

    [Route("errors/service-unavailable")]
    [Route("503")]
    public IActionResult ServiceUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("ServiceUnavailable");
    }

    [Route("errors/{statusCode:int}")]
    public IActionResult StatusCodePage(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status404NotFound => PageNotFound(),
            StatusCodes.Status503ServiceUnavailable => ServiceUnavailable(),
            _ => InternalServerError(),
        };
    }
}
