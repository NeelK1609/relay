namespace Relay.Domain;

public enum CaseStatus { New, InProgress, WaitingOnCustomer, Resolved, Closed }

public enum CasePriority { Low, Normal, High, Urgent }

public enum CaseCategory { Outage, Billing, Payment, MoveService, Meter, General }

public enum Channel { Portal, Email, Chat, Phone, Sms }

public enum AuthorType { Customer, Staff, AiAgent, System }

public enum Sentiment { Positive, Neutral, Negative, Distressed }
