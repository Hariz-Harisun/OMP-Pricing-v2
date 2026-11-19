namespace OmpPricing.App.Pricing;

/// <summary>Line statuses from A6 rule 9.</summary>
public enum LineStatus
{
    Queued,
    Priced,
    PricedReview,
    Incomplete,
    NotFound,
    NeedsReview,
    WaitingForQuota,
    InvalidInput,
}
