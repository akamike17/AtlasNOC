namespace AtlasNOC.Domain.Entities;

public sealed record PrepaymentQuote(int Months, int BonusDays, decimal MonthlyPrice, decimal Total);

/// Política inicial configurable para prepago; no contiene reglas fiscales.
public sealed class PrepaymentPolicy
{
    private readonly IReadOnlyDictionary<int, int> _bonusDays;
    public PrepaymentPolicy(IReadOnlyDictionary<int, int>? bonusDays = null) => _bonusDays = bonusDays ?? new Dictionary<int, int> { [3] = 5, [6] = 10, [9] = 15, [12] = 30 };
    public PrepaymentQuote Quote(int months, decimal monthlyPrice)
    {
        if (months <= 0 || monthlyPrice < 0) throw new ArgumentOutOfRangeException();
        var bonus = _bonusDays.TryGetValue(months, out var days) ? days : 0;
        return new PrepaymentQuote(months, bonus, monthlyPrice, months * monthlyPrice);
    }
}
