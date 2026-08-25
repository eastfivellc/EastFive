using System;
using System.Threading.Tasks;
using System.Text;

namespace TestGeneration
{
    class Program
    {
        static void Main()
        {
            var generator = new Generator();
            var result = generator.GenerateSingleOverload(0, 1);
            Console.WriteLine(result);
        }
    }

    class Generator
    {
        public string GenerateSingleOverload(int numPrimes, int numDelayed)
        {
            var sb = new StringBuilder();

            // Build generic type parameters
            var typeParams = new StringBuilder();
            for (int i = 1; i <= numPrimes; i++)
                typeParams.Append($"TPrime{i}, ");
            for (int i = 1; i <= numDelayed; i++)
                typeParams.Append($"TDelayed{i}, ");
            typeParams.Append("TResult");

            // Build func signature (input parameters)
            var funcParams = new StringBuilder();
            for (int i = 1; i <= numPrimes; i++)
                funcParams.Append($"TPrime{i}, ");
            for (int i = 1; i <= numDelayed; i++)
                funcParams.Append($"TDelayed{i}, ");
            funcParams.Append("Task<TResult>");

            // Build return type parameters
            var returnParams = new StringBuilder();
            for (int i = 1; i <= numPrimes; i++)
                returnParams.Append($"TPrime{i}, ");
            returnParams.Append("Task<TResult>");

            // Build method parameters
            var methodParams = new StringBuilder();
            for (int i = 1; i <= numDelayed; i++)
            {
                if (i > 1) methodParams.Append(",\n");
                methodParams.Append($"            InvokeDelayedAsyncCallback<TDelayed{i}, TResult> delayed{i}Callback");
            }

            sb.AppendLine($"        public static Func<{returnParams}> InvokeDelayed<{typeParams}>(");
            sb.AppendLine($"            this Func<{funcParams}> func,");
            sb.AppendLine(methodParams.ToString());
            sb.AppendLine("        {");

            return sb.ToString();
        }
    }
}
