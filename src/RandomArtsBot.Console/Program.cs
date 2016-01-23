using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Topshelf;
using RandomArtsBot;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace RandomArtsBot.Console
{
    class Program
    {
        private static ILog Logger = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            Logger.Info("Starting RandomArtsBot console app");

            HostFactory.Run(x =>
            {
                x.Service<Bot>(s =>
                {
                    s.ConstructUsing(name => new Bot("@randomartsbot"));
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });

                x.RunAsLocalSystem();

                x.SetDescription("F Random Arts Bot");
                x.SetDisplayName("RandomArtsBot");
                x.SetServiceName("RandomArtsBot");
            });
        }
    }
}