using System;
using System.Collections.Generic;
using System.Linq;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

/// <summary>
/// Müşteri (CRM) kalıcılığı + arama/dedup. Tek JSON dosyası (customers.json).
/// </summary>
public class CustomerService
{
    public List<Customer> Load() => JsonStore.LoadList<Customer>(AppPaths.CustomersFile);

    public void Save(IEnumerable<Customer> items) =>
        JsonStore.SaveList(AppPaths.CustomersFile, items);

    /// <summary>E-posta ile dedup'lu birleştirme (Excel içe aktarımda). Aynı e-posta varsa GÜNCELLER.</summary>
    public static void UpsertByEmail(IList<Customer> target, Customer incoming)
    {
        var email = (incoming.Email ?? "").Trim().ToLowerInvariant();
        Customer? existing = string.IsNullOrEmpty(email)
            ? null
            : target.FirstOrDefault(c => (c.Email ?? "").Trim().ToLowerInvariant() == email);

        if (existing is null) { target.Add(incoming); return; }

        existing.CompanyName = incoming.CompanyName;
        existing.ContactName = incoming.ContactName;
        existing.Salutation = incoming.Salutation;
        existing.Phone = incoming.Phone;
        existing.Address = incoming.Address;
        existing.TaxOffice = incoming.TaxOffice;
        existing.TaxNumber = incoming.TaxNumber;
        if (!string.IsNullOrWhiteSpace(incoming.Tags)) existing.Tags = incoming.Tags;
    }

    /// <summary>Serbest metin arama (firma/kişi/e-posta/telefon/etiket).</summary>
    public static IEnumerable<Customer> Search(IEnumerable<Customer> source, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return source;
        var q = query.Trim().ToLowerInvariant();
        return source.Where(c =>
            (c.CompanyName ?? "").ToLowerInvariant().Contains(q) ||
            (c.ContactName ?? "").ToLowerInvariant().Contains(q) ||
            (c.Email ?? "").ToLowerInvariant().Contains(q) ||
            (c.Phone ?? "").ToLowerInvariant().Contains(q) ||
            (c.Tags ?? "").ToLowerInvariant().Contains(q));
    }
}
