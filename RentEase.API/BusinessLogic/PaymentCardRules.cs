namespace PropertyLeasing.BusinessLogic;

/// <summary>Validation for simulated card payments (MM/YY expiry).</summary>
public static class PaymentCardRules
{
    public static IReadOnlyList<string> ValidateExpiryDate(string? expiry, DateTime? referenceUtc = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(expiry))
        {
            errors.Add("Expiry date is required (MM/YY).");
            return errors;
        }

        var trimmed = expiry.Trim().Replace(" ", "");
        var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            errors.Add("Enter expiry as MM/YY (e.g. 08/28).");
            return errors;
        }

        if (!int.TryParse(parts[0], out var month) || month is < 1 or > 12)
        {
            errors.Add("Expiry month must be between 01 and 12.");
            return errors;
        }

        if (!int.TryParse(parts[1], out var yearPart))
        {
            errors.Add("Expiry year is invalid.");
            return errors;
        }

        var year = yearPart switch
        {
            >= 100 => yearPart,
            >= 0 and <= 99 => 2000 + yearPart,
            _ => -1
        };

        if (year < 2000)
        {
            errors.Add("Expiry year is invalid.");
            return errors;
        }

        var now = referenceUtc ?? DateTime.Now;
        var expiryYearMonth = year * 100 + month;
        var currentYearMonth = now.Year * 100 + now.Month;

        if (expiryYearMonth < currentYearMonth)
            errors.Add("This card has expired. Use a future expiry date (MM/YY).");

        return errors;
    }
}
