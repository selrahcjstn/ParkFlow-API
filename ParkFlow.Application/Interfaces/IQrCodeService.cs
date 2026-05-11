namespace ParkFlow.Application.Interfaces;
    
public interface IQrCodeService
{
    byte[] GenerateQrCode(string text);
}