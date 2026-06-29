using FluentValidation;

namespace OpenStickyMemos.Api.DTOs;

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(256).When(x => x.Title is not null)
            .WithMessage("El título no puede exceder 256 caracteres");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("El color es obligatorio")
            .MaximumLength(16).WithMessage("El color no puede exceder 16 caracteres");

        RuleFor(x => x.Width)
            .InclusiveBetween(100, 800).WithMessage("El ancho debe estar entre 100 y 800");

        RuleFor(x => x.Height)
            .InclusiveBetween(80, 800).WithMessage("La altura debe estar entre 80 y 800");
    }
}

public class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(256).When(x => x.Title is not null)
            .WithMessage("El título no puede exceder 256 caracteres");

        RuleFor(x => x.Color)
            .MaximumLength(16).When(x => x.Color is not null)
            .WithMessage("El color no puede exceder 16 caracteres");

        RuleFor(x => x.Width)
            .InclusiveBetween(100, 800).When(x => x.Width.HasValue)
            .WithMessage("El ancho debe estar entre 100 y 800");

        RuleFor(x => x.Height)
            .InclusiveBetween(80, 800).When(x => x.Height.HasValue)
            .WithMessage("La altura debe estar entre 80 y 800");
    }
}
