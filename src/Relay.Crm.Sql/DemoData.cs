using Microsoft.EntityFrameworkCore;
using Relay.Domain;

namespace Relay.Crm.Sql;

/// <summary>Fictional Highland Energy data so the portal and workspace have something to show.</summary>
public static class DemoData
{
    public static async Task SeedAsync(RelayDbContext db, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await db.Customers.AnyAsync(ct)) return;

        var staff = new[]
        {
            new Staff("Aiko Tanaka", "aiko.tanaka@highland-energy.example", "Customer Care"),
            new Staff("Daniel Okafor", "daniel.okafor@highland-energy.example", "Customer Care"),
            new Staff("Priya Shah", "priya.shah@highland-energy.example", "Billing"),
            new Staff("Marcus Lee", "marcus.lee@highland-energy.example", "Field Operations"),
        };
        var (aiko, daniel, priya, marcus) = (staff[0], staff[1], staff[2], staff[3]);

        Customer NewCustomer(string first, string last, string phone, string acct, string address, string city, string plan, decimal balance, int years)
        {
            var account = new ServiceAccount(acct, address, city, plan);
            account.SetBalance(balance);
            var c = new Customer(first, last, $"{first}.{last}@example.com".ToLowerInvariant(), phone, account);
            c.SetCustomerSince(now.AddYears(-years));
            return c;
        }

        var sarah = NewCustomer("Sarah", "Mitchell", "204-555-0141", "HE-100245", "88 Wellington Cres", "Winnipeg", "Residential Standard", 142.18m, 6);
        var omar = NewCustomer("Omar", "Haddad", "204-555-0187", "HE-100378", "1204 Pembina Hwy", "Winnipeg", "Residential Time-of-Use", 0m, 2);
        var linda = NewCustomer("Linda", "Bergen", "204-555-0112", "HE-101902", "15 Main St", "Steinbach", "Equal Payment Plan", 96.00m, 11);
        var raj = NewCustomer("Raj", "Patel", "204-555-0163", "HE-102055", "402 Rosser Ave", "Brandon", "Small Business", 1218.40m, 4);
        var emily = NewCustomer("Emily", "Chartrand", "204-555-0129", "HE-102331", "27 Eveline St", "Selkirk", "Residential Standard", 310.75m, 1);
        var tom = NewCustomer("Tom", "Nguyen", "204-555-0176", "HE-103118", "9 Selkirk Ave", "Thompson", "Residential Standard", 58.30m, 8);
        var grace = NewCustomer("Grace", "Okoye", "204-555-0150", "HE-103540", "310 Osborne St", "Winnipeg", "Residential Time-of-Use", 0m, 3);
        var ben = NewCustomer("Ben", "Fraser", "204-555-0194", "HE-104002", "61 Lakeshore Rd", "Gimli", "Residential Standard", 405.12m, 15);
        var customers = new[] { sarah, omar, linda, raj, emily, tom, grace, ben };

        SupportCase Case(Customer who, string title, string body, CaseCategory cat, CasePriority pri, Channel ch, TimeSpan age) =>
            SupportCase.Open(who, title, body, cat, pri, ch, now - age);

        var cases = new List<SupportCase>();

        var c1 = Case(raj, "Power out at our restaurant since this morning",
            "Our restaurant on Rosser Ave has had no power since about 6am. The neighbours seem to have power. We have a walk-in freezer full of stock, please help urgently.",
            CaseCategory.Outage, CasePriority.Urgent, Channel.Phone, TimeSpan.FromHours(3));
        cases.Add(c1);

        var c2 = Case(emily, "My bill is three times higher than usual",
            "My latest bill is $310 but I usually pay around $100. Nothing has changed in my home. Could the meter be wrong?",
            CaseCategory.Billing, CasePriority.Normal, Channel.Portal, TimeSpan.FromHours(30));
        c2.AssignTo(priya, now - TimeSpan.FromHours(26));
        c2.AddMessage(AuthorType.Staff, priya.Name,
            "Hi Emily, thanks for reaching out. I can see your last two readings were estimated. Could you send a photo of your meter display so we can compare it with the actual reading?",
            false, now - TimeSpan.FromHours(25));
        c2.ChangeStatus(CaseStatus.WaitingOnCustomer, now - TimeSpan.FromHours(25));
        cases.Add(c2);

        var c3 = Case(sarah, "Moving to a new address next month",
            "I'm moving from Wellington Cres to 22 Arbor Lane in Winnipeg on the 15th of next month. Please stop service at the old place and start it at the new one.",
            CaseCategory.MoveService, CasePriority.Low, Channel.Email, TimeSpan.FromHours(5));
        cases.Add(c3);

