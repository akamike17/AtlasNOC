namespace AtlasNOC.Domain.Entities;

public static class ServiceCreditCalculator
{
    public static decimal Calculate(decimal monthlyPrice, DateTime fromUtc, DateTime toUtc)
    {
        if (monthlyPrice < 0 || toUtc <= fromUtc) throw new ArgumentException("Periodo o tarifa inválidos.");
        var minutes = (decimal)(toUtc - fromUtc).TotalMinutes;
        return Math.Min(monthlyPrice, Math.Round(monthlyPrice * minutes / (30m * 24m * 60m), 2));
    }
}
