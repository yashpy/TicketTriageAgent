# AI Support Ticket Triage Agent

A multi-step agentic system that classifies incoming support tickets, retrieves
relevant knowledge-base context (RAG), drafts a response, scores its own
confidence, and either auto-resolves the ticket or routes it to a human for
approval — with every step logged for evaluation.

Built to demonstrate agent **orchestration**, not a single LLM call wrapped in
an endpoint: tool use, retrieval, confidence-gated decisions, human-in-the-loop
fallback, and observability over every decision the agent makes.

## Architecture

```
POST /api/tickets
        |
        v
  [1] Classify  --> Claude API --> category + confidence + reasoning
        |
        v
  [2] Retrieve  --> Knowledge Base search (RAG) --> top-k relevant articles
        |
        v
  [3] Draft     --> Claude API --> draft response + confidence
        |
        v
  [4] Decide    --> confidence >= threshold?
        |                          |
       YES                        NO
        |                          |
        v                          v
  AutoResolved            PendingApproval --> human approves/rejects
```

Every step (input, output, duration) is appended to the ticket's
`ReasoningTrail`, so you can pull any ticket and see exactly why the agent
made the call it made — that trail is the difference between a demo and
something you could actually put in front of an eval framework.

## Stack

- **.NET 8 / ASP.NET Core Web API** — orchestration engine and REST endpoints
- **Claude API (Anthropic)** — classification and draft generation, two
  separate structured-output calls
- **In-memory keyword retrieval** — stands in for a vector store; swap
  `IKnowledgeBaseService` for a pgvector/Pinecone implementation without
  touching the orchestrator
- **MongoDB** (optional) — persistent ticket storage; falls back to in-memory
  storage with zero config so the project runs immediately
- **Docker** — containerized for deployment to AWS/anywhere

## Running locally

```bash
export GROQ_API_KEY=gsk_...
# (Claude service still in repo if you want it — swap DI line in Program.cs
# back to ClaudeClassificationService and use ANTHROPIC_API_KEY instead)
dotnet restore
dotnet run
```

API comes up on `http://localhost:5000` by default (check console output),
Swagger UI at `/swagger` in development.

To enable MongoDB persistence instead of in-memory storage, set in
`appsettings.json` or environment:
```json
"Mongo": { "UseMongo": true, "ConnectionString": "mongodb://localhost:27017" }
```

## Endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/tickets` | Submit a ticket, runs the full pipeline synchronously |
| `GET` | `/api/tickets` | List all tickets |
| `GET` | `/api/tickets/{id}` | Get one ticket with full reasoning trail |
| `GET` | `/api/tickets/pending` | List tickets awaiting human approval |
| `POST` | `/api/tickets/{id}/approve` | Approve a pending ticket |
| `POST` | `/api/tickets/{id}/reject` | Reject a pending ticket with a reason |
| `GET` | `/api/tickets/stats` | Aggregate eval stats — auto-resolve rate, avg confidence |

## Eval

`eval/sample_tickets.json` holds 10 hand-labeled tickets across 5 categories.
`scripts/run_eval.sh` posts each one to the running API and prints predicted
vs. expected category side by side:

```bash
chmod +x scripts/run_eval.sh
./scripts/run_eval.sh
```

This is intentionally small and manual — the point isn't a large benchmark,
it's showing the *habit* of evaluating agent output against labeled data
instead of eyeballing a few demo runs and calling it done.

## Why this project

Single-call "send text to an LLM, return text" wrappers are the commodity
pattern at this point. This project focuses on the parts that are actually
hard in production agent systems: retrieval grounding, confidence-based
routing, human-in-the-loop fallback for low-confidence cases, and a reasoning
trail that makes every decision auditable after the fact.

## Notes / honest limitations

- Retrieval is keyword-overlap, not embeddings — good enough to demonstrate
  the RAG *pattern*, not production-grade semantic search. Swapping in real
  embeddings is a single-file change (`IKnowledgeBaseService`).
- Confidence scores come from the model self-reporting via prompt
  instruction, not a calibrated classifier — flagged here deliberately rather
  than oversold.
- No auth on the API — add before deploying anywhere public.
