using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Application.Transactions;

/// <summary>
/// The checks add and edit share. Both write into the same derived position, so both have
/// to answer the same question: would this leave the holding owing units it never had?
/// </summary>
internal static class TransactionRules
{
    public static void ValidateShape(decimal quantity, decimal unitPrice, string currency)
    {
        // Direction lives in the type (Sell, Withdrawal), never in the sign — a negative
        // quantity on a Sell would subtract twice.
        if (quantity <= 0m)
            throw new DomainValidationException("Кількість має бути більшою за нуль.");

        if (unitPrice < 0m)
            throw new DomainValidationException("Ціна не може бути від'ємною.");

        if (!SupportedCurrencies.All.Contains(currency))
            throw new DomainValidationException($"Валюта {currency} не підтримується.");
    }

    /// <summary>
    /// Rejects a write that would push the position below zero. <paramref name="existing"/>
    /// is every transaction the holding has right now, including the row being edited — it
    /// is matched out by id and replaced with <paramref name="written"/>.
    ///
    /// Checked for every write, not just sells: editing a buy down past the sells that
    /// followed it lands in the same place. Deleting, on the other hand, is not checked —
    /// it is the escape hatch for a row that should never have existed, and guarding it
    /// would mean unwinding the whole chain in order just to drop one mistake.
    ///
    /// Back-dating is fine: the position is a net over the whole set, so the order rows
    /// were entered in never matters.
    /// </summary>
    public static void EnsurePositionStaysNonNegative(IEnumerable<Transaction> existing, Transaction written)
    {
        var position = HoldingPositions.Of(existing.Where(t => t.Id != written.Id).Append(written)) ?? 0m;
        if (position >= 0m)
            return;

        throw new DomainValidationException(
            $"Після цієї операції позиція стала б від'ємною ({position:0.####} од.). "
            + "Спершу додайте купівлю або виправте кількість.");
    }
}
