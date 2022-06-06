using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;

namespace TCC.Parser
{
    public abstract class TccCommand<TOption> : Command where TOption : class
    {
        public TccCommand(string name, string description = null) : base(name, description)
        {
            Handler = CommandHandler.Create<TOption, IHost>(RunAsync);
            foreach (var option in CreateOptions())
            {
                AddOption(option);
            }
            foreach (var argument in CreateArguments())
            {
                AddArgument(argument);
            }
        }

        private Task RunAsync(TOption options, IHost host)
        {
            var serviceProvider = host.Services;
            var tccController = serviceProvider.GetRequiredService<ITccController>();
            return RunAsync(tccController, options);
        }

        protected abstract Task RunAsync(ITccController controller, TOption option);
        protected abstract IEnumerable<Option> CreateOptions();
        protected abstract IEnumerable<Argument> CreateArguments();
    }
}