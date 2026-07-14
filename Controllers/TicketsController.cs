using Microsoft.AspNetCore.Mvc;
using TicketTriageAgent.Dtos;
using TicketTriageAgent.Models;
using TicketTriageAgent.Services;

namespace TicketTriageAgent.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITriageOrchestrator _orchestrator;
    private readonly ITicketRepository _repository;

    public TicketsController(ITriageOrchestrator orchestrator, ITicketRepository repository)
    {
        _orchestrator = orchestrator;
        _repository = repository;
    }

    /// <summary>Submits a new ticket and runs it through the full agent pipeline synchronously.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketResponse>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest("Subject and Body are required.");
        }

        var ticket = await _orchestrator.RunAsync(request.Subject, request.Body, ct);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, TicketResponse.FromTicket(ticket));
    }

    [HttpGet]
    public async Task<ActionResult<List<TicketResponse>>> GetAll(CancellationToken ct)
    {
        var tickets = await _repository.GetAllAsync(ct);
        return tickets.Select(TicketResponse.FromTicket).ToList();
    }

    [HttpGet("pending")]
    public async Task<ActionResult<List<TicketResponse>>> GetPending(CancellationToken ct)
    {
        var tickets = await _repository.GetByStatusAsync(TicketStatus.PendingApproval, ct);
        return tickets.Select(TicketResponse.FromTicket).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TicketResponse>> Get(string id, CancellationToken ct)
    {
        var ticket = await _repository.GetAsync(id, ct);
        return ticket is null ? NotFound() : TicketResponse.FromTicket(ticket);
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<TicketResponse>> Approve(string id, ApprovalDecisionRequest request, CancellationToken ct)
    {
        try
        {
            var ticket = await _orchestrator.ApproveAsync(id, request.Note, ct);
            return TicketResponse.FromTicket(ticket);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<TicketResponse>> Reject(string id, RejectionDecisionRequest request, CancellationToken ct)
    {
        try
        {
            var ticket = await _orchestrator.RejectAsync(id, request.Reason, ct);
            return TicketResponse.FromTicket(ticket);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    /// <summary>Aggregate eval stats — auto-resolve rate, average confidences, decision breakdown.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<EvalStats>> Stats(CancellationToken ct)
    {
        var tickets = await _repository.GetAllAsync(ct);
        if (tickets.Count == 0)
        {
            return new EvalStats(0, 0, 0, 0, 0, 0, 0, 0);
        }

        return new EvalStats(
            TotalTickets: tickets.Count,
            AutoResolved: tickets.Count(t => t.Status == TicketStatus.AutoResolved),
            PendingApproval: tickets.Count(t => t.Status == TicketStatus.PendingApproval),
            Approved: tickets.Count(t => t.Status == TicketStatus.Approved),
            Rejected: tickets.Count(t => t.Status == TicketStatus.Rejected),
            AverageClassificationConfidence: tickets.Average(t => t.ClassificationConfidence ?? 0),
            AverageDraftConfidence: tickets.Average(t => t.DraftConfidence ?? 0),
            AutoResolveRate: (double)tickets.Count(t => t.Status == TicketStatus.AutoResolved) / tickets.Count);
    }
}
