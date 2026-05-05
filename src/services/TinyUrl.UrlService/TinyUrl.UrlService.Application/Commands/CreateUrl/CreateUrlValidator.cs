using FluentValidation;

namespace TinyUrl.UrlService.Application.Commands.CreateUrl;

public class CreateUrlValidator : AbstractValidator<CreateUrlCommand>
{
    public CreateUrlValidator()
    {
        RuleFor(x => x.LongUrl).NotEmpty()
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var r) && (r.Scheme == "http" || r.Scheme == "https"))
            .WithMessage("Must be a valid HTTP/HTTPS URL.");
        RuleFor(x => x.CustomAlias).MaximumLength(50).Matches("^[a-zA-Z0-9_-]+$")
            .When(x => !string.IsNullOrEmpty(x.CustomAlias));
    }
}
