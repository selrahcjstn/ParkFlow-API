using FluentValidation;
using System.IO;
using System.Linq;

namespace ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;

public class UploadOrcrDocumentCommandValidator : AbstractValidator<UploadOrcrDocumentCommand>
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

    public UploadOrcrDocumentCommandValidator()
    {
        RuleFor(x => x.CorSubmissionId)
            .NotEmpty()
            .WithMessage("COR submission ID is required.");

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required.")
            .Must(file => file != null && file.Length > 0)
            .WithMessage("File cannot be empty.")
            .Must(file =>
            {
                if (file == null) return false;
                var extension = Path.GetExtension(file.FileName).ToLower();
                return AllowedExtensions.Contains(extension);
            })
            .WithMessage("Invalid file format. Only PDF and image formats (JPG, JPEG, PNG) are allowed for OR/CR documents.");
    }
}
