using System;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Azure.Auth
{
    public class AppleProviderAttribute : Attribute, IProvideLoginProvider
    {
        private const string appleKeyServerUrl = "https://appleid.apple.com/auth/keys";

        public Task<TResult> ProvideLoginProviderAsync<TResult>(
            Func<IProvideLogin, TResult> onLoaded,
            Func<string, TResult> onNotAvailable)
        {
            return EastFive.Configuration.Configured.Member<AppleConfiguration, string, Task<TResult>>(config => config.ClientId,
                EastFive.Azure.Auth.AppleConfiguration.ClientIdKey,
                applicationId =>
                {
                    return EastFive.Configuration.Configured.Member<AppleConfiguration, string, Task<TResult>>(config => config.ValidAudiences,
                        EastFive.Azure.Auth.AppleConfiguration.ValidAudiencesKey,
                        (validAudiencesStr) =>
                        {
                            var validAudiences = validAudiencesStr.Split(','.AsArray());
                            return OAuth.Keys.LoadTokenKeysAsync(new Uri(appleKeyServerUrl),
                                keys =>
                                {
                                    var provider = new AppleProvider(applicationId, validAudiences, keys);
                                    return onLoaded(provider);
                                },
                                onFailure: onNotAvailable);
                        },
                        onNotAvailable.AsAsyncFunc());
                },
                onNotAvailable.AsAsyncFunc());
        }
    }
}

