using System.Threading.Tasks;

namespace TradeStorm.Api.Services;

public interface IEmailService
{
    Task<bool> SendVerificationCodeAsync(string toEmail, string userName, string code);
    Task<bool> SendPasswordResetCodeAsync(string toEmail, string userName, string code, string requestIp);
    Task<bool> SendPasswordChangedAlertAsync(string toEmail, string userName, string requestIp);
}
