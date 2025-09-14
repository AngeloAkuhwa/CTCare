using System.Net;
using System.Text.Json.Serialization;

namespace CTCare.Shared.BasicResult
{
    public interface IActionResult
    {
        [JsonIgnore]
        HttpStatusCode Status { get; set; }

        [JsonIgnore]
        string ErrorMessage { get; set; }
    }
}
