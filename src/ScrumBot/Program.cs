using System;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace ScrumBot
{
    class Program
    {
        static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Console.Error.WriteLine($"Unhandled exception: {e.Exception.Message}");
                Console.Error.WriteLine(e.Exception.StackTrace);
                e.SetObserved();
            };

            using (var runner = new Runner())
            {
                Console.Write("Starting ScrumBot...");
                runner.Start();
                AssemblyLoadContext.Default.Unloading += _ => runner.Stop();
                Console.WriteLine(" started!");

                runner.Wait();

                Console.Write("Stopping ScrumBot...");
            }

            Console.WriteLine(" stopped!");
        }
    }
}
