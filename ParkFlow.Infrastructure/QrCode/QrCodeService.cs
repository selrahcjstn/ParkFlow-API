using System;
using ParkFlow.Application.Interfaces;
using QRCoder;

namespace ParkFlow.Infrastructure.QrCode;

public class QrCodeService : IQrCodeService
{
    public byte[] GenerateQrCode(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        
        return qrCode.GetGraphic(20);   
    }
}
