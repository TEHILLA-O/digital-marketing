var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var identityDb = postgres.AddDatabase("identity");
var accountsDb = postgres.AddDatabase("accounts");
var paymentsDb = postgres.AddDatabase("payments");
var ledgerDb = postgres.AddDatabase("ledger");
var auditDb = postgres.AddDatabase("audit");

var redis = builder.AddRedis("redis").WithDataVolume();
var kafka = builder.AddKafka("kafka");

var identity = builder.AddProject<Projects.LedgerX_Identity_Api>("identity-api")
    .WithReference(identityDb)
    .WithHttpEndpoint(5101, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WaitFor(postgres);

var accounts = builder.AddProject<Projects.LedgerX_Accounts_Api>("accounts-api")
    .WithReference(accountsDb)
    .WithHttpEndpoint(5102, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WaitFor(postgres);

var ledger = builder.AddProject<Projects.LedgerX_Ledger_Api>("ledger-api")
    .WithReference(ledgerDb)
    .WithHttpEndpoint(5104, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WaitFor(postgres);

var payments = builder.AddProject<Projects.LedgerX_Payments_Api>("payments-api")
    .WithReference(paymentsDb)
    .WithReference(redis)
    .WithHttpEndpoint(5103, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Services__Accounts", accounts.GetEndpoint("http"))
    .WithEnvironment("Services__Ledger", ledger.GetEndpoint("http"))
    .WaitFor(accounts)
    .WaitFor(ledger)
    .WaitFor(redis);

builder.AddProject<Projects.LedgerX_Audit_Worker>("audit-worker")
    .WithReference(auditDb)
    .WithHttpEndpoint(5105, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WaitFor(postgres);

builder.AddProject<Projects.LedgerX_Notification_Worker>("notification-worker")
    .WithHttpEndpoint(5106, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka);

builder.AddProject<Projects.LedgerX_Projection_Worker>("projection-worker")
    .WithReference(redis)
    .WithHttpEndpoint(5107, name: "http")
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Services__Accounts", accounts.GetEndpoint("http"))
    .WithEnvironment("Services__Ledger", ledger.GetEndpoint("http"));

builder.AddProject<Projects.LedgerX_Web>("web")
    .WithHttpEndpoint(5100, name: "http")
    .WithEnvironment("Services__Identity", identity.GetEndpoint("http"))
    .WithEnvironment("Services__Accounts", accounts.GetEndpoint("http"))
    .WithEnvironment("Services__Payments", payments.GetEndpoint("http"))
    .WithEnvironment("Services__Ledger", ledger.GetEndpoint("http"))
    .WaitFor(identity);

builder.Build().Run();
