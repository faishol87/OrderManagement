using System.ComponentModel.DataAnnotations;

namespace Order_Management.Common;

public static class Validators
{
    public static void ValidateAndThrow<T>(T model) where T : class
    {
        var results = new List<ValidationResult>();

        if (System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                model, new ValidationContext(model), results, validateAllProperties: true))
            return;

        var errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty("$"), (r, member) => (member, message: r.ErrorMessage ?? "Invalid"))
            .GroupBy(x => x.member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.message).ToArray());

        throw ApiException.BadRequest("VALIDATION_ERROR", "Payload tidak valid.", errors);
    }

    public static void EnsurePagination(int page, int pageSize)
    {
        if (page < 1)
            throw ApiException.BadRequest("INVALID_PAGE", "page minimal 1.");
        if (pageSize is < 1 or > 100)
            throw ApiException.BadRequest("INVALID_PAGE_SIZE", "pageSize harus antara 1 dan 100.");
    }
}