        var c4 = Case(ben, "Can I set up a payment arrangement?",
            "I've fallen behind on my bill after some unexpected expenses. Is it possible to split my balance into smaller monthly payments?",
            CaseCategory.Payment, CasePriority.High, Channel.Chat, TimeSpan.FromHours(6));
        cases.Add(c4);

        var c5 = Case(tom, "Flickering lights on our street",
            "Lights in our house and the neighbour's have been flickering every evening this week. Is there a problem with the line?",
            CaseCategory.Outage, CasePriority.High, Channel.Portal, TimeSpan.FromHours(20));
        c5.AssignTo(marcus, now - TimeSpan.FromHours(19));
        c5.AddMessage(AuthorType.Staff, marcus.Name,
            "Thanks Tom. I've asked a crew to inspect the transformer on Selkirk Ave tomorrow morning. I'll update you once they've been out.",
            false, now - TimeSpan.FromHours(18));
        c5.AddMessage(AuthorType.Staff, marcus.Name,
            "Crew booked for 9am. Likely a loose neutral on the pole-mounted transformer.", true, now - TimeSpan.FromHours(18));
        cases.Add(c5);

        var c6 = Case(omar, "How does Time-of-Use pricing work?",
            "I switched to the Time-of-Use plan. Which hours are cheapest, and does it make sense to run my dishwasher overnight?",
            CaseCategory.General, CasePriority.Low, Channel.Portal, TimeSpan.FromDays(3));
        c6.AssignTo(aiko, now - TimeSpan.FromDays(3) + TimeSpan.FromHours(2));
        c6.AddMessage(AuthorType.Staff, aiko.Name,
            "Hi Omar! Off-peak hours are 9pm to 7am on weekdays and all day on weekends. Running your dishwasher overnight is a great way to save.",
            false, now - TimeSpan.FromDays(3) + TimeSpan.FromHours(3));
        c6.ChangeStatus(CaseStatus.Resolved, now - TimeSpan.FromDays(2));
        cases.Add(c6);

        var c7 = Case(linda, "Equal Payment Plan amount changed",
            "My Equal Payment Plan amount went up from $96 to $128. Why did it change?",
            CaseCategory.Billing, CasePriority.Normal, Channel.Email, TimeSpan.FromHours(10));
        c7.AssignTo(priya, now - TimeSpan.FromHours(9));
        cases.Add(c7);

        var c8 = Case(grace, "Smart meter display shows an error",
            "The display on my meter shows 'Err 04' and has been blinking since yesterday.",
            CaseCategory.Meter, CasePriority.Normal, Channel.Sms, TimeSpan.FromHours(2));
        cases.Add(c8);

        var c9 = Case(raj, "Request for a copy of last year's invoices",
            "Our accountant needs copies of all invoices for last year for the business account.",
            CaseCategory.Billing, CasePriority.Low, Channel.Email, TimeSpan.FromDays(4));
        c9.AssignTo(priya, now - TimeSpan.FromDays(4) + TimeSpan.FromHours(4));
        c9.AddMessage(AuthorType.Staff, priya.Name, "Hi Raj, I've emailed the PDF invoices to the address on file.", false, now - TimeSpan.FromDays(3));
        c9.ChangeStatus(CaseStatus.Resolved, now - TimeSpan.FromDays(3));
        c9.ChangeStatus(CaseStatus.Closed, now - TimeSpan.FromDays(1));
        cases.Add(c9);

        var c10 = Case(emily, "Payment didn't go through",
            "I tried paying online this morning and got an error, but my bank shows the money as pending.",
            CaseCategory.Payment, CasePriority.Normal, Channel.Chat, TimeSpan.FromMinutes(40));
        cases.Add(c10);

        var c11 = Case(sarah, "Tree branch resting on power line",
            "A large branch came down in the storm and is resting on the line running to our house. Nobody has touched it.",
            CaseCategory.Outage, CasePriority.Urgent, Channel.Phone, TimeSpan.FromHours(1.5));
        c11.AssignTo(marcus, now - TimeSpan.FromHours(1.4));
        c11.AddMessage(AuthorType.Staff, marcus.Name,
            "Please stay at least 10 metres away from the branch and line. A crew is on the way and should arrive within the hour.",
            false, now - TimeSpan.FromHours(1.3));
        cases.Add(c11);

