using TicketTriageAgent.Configuration;
using TicketTriageAgent.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Config binding ---
builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection(ClaudeOptions.SectionName));
builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<TriageOptions>(builder.Configuration.GetSection(TriageOptions.SectionName));

// Prefer env vars over appsettings.json so real keys never get committed to the repo.
var envApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (!string.IsNullOrWhiteSpace(envApiKey))
{
    builder.Services.PostConfigure<ClaudeOptions>(opts => opts.ApiKey = envApiKey);
}

var envGroqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
if (!string.IsNullOrWhiteSpace(envGroqKey))
{
    builder.Services.PostConfigure<GroqOptions>(opts => opts.ApiKey = envGroqKey);
}

// --- HTTP client for the classification/draft LLM ---
// Default: Groq (free tier). To go back to Claude, swap the line below for:
//   builder.Services.AddHttpClient<IClassificationService, ClaudeClassificationService>();
builder.Services.AddHttpClient<IClassificationService, GroqClassificationService>();

// --- Knowledge base (RAG) ---
builder.Services.AddSingleton<IKnowledgeBaseService, InMemoryKnowledgeBaseService>();

// --- Repository: Mongo if configured, otherwise in-memory (zero-setup default) ---
var useMongo = builder.Configuration.GetValue<bool>($"{MongoOptions.SectionName}:UseMongo");
if (useMongo)
{
    builder.Services.AddSingleton<ITicketRepository, MongoTicketRepository>();
}
else
{
    builder.Services.AddSingleton<ITicketRepository, InMemoryTicketRepository>();
}

// --- Orchestrator (the agent) ---
builder.Services.AddScoped<ITriageOrchestrator, TriageOrchestrator>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "Ticket Triage Agent",
    status = "running",
    docs = "/swagger"
}));

app.Run();
