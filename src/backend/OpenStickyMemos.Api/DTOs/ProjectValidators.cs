using FluentValidation;

namespace OpenStickyMemos.Api.DTOs;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proyecto es obligatorio")
            .MaximumLength(128).WithMessage("El nombre no puede exceder 128 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(1024).When(x => x.Description is not null)
            .WithMessage("La descripción no puede exceder 1024 caracteres");
    }
}

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proyecto es obligatorio")
            .MaximumLength(128).WithMessage("El nombre no puede exceder 128 caracteres");
    }
}

public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio")
            .EmailAddress().WithMessage("El email no es válido");
    }
}
