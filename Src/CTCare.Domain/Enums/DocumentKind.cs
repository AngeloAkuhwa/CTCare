namespace CTCare.Domain.Enums;

[Flags]
public enum DocumentKind
{
    DoctorsNote = 1,
    MedicalCertificate = 2,
    CourtSummons = 3,
    BereavementProof = 4,
    Other = 99
}
