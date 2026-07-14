using System.Diagnostics;
using Microsoft.Extensions.Options;
using TicketTriageAgent.Configuration;
using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

/// <summary>
/// The agent. Each public step is logged to the ticket's ReasoningTrail with timing,
/// so every decision the agent makes is auditable after the fact — this is the
/// "evaluation and observability" piece that separates an orchestrator from a
/// single LLM call wrapped in an endpoint.
///
/// Flow: classify -> retrieve (RAG) -> draft -> confidence gate -> auto-resolve OR
/// route to a human for approval/rejection.
/// </summary>
public class TriageOrchestrator : ITriageOrchestrator
{
    private readonly IClassificationService _classifier;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly ITicketRepository _repository;
    private readonly TriageOptions _options;
    private readonly ILogger<TriageOrchestrator> _logger;

    public TriageOrchestrator(
        IClassificationService classifier,
        IKnowledgeBaseService knowledgeBase,
        ITicketRepository repository,
        IOptions<TriageOptions> options,
        ILogger<TriageOrchestrator> logger)
    {
        _classifier = classifier;
        _knowledgeBase = knowledgeBase;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Ticket> RunAsync(string subject, string body, CancellationToken ct = default)
    {
        var ticket = new Ticket { Subject = subject, Body = body };

        // --- Step 1: classify ---
        var classifyResult = await TimedStep(ticket, "classify", $"{subject} | {body}", async () =>
        {
            var outcome = await _classifier.ClassifyAsync(subject, body, ct);
            ticket.Category = outcome.Category;
            ticket.ClassificationConfidence = outcome.Confidence;
            ticket.ClassificationReasoning = outcome.Reasoning;
            ticket.Status = TicketStatus.Classified;
            return $"category={outcome.Category}, confidence={outcome.Confidence:F2}, reasoning={outcome.Reasoning}";
        });
        _logger.LogInformation("Ticket {Id} classified: {Result}", ticket.Id, classifyResult);

        // --- Step 2: retrieve (RAG) ---
        await TimedStep(ticket, "retrieve", $"category={ticket.Category}", async () =>
        {
            var context = await _knowledgeBase.SearchAsync($"{subject} {body}", ticket.Category, topK: 3, ct);
            ticket.RetrievedContext = context;
            return context.Count > 0
                ? $"retrieved {context.Count} article(s)"
                : "no relevant articles found";
        });

        // --- Step 3: draft ---
        await TimedStep(ticket, "draft", $"context_count={ticket.RetrievedContext.Count}", async () =>
        {
            var draft = await _classifier.DraftResponseAsync(
                subject, body, ticket.Category ?? "general_question", ticket.RetrievedContext, ct);
            ticket.DraftResponse = draft.DraftText;
            ticket.DraftConfidence = draft.Confidence;
            ticket.Status = TicketStatus.Drafted;
            return $"confidence={draft.Confidence:F2}, draft_length={draft.DraftText.Length}";
        });

        // --- Step 4: decide (confidence gate + human-in-the-loop) ---
        await TimedStep(ticket, "decide", $"threshold={_options.ConfidenceThreshold:F2}", async () =>
        {
            var confidence = ticket.DraftConfidence ?? 0;
            if (confidence >= _options.ConfidenceThreshold)
            {
                ticket.Status = TicketStatus.AutoResolved;
                ticket.DecisionReason =
                    $"Auto-resolved: draft confidence {confidence:F2} >= threshold {_options.ConfidenceThreshold:F2}.";
            }
            else
            {
                ticket.Status = TicketStatus.PendingApproval;
                ticket.DecisionReason =
                    $"Routed to human: draft confidence {confidence:F2} < threshold {_options.ConfidenceThreshold:F2}.";
            }
            return ticket.DecisionReason;
        });

        await _repository.InsertAsync(ticket, ct);
        return ticket;
    }

    public async Task<Ticket> ApproveAsync(string ticketId, string? note, CancellationToken ct = default)
    {
        var ticket = await _repository.GetAsync(ticketId, ct)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        if (ticket.Status != TicketStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Ticket {ticketId} is not pending approval (status={ticket.Status}).");
        }

        ticket.Status = TicketStatus.Approved;
        ticket.ReasoningTrail.Add(new TraceStep
        {
            StepName = "human_approval",
            Input = note ?? "(no note)",
            Output = "approved",
            Timestamp = DateTime.UtcNow
        });

        await _repository.ReplaceAsync(ticket, ct);
        return ticket;
    }

    public async Task<Ticket> RejectAsync(string ticketId, string reason, CancellationToken ct = default)
    {
        var ticket = await _repository.GetAsync(ticketId, ct)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        if (ticket.Status != TicketStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Ticket {ticketId} is not pending approval (status={ticket.Status}).");
        }

        ticket.Status = TicketStatus.Rejected;
        ticket.RejectionReason = reason;
        ticket.ReasoningTrail.Add(new TraceStep
        {
            StepName = "human_rejection",
            Input = reason,
            Output = "rejected",
            Timestamp = DateTime.UtcNow
        });

        await _repository.ReplaceAsync(ticket, ct);
        return ticket;
    }

    private static async Task<string> TimedStep(
        Ticket ticket, string stepName, string input, Func<Task<string>> action)
    {
        var sw = Stopwatch.StartNew();
        var output = await action();
        sw.Stop();

        ticket.ReasoningTrail.Add(new TraceStep
        {
            StepName = stepName,
            Input = input,
            Output = output,
            Timestamp = DateTime.UtcNow,
            DurationMs = sw.ElapsedMilliseconds
        });

        return output;
    }
}
