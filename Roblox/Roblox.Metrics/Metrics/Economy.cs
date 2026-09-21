using System.Diagnostics;

namespace Roblox.Metrics;

public enum PurchaseProductType { Asset, DeveloperProduct }
public enum PurchaseFailureReason { AlreadyOwned, NoLongerForSale, InsufficientFunds, StockExhausted }

public static class EconomyMetrics
{
    public static void ReportRobuxVolumeChange(long robuxAmount)
    {
        Debug.Assert(robuxAmount > 0);
        if (robuxAmount > 0) RobloxMetrics.EconomyRobuxVolume.Add(robuxAmount);
    }

    public static void ReportPurchaseDuration(long elapsedMilliseconds, PurchaseProductType productType, bool isResale)
    {
        RobloxMetrics.PurchaseDuration.Record(elapsedMilliseconds,
            new KeyValuePair<string, object?>("purchase.product_type", ProductType(productType)),
            new KeyValuePair<string, object?>("purchase.sale_type", isResale ? "resale" : "first_party"));
    }

    public static void ReportPurchaseFailure(PurchaseFailureReason reason, PurchaseProductType productType)
    {
        RobloxMetrics.PurchaseFailures.Add(1,
            new KeyValuePair<string, object?>("failure.reason", reason switch
            {
                PurchaseFailureReason.AlreadyOwned => "already_owned",
                PurchaseFailureReason.NoLongerForSale => "no_longer_for_sale",
                PurchaseFailureReason.InsufficientFunds => "insufficient_funds",
                PurchaseFailureReason.StockExhausted => "stock_exhausted",
                _ => "unknown",
            }),
            new KeyValuePair<string, object?>("purchase.product_type", ProductType(productType)));
    }

    private static string ProductType(PurchaseProductType type) =>
        type == PurchaseProductType.DeveloperProduct ? "developer_product" : "asset";
}
