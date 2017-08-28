using System;
using System.Threading;
using Akka.Actor;

namespace ScrumBot
{
    public class Runner : IDisposable
    {
        private ActorSystem _actorSystem;
        private ManualResetEventSlim _mre;
        public void Start()
        {
            _actorSystem = ActorSystem.Create("ScrumBot");
            _actorSystem.ActorOf(Props.Create(() => new ScrumBot()));
            _mre = new ManualResetEventSlim();
        }

        public void Wait()
        {
            _mre.Wait();
        }

        public void Stop()
        {
            _mre.Set();
        }

        public void Dispose()
        {
            if (_actorSystem != null) _actorSystem.Dispose();
        }
    }
}
