using System.Threading.Tasks;

namespace TradeStorm.Api.Services;

public interface IEmailService
{
    Task<bool> SendVerificationCodeAsync(string toEmail, string userName, string code);
}
