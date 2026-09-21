using System.ComponentModel.DataAnnotations;
using JevSharp.Abstractions.Clients;
using JevSharp.Abstractions.Exceptions;
using JevSharp.Abstractions.Models;
using JevSharp.Abstractions.Requests;
using JevSharp.Abstractions.Responses;
using JevSharp.Samples.Web.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace JevSharp.Samples.Web.Controllers;

/// <summary>Evaluates structured ticket content through an injected JevSharp client.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class EvaluationsController : ControllerBase
{
    private readonly IJevClient client;

    /// <summary>Initializes the controller with the client registered by dependency injection.</summary>
    /// <param name="client">The evaluation client for the current request scope.</param>
    /// <exception cref="ArgumentNullException">The client is null.</exception>
    public EvaluationsController(IJevClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>Evaluates the submitted ticket against two independent yes/no questions.</summary>
    /// <param name="ticket">The ticket content supplied in the JSON request body.</param>
    /// <param name="cancellationToken">Cancels request processing and the provider call.</param>
    /// <returns>Normalized probabilities or a safe HTTP problem response.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EvaluationResponse>> EvaluateAsync(
        [FromBody] EvaluationTicket ticket,
        CancellationToken cancellationToken)
    {
        var request = new JevRequest(JevValue.FromObject(ticket), new Dictionary<string, JevQuestion>
        {
            ["urgent"] = new NoulQuestion("Does the message describe an urgent problem?"),
            ["refund"] = new NoulQuestion("Does the customer request a refund?")
        });
        try
        {
            var response = await client.EvaluateAsync(request, cancellationToken);
            return Ok(new EvaluationResponse(
                response.GetAnswer<NoulAnswer>("urgent").Probability,
                response.GetAnswer<NoulAnswer>("refund").Probability,
                response.Model));
        }
        catch (JevRateLimitException)
        {
            return Problem("The evaluation service is busy. Try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (JevException)
        {
            return Problem("The evaluation service could not complete the request.", statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
