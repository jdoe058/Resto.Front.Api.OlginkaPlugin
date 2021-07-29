using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;

using Resto.Front.Api.Attributes;
using Resto.Front.Api.Attributes.JetBrains;

namespace Resto.Front.Api.OlginkaPlugin
{
    [UsedImplicitly]
    [PluginLicenseModuleId(21016318)]
    public sealed class OlginkaPlugin : IFrontPlugin
    {
        private readonly Stack<IDisposable> subscriptions = new Stack<IDisposable>();

        public void Dispose()
        {
            while (subscriptions.Any())
            {
                var subscription = subscriptions.Pop();
                try
                {
                    subscription.Dispose();
                }
                catch (RemotingException)
                {
                    // nothing to do with the lost connection
                }
            }
            PluginContext.Log.Info("OlginkaPlugin stopped");
        }

        public OlginkaPlugin()
        {
            PluginContext.Log.Info("Initializing OlginkaPlugin");

            subscriptions.Push(new PayButton());

            PluginContext.Log.Info("SamplePlugin OlginkaPlugin");
        }

    }
}