        var articles = new[]
        {
            new KnowledgeArticle("What to do during a power outage", CaseCategory.Outage,
                "Check whether your neighbours have power. If only your home is affected, check your main breaker or fuse panel first. If the whole street is out, check the Highland Energy outage map or report the outage through the portal or by phone at 1-888-555-0100. Unplug sensitive electronics, keep fridge and freezer doors closed (food stays frozen for about 24 hours in a full freezer), and never run a generator indoors."),
            new KnowledgeArticle("Downed or damaged power lines: stay safe", CaseCategory.Outage,
                "Always assume a downed line is live. Stay at least 10 metres (33 feet) away and keep others away. Do not touch anything in contact with the line, including tree branches, fences or vehicles. Call 911 and Highland Energy's emergency line at 1-888-555-0199 immediately. These reports are always treated as urgent."),
            new KnowledgeArticle("Understanding your bill", CaseCategory.Billing,
                "Your bill shows your billing period, meter readings, usage in kilowatt-hours (kWh), the energy charge, a basic monthly charge, and taxes. Readings marked 'E' are estimated; readings marked 'A' are actual. When an estimate is lower than your real usage, the next actual reading corrects it, which can make one bill higher than usual."),
            new KnowledgeArticle("Why is my bill higher than usual?", CaseCategory.Billing,
                "Common causes are colder weather and electric heating, a catch-up after estimated readings, new appliances such as space heaters or EV chargers, or a longer billing period. Compare your daily average kWh with the same month last year. If you still think something is wrong, submit a photo of your meter display and we will check it against our records."),
            new KnowledgeArticle("Equal Payment Plan", CaseCategory.Billing,
                "The Equal Payment Plan spreads your yearly cost into equal monthly payments. We review your amount every six months against your actual usage. If you used more energy than expected, or rates changed, your monthly amount can go up; if you used less, it can go down."),
            new KnowledgeArticle("Payment arrangements", CaseCategory.Payment,
                "If you are having trouble paying, we can set up a payment arrangement that splits your overdue balance across up to 12 months, on top of your regular bills. Arrangements are available to residential customers in good standing who have not missed an arrangement in the last 12 months. Contact us before your due date to avoid late charges."),
            new KnowledgeArticle("Ways to pay your bill", CaseCategory.Payment,
                "Pay online in the customer portal, through online banking (use your account number as the payee reference), by pre-authorized debit, or by phone. Online banking payments can take 2 to 3 business days to reach us. If a payment shows as pending at your bank but failed in the portal, do not pay again; it is usually reversed within 3 business days."),
            new KnowledgeArticle("Moving: start, stop or transfer service", CaseCategory.MoveService,
                "Let us know at least 5 business days before you move. Give us your move date, your new address, and a forwarding address for your final bill. We take a final meter reading at your old address on the move date and start service at the new address on the same day, so there is no gap in service."),
            new KnowledgeArticle("Smart meter error codes", CaseCategory.Meter,
                "Err 01 to Err 03 are temporary communication errors and clear on their own within 24 hours. Err 04 means the meter could not complete a self-test and needs a technician visit; your power is not affected and your billing will use an estimate until it is fixed. Never open the meter or break its seal."),
            new KnowledgeArticle("Time-of-Use pricing", CaseCategory.General,
                "On the Time-of-Use plan, off-peak hours are 9pm to 7am on weekdays and all day on weekends and statutory holidays. On-peak hours are 7am to 9pm on weekdays. Shifting dishwashers, laundry and EV charging to off-peak hours is the easiest way to save."),
        };

        db.Staff.AddRange(staff);
        db.Customers.AddRange(customers);
        db.Cases.AddRange(cases);
        db.KnowledgeArticles.AddRange(articles);
        db.DemoState.Add(new DemoState { SeededAt = now });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Wipes all CRM data and reloads the demo set, in one transaction.</summary>
    public static async Task ResetAsync(RelayDbContext db, DateTimeOffset now, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.CaseMessages.ExecuteDeleteAsync(ct);
            await db.Cases.ExecuteDeleteAsync(ct);
            await db.Customers.ExecuteDeleteAsync(ct);
            await db.Accounts.ExecuteDeleteAsync(ct);
            await db.Staff.ExecuteDeleteAsync(ct);
            await db.KnowledgeArticles.ExecuteDeleteAsync(ct);
            await db.DemoState.ExecuteDeleteAsync(ct);
            await db.Database.ExecuteSqlRawAsync("ALTER SEQUENCE CaseNumbers RESTART WITH 1001", ct);
            db.ChangeTracker.Clear();
            await SeedAsync(db, now, ct);
            await tx.CommitAsync(ct);
        });
    }
}
