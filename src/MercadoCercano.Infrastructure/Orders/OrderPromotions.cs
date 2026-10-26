using System.Numerics;
using MercadoCercano.Application.Orders;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Promotions;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Orders;

public sealed partial class OrderService
{
    private sealed record PromotionCandidate(Promotion Rule, long Amount, HashSet<Guid> Eligible);

    private async Task<PromotionCandidate?> SelectPromotionAsync(QuoteRequest request, List<QuoteLineResponse> lines,
        IReadOnlyDictionary<Guid, Product> products, DateTime now, CancellationToken ct)
    {
        var code = PromotionService.NormalizeCode(request.CouponCode);
        var rules = await db.Promotions.AsNoTracking().Where(item => item.IsActive && item.StartsAtUtc <= now && item.EndsAtUtc > now &&
            item.UsedCount < int.MaxValue && (!item.MaxUses.HasValue || item.UsedCount < item.MaxUses) &&
            (item.Code == null || EF.Functions.Collate(item.Code, "NOCASE") == code)).ToListAsync(ct);
        var eligible = new List<PromotionCandidate>();
        foreach (var rule in rules)
        {
            var selected = lines.Where(line => (!rule.ProductId.HasValue || rule.ProductId == line.ProductId) &&
                (!rule.CategoryId.HasValue || rule.CategoryId == products[line.ProductId].CategoryId)).ToArray();
            var amount = selected.Sum(line => line.LineTotal);
            if (amount == 0 || amount < rule.MinimumOrderAmount) continue;
            var discount = rule.DiscountType == DiscountType.Percentage ? Proportion(amount, rule.DiscountValue, 100) : rule.DiscountValue;
            discount = Math.Min(amount, Math.Min(discount, rule.MaximumDiscountAmount ?? long.MaxValue));
            if (discount > 0) eligible.Add(new(rule, discount, selected.Select(line => line.ProductId).ToHashSet()));
        }
        if (code is not null && !eligible.Any(item => PromotionService.NormalizeCode(item.Rule.Code) == code))
            throw Validation("El cupon no esta disponible o no cumple vigencia, cupo, alcance o compra minima. Corrigelo o quitalo y recalcula.");
        return eligible.OrderByDescending(item => item.Amount).ThenByDescending(item => item.Rule.Code != null)
            .ThenBy(item => item.Rule.Id).FirstOrDefault();
    }

    private static List<QuoteLineResponse> DistributeDiscount(List<QuoteLineResponse> lines, PromotionCandidate candidate)
    {
        var eligible = lines.Where(line => candidate.Eligible.Contains(line.ProductId)).ToArray();
        var basis = eligible.Sum(line => line.LineTotal);
        var shares = eligible.Select(line =>
        {
            var amount = BigInteger.DivRem((BigInteger)candidate.Amount * line.LineTotal, basis, out var remainder);
            return (line.ProductId, Amount: (long)amount, Remainder: remainder);
        }).ToArray();
        var remaining = candidate.Amount - shares.Sum(item => item.Amount);
        var extras = shares.OrderByDescending(item => item.Remainder).ThenBy(item => item.ProductId)
            .Take((int)remaining).Select(item => item.ProductId).ToHashSet();
        var discounts = shares.ToDictionary(item => item.ProductId, item => item.Amount + (extras.Contains(item.ProductId) ? 1 : 0));
        return lines.Select(line => line with { PromotionDiscountTotal = discounts.GetValueOrDefault(line.ProductId) }).ToList();
    }

    private async Task ConsumePromotionAsync(Promotion rule, Order order, CancellationToken ct)
    {
        var changed = await db.Promotions.Where(item => item.Id == rule.Id && item.UsedCount < int.MaxValue &&
            (!item.MaxUses.HasValue || item.UsedCount < item.MaxUses))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UsedCount, item => item.UsedCount + 1), ct);
        if (changed != 1) throw Conflict("El cupo promocional cambio. Recalcula antes de enviar.");
        db.PromotionUsages.Add(new PromotionUsage { Order = order, PromotionId = rule.Id, DiscountAmount = order.PromotionDiscountTotal });
    }

    private async Task ReleasePromotionAsync(Order order, CancellationToken ct)
    {
        var usage = await db.PromotionUsages.SingleOrDefaultAsync(item => item.OrderId == order.Id && item.ReleasedAtUtc == null, ct);
        if (usage is null) return;
        var changed = await db.Promotions.Where(item => item.Id == usage.PromotionId && item.UsedCount > 0)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UsedCount, item => item.UsedCount - 1), ct);
        if (changed != 1) throw Conflict("El contador de la promocion requiere revision; no se cancelo el pedido.");
        usage.ReleasedAtUtc = DateTime.UtcNow;
    }

    private static long Proportion(long amount, long quantity, long total) => (long)((BigInteger)amount * quantity / total);
}
