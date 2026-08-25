using System;

namespace BlackBarLabs.SendGrid.Exceptions
{
    public class EmailSubstitutionParameterException : Exception
    {
        private readonly string extraneousSubstitutionParameter;

        public EmailSubstitutionParameterException(string extraneousSubstitutionParameter)
        {
            this.extraneousSubstitutionParameter = extraneousSubstitutionParameter;
        }
    }
}
