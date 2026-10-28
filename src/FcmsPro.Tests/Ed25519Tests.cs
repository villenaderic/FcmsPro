using System;
using System.IO;
using System.Text;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using Xunit;

namespace FcmsPro.Tests;

public class Ed25519Tests
{
    private const string PublicKeyBase64 = "lw1Jki2HknZpQQwcC1h3Sk282FtS9G303XLgscGcVpQ=";

    [Fact]
    public void VerifyEd25519Checker_WithValidSignature_Succeeds()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = Encoding.UTF8.GetBytes("Hello NetSparkle Ed25519 verification!");
            File.WriteAllBytes(tempFile, data);

            var checker = new Ed25519Checker(SecurityMode.Strict, PublicKeyBase64);
            var sig = "fmi4FhsumDD7q8TpcT2mdp3JvUo8K7Z8OVvDYdjvuj74ptN8QJ6lLqLGdpOnr/j+ELMmbf7SfHoWf50k6TnmCg==";

            var res = checker.VerifySignatureOfFile(sig, tempFile);
            Assert.Equal(ValidationResult.Valid, res);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void VerifyEd25519Checker_WithTamperedFile_Fails()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = Encoding.UTF8.GetBytes("Tampered content!");
            File.WriteAllBytes(tempFile, data);

            var checker = new Ed25519Checker(SecurityMode.Strict, PublicKeyBase64);
            var sig = "fmi4FhsumDD7q8TpcT2mdp3JvUo8K7Z8OVvDYdjvuj74ptN8QJ6lLqLGdpOnr/j+ELMmbf7SfHoWf50k6TnmCg==";

            var res = checker.VerifySignatureOfFile(sig, tempFile);
            Assert.Equal(ValidationResult.Invalid, res);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void VerifyEd25519Checker_WithInvalidSignature_Fails()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = Encoding.UTF8.GetBytes("Hello NetSparkle Ed25519 verification!");
            File.WriteAllBytes(tempFile, data);

            var checker = new Ed25519Checker(SecurityMode.Strict, PublicKeyBase64);
            var invalidSig = Convert.ToBase64String(new byte[64]);

            var res = checker.VerifySignatureOfFile(invalidSig, tempFile);
            Assert.Equal(ValidationResult.Invalid, res);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
