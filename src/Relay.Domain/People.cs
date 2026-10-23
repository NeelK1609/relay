namespace Relay.Domain;

public sealed class ServiceAccount
{
    private ServiceAccount() { }

    public ServiceAccount(string accountNumber, string serviceAddress, string city, string plan)
    {
        Id = Guid.CreateVersion7();
        AccountNumber = accountNumber;
        ServiceAddress = serviceAddress;
        City = city;
        Plan = plan;
    }

    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; } = "";
    public string ServiceAddress { get; private set; } = "";
    public string City { get; private set; } = "";
    public string Plan { get; private set; } = "";
    public decimal Balance { get; private set; }

    public void SetBalance(decimal balance) => Balance = balance;
}

public sealed class Customer
{
    private Customer() { }

    public Customer(string firstName, string lastName, string email, string phone, ServiceAccount account)
    {
        Id = Guid.CreateVersion7();
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        Account = account;
        AccountId = account.Id;
        CustomerSince = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public Guid AccountId { get; private set; }
    public ServiceAccount Account { get; private set; } = null!;
    public DateTimeOffset CustomerSince { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public void SetCustomerSince(DateTimeOffset since) => CustomerSince = since;
}

public sealed class Staff
{
    private Staff() { }

    public Staff(string name, string email, string team)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Email = email;
        Team = team;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Team { get; private set; } = "";
}

public sealed class KnowledgeArticle
{
    private KnowledgeArticle() { }

    public KnowledgeArticle(string title, CaseCategory category, string body)
    {
        Id = Guid.CreateVersion7();
        Title = title;
        Category = category;
        Body = body;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = "";
    public CaseCategory Category { get; private set; }
    public string Body { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
}
