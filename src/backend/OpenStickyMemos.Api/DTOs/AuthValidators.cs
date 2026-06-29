using FluentValidation;

namespace OpenStickyMemos.Api.DTOs;

public class ExternalLoginRequestValidator : AbstractValidator<ExternalLoginRequest>
{
    public ExternalLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("El token de ID es obligatorio");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("El proveedor es obligatorio")
            .Must(p => p is "Google" or "Microsoft")
            .WithMessage("El proveedor debe ser 'Google' o 'Microsoft'");
    }
}

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("El refresh token es obligatorio");
    }
}
