using System;
using NUnit.Framework;

namespace DryIoc.IssuesTests
{
    [TestFixture]
    public class GHIssue338_Child_container_disposes_parent_container_singletons
    {
        [Test, Ignore("todo: fixme")]
        public void Should_allow_registration_in_child_container()
        {
            var parent = new Container(rules => rules.WithConcreteTypeDynamicRegistrations());
            parent.Register<IService, Service>(Reuse.Singleton);
            var service = parent.Resolve<IService>();

            // var child = parent.WithRegistrationsCopy();

            // Basically it is the CreateFacade without WithFacadeRules and with IfAlreadyRegistered.Replace policy as default
            var child = CreateChild(parent);

            var service2 = child.Resolve<IService>();
            Assert.AreEqual(service, service2);

            child.Dispose();
            Assert.IsFalse(service.IsDisposed); //child container disposed parent singleton!!!

            parent.Dispose();
            Assert.IsTrue(service.IsDisposed);
        }

        private static IContainer CreateChild(IContainer parent, IfAlreadyRegistered childRegistrationPolicy = IfAlreadyRegistered.Replace) =>
            parent.With(
            parent.Parent,
            parent.Rules.WithDefaultIfAlreadyRegistered(childRegistrationPolicy),
            parent.ScopeContext,
            RegistrySharing.CloneAndDropCache, 
            parent.SingletonScope.Clone(),
            parent.CurrentScope?.Clone());


        public interface IService
        {
            bool IsDisposed { get; }
        }

        class Service : IService, IDisposable
        {
            public void Dispose() => IsDisposed = true;
            public bool IsDisposed { get; private set; }
        }
    }
}
