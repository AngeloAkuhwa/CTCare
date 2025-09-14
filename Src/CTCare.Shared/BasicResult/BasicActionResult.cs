using System.Net;

namespace CTCare.Shared.BasicResult;
public class BasicActionResult: IActionResult
{
    public HttpStatusCode Status { get; set; }
    public string ErrorMessage { get; set; }

    public BasicActionResult()
    {
        Status = HttpStatusCode.OK;
        ErrorMessage = string.Empty;
    }

    public BasicActionResult(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = HttpStatusCode.BadRequest;
    }

    public BasicActionResult(HttpStatusCode status)
    {
        Status = status;
        ErrorMessage = string.Empty;
    }
}
