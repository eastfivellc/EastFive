using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EastFive.Web.Services
{
    public struct SendMessageTemplate
    {
        public string externalTemplateId;
        public string name;
    }

    public interface ISendMessageService
    {
        Task<TResult> SendEmailMessageAsync<TResult>(
            string templateName,
            string toAddress, string toName,
            string fromAddress, string fromName,
            string subject,
            IDictionary<string, string> substitutionsSingle,
            IDictionary<string, IDictionary<string, string>[]> substitutionsMultiple,
            Func<string, TResult> onSuccess,
            Func<TResult> onServiceUnavailable,
            Func<string, TResult> onFailed);

        Task<SendMessageTemplate[]> ListTemplatesAsync();
    }
}